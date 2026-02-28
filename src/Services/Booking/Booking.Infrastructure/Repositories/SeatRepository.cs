using Booking.Domain.Entities;
using Booking.Domain.Interfaces;
using Booking.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Booking.Infrastructure.Repositories;

public class SeatRepository : ISeatRepository
{
    private readonly BookingDbContext _context;

    public SeatRepository(BookingDbContext context)
    {
        _context = context;
    }

    public async Task<Seat?> GetByIdAsync(Guid id)
    {
        return await _context.Seats.FindAsync(id);
    }

    public async Task<IEnumerable<Seat>> GetSeatsByShowIdAsync(Guid showId)
    {
        return await _context.Seats.Where(s => s.ShowId == showId).ToListAsync();
    }

    public async Task UpdateAsync(Seat seat)
    {
        _context.Seats.Update(seat);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> ReserveAtomicAsync(Guid seatId)
    {
        using var connection = new Microsoft.Data.SqlClient.SqlConnection(_context.Database.GetConnectionString());
        const string sql = "UPDATE Seats SET IsReserved = 1 WHERE Id = @Id AND IsReserved = 0";
        var affectedRows = await Dapper.SqlMapper.ExecuteAsync(connection, sql, new { Id = seatId });
        return affectedRows > 0;
    }

    public async Task<bool> IsSeatAvailableAsync(Guid seatId)
    {
        var seat = await _context.Seats.FindAsync(seatId);
        return seat != null && !seat.IsReserved;
    }
}
