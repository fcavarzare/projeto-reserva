namespace Booking.Application.Contracts;

public interface PaymentProcessedEvent
{
    Guid ReservationId { get; }
    bool Success { get; }
    string Message { get; }
}
