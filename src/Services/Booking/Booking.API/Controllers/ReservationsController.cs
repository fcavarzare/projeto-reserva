using Booking.Application.Features.Reservations.Commands.CreateReservation;
using Booking.Infrastructure.Data;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json;

namespace Booking.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Microsoft.AspNetCore.Authorization.Authorize]
public class ReservationsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IConnectionMultiplexer _redis;
    private readonly BookingDbContext _context;
    private const string SEATS_CACHE_KEY = "view_model:seats:";

    public ReservationsController(IMediator mediator, IConnectionMultiplexer redis, BookingDbContext context)
    {
        _mediator = mediator;
        _redis = redis;
        _context = context;
    }

    [HttpGet("seats/{sessionId}")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<IActionResult> GetSeats(Guid sessionId)
    {
        var cacheKey = SEATS_CACHE_KEY + sessionId;
        var db = _redis.GetDatabase();
        
        var cached = await db.StringGetAsync(cacheKey);
        if (cached.HasValue) return Ok(JsonDocument.Parse(cached.ToString()));

        var seats = await _context.Seats
            .Where(s => s.ShowId == sessionId)
            .AsNoTracking()
            .OrderBy(s => s.Row).ThenBy(s => s.Number)
            .ToListAsync();
        
        if (seats.Any())
        {
            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            await db.StringSetAsync(cacheKey, JsonSerializer.Serialize(seats, jsonOptions), TimeSpan.FromMinutes(5));
        }

        return Ok(seats);
    }

    [HttpPost("seed")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<IActionResult> Seed()
    {
        _context.Reservations.RemoveRange(_context.Reservations);
        _context.Seats.RemoveRange(_context.Seats);
        _context.Shows.RemoveRange(_context.Shows);
        _context.Theaters.RemoveRange(_context.Theaters);
        _context.Movies.RemoveRange(_context.Movies);
        await _context.SaveChangesAsync();

        // 1. Criar Filmes com links da Amazon (CDN IMDb) - Mais estáveis
        var m1 = new Booking.Domain.Entities.Movie(
            "Batman: O Cavaleiro das Trevas", 
            "https://m.media-amazon.com/images/M/MV5BMTMxNTMwODM0NF5BMl5BanBnXkFtZTcwODAyMTk2Mw@@._V1_SX300.jpg", 
            "O Coringa causa o caos em Gotham City e Batman precisa detê-lo para salvar a cidade.");
        
        var m2 = new Booking.Domain.Entities.Movie(
            "Interestelar", 
            "https://m.media-amazon.com/images/M/MV5BZjdkOTU3MDktN2IxOS00OGEyLWFmMjktY2FiMmZkNWIyODZiXkFtZTgwMTkzOTUyMDI@._V1_SX300.jpg", 
            "Uma equipe de exploradores viaja através de um buraco de minhoca no espaço para salvar a humanidade.");
        
        var m3 = new Booking.Domain.Entities.Movie(
            "Matrix", 
            "https://m.media-amazon.com/images/M/MV5BNzQzOTk3OTAtNDQ0Zi00ZTVkLWI0MTEtMDllZjNkYzNjNTc4L2ltYWdlXkFtZTgwMjAxMTkwMzE@._V1_SX300.jpg", 
            "Um hacker descobre que a realidade em que vive é uma simulação controlada por máquinas.");
        
        await _context.Movies.AddRangeAsync(m1, m2, m3);

        // 2. Criar Salas
        var t1 = new Booking.Domain.Entities.Theater("Sala IMAX 01", 5, 10);
        var t2 = new Booking.Domain.Entities.Theater("Sala VIP Premium", 3, 6);
        await _context.Theaters.AddRangeAsync(t1, t2);
        await _context.SaveChangesAsync();

        // 3. Criar Sessões
        var s1 = new Booking.Domain.Entities.Show(m1.Id, t1.Id, DateTime.UtcNow.AddHours(2));
        var s2 = new Booking.Domain.Entities.Show(m2.Id, t1.Id, DateTime.UtcNow.AddHours(5));
        var s3 = new Booking.Domain.Entities.Show(m3.Id, t2.Id, DateTime.UtcNow.AddHours(3));
        await _context.Shows.AddRangeAsync(s1, s2, s3);
        await _context.SaveChangesAsync();

        // 4. Criar Assentos
        async Task CreateSeats(Booking.Domain.Entities.Show session, Booking.Domain.Entities.Theater theater)
        {
            var rows = new[] { "A", "B", "C", "D", "E", "F" };
            for (int r = 0; r < theater.Rows; r++)
                for (int n = 1; n <= theater.SeatsPerRow; n++)
                    await _context.Seats.AddAsync(new Booking.Domain.Entities.Seat(session.Id, rows[r], n));
        }

        await CreateSeats(s1, t1);
        await CreateSeats(s2, t1);
        await CreateSeats(s3, t2);
        await _context.SaveChangesAsync();

        return Ok(new { message = "O catálogo de cinema foi atualizado com sucesso!" });
    }

    [HttpPost]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<IActionResult> Create([FromBody] CreateReservationCommand command)
    {
        try
        {
            var reservationId = await _mediator.Send(command);
            var seat = await _context.Seats.FindAsync(command.SeatId);
            if (seat != null) await _redis.GetDatabase().KeyDeleteAsync(SEATS_CACHE_KEY + seat.ShowId);
            return Ok(new { id = reservationId });
        }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{seatId}")]
    public async Task<IActionResult> Cancel(Guid seatId)
    {
        var seat = await _context.Seats.FindAsync(seatId);
        if (seat != null)
        {
            seat.Release();
            var pending = _context.Reservations.Where(r => r.SeatId == seatId && r.Status != Booking.Domain.Enums.ReservationStatus.Confirmed);
            _context.Reservations.RemoveRange(pending);
            await _context.SaveChangesAsync();
            await _redis.GetDatabase().KeyDeleteAsync(SEATS_CACHE_KEY + seat.ShowId);
            return Ok(new { message = "A reserva foi cancelada com sucesso." });
        }
        return NotFound();
    }

    [HttpGet("debug/redis")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous] // Permitir ver sem login para facilitar o debug
    public async Task<IActionResult> GetRedisData()
    {
        var server = _redis.GetServer(_redis.GetEndPoints()[0]);
        var db = _redis.GetDatabase();
        var keys = server.Keys().ToList();
        
        var debugData = new System.Collections.Generic.Dictionary<string, string>();
        foreach (var key in keys)
        {
            var value = await db.StringGetAsync(key);
            debugData.Add(key.ToString(), value.ToString());
        }

        return Ok(new { 
            totalKeys = keys.Count,
            data = debugData 
        });
    }
}
