using MassTransit;
using Booking.Application.Contracts;
using Booking.Domain.Interfaces;

namespace Booking.API.Consumers;

public class PaymentProcessedConsumer : IConsumer<PaymentProcessedEvent>
{
    private readonly IReservationRepository _reservationRepository;
    private readonly ISeatRepository _seatRepository;
    private readonly ILogger<PaymentProcessedConsumer> _logger;

    public PaymentProcessedConsumer(
        IReservationRepository reservationRepository,
        ISeatRepository seatRepository,
        ILogger<PaymentProcessedConsumer> logger)
    {
        _reservationRepository = reservationRepository;
        _seatRepository = seatRepository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PaymentProcessedEvent> context)
    {
        var message = context.Message;
        _logger.LogInformation("Recebida resposta de pagamento para reserva {ReservationId}: {Success}", 
            message.ReservationId, message.Success);

        var reservation = await _reservationRepository.GetByIdAsync(message.ReservationId);

        if (reservation == null) return;

        if (message.Success)
        {
            _logger.LogInformation("Confirmando reserva {ReservationId}!", message.ReservationId);
            reservation.Confirm();
            await _reservationRepository.UpdateAsync(reservation);
        }
        else
        {
            _logger.LogWarning("Cancelando reserva {ReservationId} por falta de pagamento.", message.ReservationId);
            reservation.Expire();
            
            var seat = await _seatRepository.GetByIdAsync(reservation.SeatId);
            if (seat != null)
            {
                seat.Release();
                await _seatRepository.UpdateAsync(seat);
            }

            await _reservationRepository.UpdateAsync(reservation);
        }
    }
}
