namespace Booking.Application.Interfaces;

public interface IPaymentService
{
    Task<bool> ProcessPaymentAsync(Guid reservationId, decimal amount);
}
