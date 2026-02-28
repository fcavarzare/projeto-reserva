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

    [HttpGet("shows")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<IActionResult> GetShows()
    {
        var shows = await _context.Shows
            .ToListAsync();
        return Ok(shows);
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
            .Select(s => new {
                s.Id,
                s.ShowId,
                s.Row,
                s.Number,
                s.IsReserved,
                Type = s.Type.ToString(),
                Price = 45.00
            })
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

        var m1 = new Booking.Domain.Entities.Movie("Batman: O Cavaleiro das Trevas", "https://m.media-amazon.com/images/M/MV5BMTMxNTMwODM0NF5BMl5BanBnXkFtZTcwODAyMTk2Mw@@._V1_SX300.jpg", "O Coringa causa o caos em Gotham City.");
        var m2 = new Booking.Domain.Entities.Movie("Interestelar", "https://m.media-amazon.com/images/M/MV5BZjdkOTU3MDktN2IxOS00OGEyLWFmMjktY2FiMmZkNWIyODZiXkEyXkFqcGdeQXVyMTMxODk2OTU@._V1_SX300.jpg", "Exploradores viajam através de um buraco de minhoca.");
        var m3 = new Booking.Domain.Entities.Movie("Matrix", "https://m.media-amazon.com/images/M/MV5BNzQzOTk3OTAtNDQ0Zi00ZTVkLWI0MTEtMDllZjNkYzNjNTc4L2ltYWdlXkEyXkFqcGdeQXVyNjU0OTQ0OTY@._V1_SX300.jpg", "Um hacker descobre a realidade simulada.");
        await _context.Movies.AddRangeAsync(m1, m2, m3);

        var theaters = new System.Collections.Generic.List<Booking.Domain.Entities.Theater>
        {
            new Booking.Domain.Entities.Theater("Cinemark SP - Shopping D", 8, 12, "SP"),
            new Booking.Domain.Entities.Theater("Cinemark RJ - Botafogo", 6, 10, "RJ"),
            new Booking.Domain.Entities.Theater("Cinemark MG - BH Shopping", 7, 12, "MG")
        };
        await _context.Theaters.AddRangeAsync(theaters);
        await _context.SaveChangesAsync();

        var shows = new System.Collections.Generic.List<Booking.Domain.Entities.Show>();
        var movies = new[] { m1, m2, m3 };
        var hours = new[] { 15, 19, 22 };

        foreach (var theater in theaters)
            foreach (var movie in movies)
                foreach (var hour in hours)
                    shows.Add(new Booking.Domain.Entities.Show(movie.Id, theater.Id, DateTime.Today.AddHours(hour)));

        await _context.Shows.AddRangeAsync(shows);
        await _context.SaveChangesAsync();

        async Task CreateSeats(Booking.Domain.Entities.Show session, Booking.Domain.Entities.Theater theater)
        {
            var rows = new[] { "A", "B", "C", "D", "E", "F", "G", "H" };
            var sessionSeats = new System.Collections.Generic.List<Booking.Domain.Entities.Seat>();
            for (int r = 0; r < theater.Rows; r++)
            {
                for (int n = 1; n <= theater.SeatsPerRow; n++)
                {
                    var type = Booking.Domain.Entities.SeatType.Normal;
                    if (r == 0 && (n == 1 || n == theater.SeatsPerRow)) type = Booking.Domain.Entities.SeatType.Wheelchair;
                    else if (r == 0 && (n == 2 || n == theater.SeatsPerRow - 1)) type = Booking.Domain.Entities.SeatType.Companion;
                    sessionSeats.Add(new Booking.Domain.Entities.Seat(session.Id, rows[r], n, type));
                }
            }
            await _context.Seats.AddRangeAsync(sessionSeats);
        }

        foreach (var show in shows)
        {
            var theater = theaters.First(t => t.Id == show.TheaterId);
            await CreateSeats(show, theater);
        }
        await _context.SaveChangesAsync();

        return Ok(new { message = "Sistema Cinemark configurado com sucesso!" });
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
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
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
        return Ok(new { totalKeys = keys.Count, data = debugData });
    }
}
