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
            "https://m.media-amazon.com/images/M/MV5BZjdkOTU3MDktN2IxOS00OGEyLWFmMjktY2FiMmZkNWIyODZiXkEyXkFqcGdeQXVyMTMxODk2OTU@._V1_SX300.jpg", 
            "Uma equipe de exploradores viaja através de um buraco de minhoca no espaço para salvar a humanidade.");
        
        var m3 = new Booking.Domain.Entities.Movie(
            "Matrix", 
            "https://m.media-amazon.com/images/M/MV5BNzQzOTk3OTAtNDQ0Zi00ZTVkLWI0MTEtMDllZjNkYzNjNTc4L2ltYWdlXkEyXkFqcGdeQXVyNjU0OTQ0OTY@._V1_SX300.jpg", 
            "Um hacker descobre que a realidade em que vive é uma simulação controlada por máquinas.");
        
        await _context.Movies.AddRangeAsync(m1, m2, m3);

        // 2. Criar Salas em 3 Estados
        var theaters = new System.Collections.Generic.List<Booking.Domain.Entities.Theater>
        {
            new Booking.Domain.Entities.Theater("CineMark SP - Paulista", 8, 12, "SP"),
            new Booking.Domain.Entities.Theater("CineMark SP - Eldorado", 6, 10, "SP"),
            new Booking.Domain.Entities.Theater("UCI RJ - New York City Center", 10, 15, "RJ"),
            new Booking.Domain.Entities.Theater("Cinepolis RJ - Lagoon", 5, 8, "RJ"),
            new Booking.Domain.Entities.Theater("Cineart MG - Diamond Mall", 7, 12, "MG"),
            new Booking.Domain.Entities.Theater("Net Cine MG - Estação", 4, 10, "MG")
        };
        await _context.Theaters.AddRangeAsync(theaters);
        await _context.SaveChangesAsync();

        // 3. Criar Sessões (Múltiplos horários)
        var shows = new System.Collections.Generic.List<Booking.Domain.Entities.Show>();
        var movies = new[] { m1, m2, m3 };
        var hours = new[] { 14, 17, 20, 22 };

        foreach (var theater in theaters)
        {
            foreach (var movie in movies)
            {
                foreach (var hour in hours)
                {
                    // Cada cinema tem sessões dos 3 filmes em 4 horários diferentes
                    shows.Add(new Booking.Domain.Entities.Show(movie.Id, theater.Id, DateTime.Today.AddHours(hour)));
                }
            }
        }
        await _context.Shows.AddRangeAsync(shows);
        await _context.SaveChangesAsync();

        // 4. Criar Assentos (Massivamente)
        async Task CreateSeats(Booking.Domain.Entities.Show session, Booking.Domain.Entities.Theater theater)
        {
            var rows = new[] { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J" };
            var sessionSeats = new System.Collections.Generic.List<Booking.Domain.Entities.Seat>();
            for (int r = 0; r < theater.Rows; r++)
            {
                for (int n = 1; n <= theater.SeatsPerRow; n++)
                {
                    sessionSeats.Add(new Booking.Domain.Entities.Seat(session.Id, rows[r], n));
                }
            }
            await _context.Seats.AddRangeAsync(sessionSeats);
        }

        foreach (var show in shows)
        {
            var theater = theaters.First(t => t.Id == show.TheaterId);
            await CreateSeats(show, theater);
        }
        
        try {
            await _context.SaveChangesAsync();
        } catch (Exception ex) {
            _context.ChangeTracker.Clear();
            return BadRequest(new { message = "Erro ao persistir catálogo expandido.", detail = ex.Message });
        }

        return Ok(new { 
            message = "Catálogo Nacional (SP, RJ, MG) atualizado com sucesso!",
            totalTheaters = theaters.Count,
            totalShows = shows.Count
        });
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
