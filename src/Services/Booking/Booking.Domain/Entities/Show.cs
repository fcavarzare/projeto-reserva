namespace Booking.Domain.Entities;

public class Show
{
    public Guid Id { get; private set; }
    public Guid MovieId { get; private set; }
    public Guid TheaterId { get; private set; }
    public DateTime Date { get; private set; }

    private Show() { }

    public Show(Guid movieId, Guid theaterId, DateTime date)
    {
        Id = Guid.NewGuid();
        MovieId = movieId;
        TheaterId = theaterId;
        Date = date;
    }
}
