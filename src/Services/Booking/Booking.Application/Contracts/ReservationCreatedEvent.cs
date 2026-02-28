namespace Booking.Application.Contracts;

public interface ReservationCreatedEvent
{
    Guid ReservationId { get; }
    decimal Amount { get; }
    Guid UserId { get; }
}
