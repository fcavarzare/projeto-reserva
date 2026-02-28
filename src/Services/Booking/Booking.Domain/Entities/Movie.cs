namespace Booking.Domain.Entities;

public class Movie
{
    public Guid Id { get; private set; }
    public string Title { get; private set; }
    public string PosterUrl { get; private set; }
    public string Description { get; private set; }

    private Movie() { }

    public Movie(string title, string posterUrl, string description)
    {
        Id = Guid.NewGuid();
        Title = title;
        PosterUrl = posterUrl;
        Description = description;
    }
}
