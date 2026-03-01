using Booking.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Booking.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Microsoft.AspNetCore.Authorization.Authorize]
public class MoviesController : ControllerBase
{
    private readonly BookingDbContext _context;

    public MoviesController(BookingDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetMovies()
    {
        var movies = await _context.Movies.ToListAsync();
        return Ok(movies);
    }

    [HttpPost]
    public async Task<IActionResult> CreateMovie([FromBody] MovieRequest request)
    {
        var movie = new Booking.Domain.Entities.Movie(request.Title, request.PosterUrl, request.Description);
        _context.Movies.Add(movie);
        await _context.SaveChangesAsync();

        var theaters = await _context.Theaters.ToListAsync();
        var hours = new[] { 14, 18, 21 };
        var newShows = new List<Booking.Domain.Entities.Show>();
        foreach (var theater in theaters)
        {
            foreach (var hour in hours)
            {
                newShows.Add(new Booking.Domain.Entities.Show(movie.Id, theater.Id, DateTime.Today.AddHours(hour)));
            }
        }
        await _context.Shows.AddRangeAsync(newShows);
        await _context.SaveChangesAsync();

        var allSeats = new List<Booking.Domain.Entities.Seat>();
        var rows = new[] { "A", "B", "C", "D", "E", "F", "G", "H" };
        foreach (var show in newShows)
        {
            var theater = theaters.First(t => t.Id == show.TheaterId);
            for (int r = 0; r < theater.Rows; r++)
            {
                for (int n = 1; n <= theater.SeatsPerRow; n++)
                {
                    var type = Booking.Domain.Entities.SeatType.Normal;
                    if (r == 0 && (n == 1 || n == theater.SeatsPerRow)) type = Booking.Domain.Entities.SeatType.Wheelchair;
                    allSeats.Add(new Booking.Domain.Entities.Seat(show.Id, rows[r], n, type));
                }
            }
        }
        await _context.Seats.AddRangeAsync(allSeats);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Filme e sessões criados!", movie });
    }

    public record MovieRequest(string Title, string PosterUrl, string Description);

    [HttpGet("{movieId}/sessions")]
    public async Task<IActionResult> GetSessions(Guid movieId)
    {
        var sessions = await _context.Shows
            .Where(s => s.MovieId == movieId)
            .Select(s => new {
                s.Id,
                s.Date,
                TheaterName = _context.Theaters.Where(t => t.Id == s.TheaterId).Select(t => t.Name).FirstOrDefault(),
                Location = _context.Theaters.Where(t => t.Id == s.TheaterId).Select(t => t.Location).FirstOrDefault()
            })
            .ToListAsync();
        return Ok(sessions);
    }
}
