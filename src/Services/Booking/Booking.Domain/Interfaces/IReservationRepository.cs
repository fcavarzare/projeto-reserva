using Booking.Domain.Entities;

namespace Booking.Domain.Interfaces;

public interface IReservationRepository
{
    Task<Reservation?> GetByIdAsync(Guid id);
    Task<IEnumerable<Reservation>> GetPendingExpiredReservationsAsync();
    Task AddAsync(Reservation reservation);
    Task UpdateAsync(Reservation reservation);
}
