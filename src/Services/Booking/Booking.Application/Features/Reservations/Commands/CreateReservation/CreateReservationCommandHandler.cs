using Booking.Domain.Entities;
using Booking.Domain.Interfaces;
using Booking.Application.Interfaces;
using Booking.Application.Contracts;
using MediatR;
using MassTransit;

namespace Booking.Application.Features.Reservations.Commands.CreateReservation;

public class CreateReservationCommandHandler : IRequestHandler<CreateReservationCommand, Guid>
{
    private readonly IReservationRepository _reservationRepository;
    private readonly ISeatRepository _seatRepository;
    private readonly IDistributedLockService _lockService;
    private readonly IPublishEndpoint _publishEndpoint;

    public CreateReservationCommandHandler(
        IReservationRepository reservationRepository,
        ISeatRepository seatRepository,
        IDistributedLockService lockService,
        IPublishEndpoint publishEndpoint)
    {
        _reservationRepository = reservationRepository;
        _seatRepository = seatRepository;
        _lockService = lockService;
        _publishEndpoint = publishEndpoint;
    }

    public async Task<Guid> Handle(CreateReservationCommand request, CancellationToken cancellationToken)
    {
        // 1. Lock Distribuído (Redis)
        string lockKey = $"lock:seat:{request.SeatId}";
        string lockValue = Guid.NewGuid().ToString();
        
        bool lockAcquired = await _lockService.AcquireLockAsync(lockKey, lockValue, TimeSpan.FromSeconds(10));
        if (!lockAcquired) throw new Exception("Assento em disputa por outro usuario.");

        try
        {
            var seat = await _seatRepository.GetByIdAsync(request.SeatId);
            if (seat == null || seat.IsReserved) throw new Exception("Assento indisponivel.");

            // 2. Passo Inicial: Criar Reserva como PENDENTE
            seat.Reserve();
            var reservation = new Reservation(request.SeatId, request.UserId, TimeSpan.FromMinutes(10));
            
            await _seatRepository.UpdateAsync(seat);
            await _reservationRepository.AddAsync(reservation);

            // 3. MENSAGERIA: Publicar o evento de reserva criada
            // O Booking não espera o pagamento aqui, ele apenas avisa que a reserva aconteceu!
            await _publishEndpoint.Publish<ReservationCreatedEvent>(new {
                ReservationId = reservation.Id,
                Amount = 150.00m,
                UserId = request.UserId
            });

            return reservation.Id;
        }
        finally
        {
            await _lockService.ReleaseLockAsync(lockKey, lockValue);
        }
    }
}
