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
        string lockKey = $"lock:seat:{request.SeatId}";
        string lockValue = Guid.NewGuid().ToString();
        
        bool lockAcquired = await _lockService.AcquireLockAsync(lockKey, lockValue, TimeSpan.FromSeconds(10));
        if (!lockAcquired) throw new Exception("Assento em disputa por outro usuario.");

        try
        {
            var seat = await _seatRepository.GetByIdAsync(request.SeatId);
            if (seat == null || seat.IsReserved) throw new Exception("Assento indisponivel.");

            seat.Reserve();
            var reservation = new Reservation(request.SeatId, request.UserId, request.Price, request.TicketType, TimeSpan.FromMinutes(10));
            
            await _seatRepository.UpdateAsync(seat);
            await _reservationRepository.AddAsync(reservation);

            await _publishEndpoint.Publish<ReservationCreatedEvent>(new {
                ReservationId = reservation.Id,
                Amount = request.Price,
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
