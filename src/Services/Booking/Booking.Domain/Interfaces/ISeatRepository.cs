using Booking.Domain.Entities;

namespace Booking.Domain.Interfaces;

public interface ISeatRepository
{
    Task<Seat?> GetByIdAsync(Guid id);
    Task<IEnumerable<Seat>> GetSeatsByShowIdAsync(Guid showId);
    Task UpdateAsync(Seat seat);
    Task<bool> IsSeatAvailableAsync(Guid seatId);
    Task<bool> ReserveAtomicAsync(Guid seatId);
}
