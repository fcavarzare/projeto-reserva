using Booking.Domain.Entities;
using Booking.Domain.Enums;
using Booking.Domain.Interfaces;
using Booking.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Booking.Infrastructure.Repositories;

public class ReservationRepository : IReservationRepository
{
    private readonly BookingDbContext _context;

    public ReservationRepository(BookingDbContext context)
    {
        _context = context;
    }

    public async Task<Reservation?> GetByIdAsync(Guid id)
    {
        return await _context.Reservations.FindAsync(id);
    }

    public async Task<IEnumerable<Reservation>> GetPendingExpiredReservationsAsync()
    {
        return await _context.Reservations
            .Where(r => r.Status == ReservationStatus.Pending && r.ExpiresAt < DateTime.UtcNow)
            .ToListAsync();
    }

    public async Task AddAsync(Reservation reservation)
    {
        await _context.Reservations.AddAsync(reservation);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Reservation reservation)
    {
        _context.Reservations.Update(reservation);
        await _context.SaveChangesAsync();
    }
}
