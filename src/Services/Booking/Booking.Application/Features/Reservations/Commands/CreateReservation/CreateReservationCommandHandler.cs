using Booking.Domain.Entities;
using Booking.Domain.Interfaces;
using Booking.Application.Interfaces;
using Booking.Application.Contracts;
using MediatR;
using MassTransit;

namespace Booking.Application.Features.Reservations.Commands.CreateReservation;

public class CreateReservationCommandHandler : IRequestHandler<CreateReservationCommand, List<Guid>>
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

    public async Task<List<Guid>> Handle(CreateReservationCommand request, CancellationToken cancellationToken)
    {
        var reservationIds = new List<Guid>();
        var locks = new List<(string key, string value)>();

        try
        {
            // 1. Tentar adquirir lock para todos os assentos
            foreach (var seatReq in request.Seats)
            {
                string lockKey = $"lock:seat:{seatReq.SeatId}";
                string lockValue = Guid.NewGuid().ToString();
                bool lockAcquired = await _lockService.AcquireLockAsync(lockKey, lockValue, TimeSpan.FromSeconds(15));
                
                if (!lockAcquired) throw new Exception($"Assento {seatReq.SeatId} está sendo reservado por outro usuário.");
                locks.Add((lockKey, lockValue));
            }

            // 2. Validar disponibilidade e criar reservas
            foreach (var seatReq in request.Seats)
            {
                var seat = await _seatRepository.GetByIdAsync(seatReq.SeatId);
                if (seat == null || seat.IsReserved) throw new Exception("Um ou mais assentos selecionados ficaram indisponíveis.");

                seat.Reserve();
                var reservation = new Reservation(seatReq.SeatId, request.UserId, seatReq.Price, seatReq.TicketType, TimeSpan.FromMinutes(10));
                
                await _seatRepository.UpdateAsync(seat);
                await _reservationRepository.AddAsync(reservation);
                reservationIds.Add(reservation.Id);

                // 3. Notificar pagamento para cada assento (ou total)
                await _publishEndpoint.Publish<ReservationCreatedEvent>(new {
                    ReservationId = reservation.Id,
                    Amount = seatReq.Price,
                    UserId = request.UserId
                });
            }

            return reservationIds;
        }
        catch
        {
            // Opcional: Implementar compensação (rollback manual) aqui se necessário
            throw;
        }
        finally
        {
            foreach (var l in locks) await _lockService.ReleaseLockAsync(l.key, l.value);
        }
    }
}
