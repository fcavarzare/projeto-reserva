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
        return Ok(movie);
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
