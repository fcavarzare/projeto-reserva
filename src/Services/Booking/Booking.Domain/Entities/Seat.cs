namespace Booking.Domain.Entities;

public class Seat
{
    public Guid Id { get; private set; }
    public Guid ShowId { get; private set; }
    public string Row { get; private set; }
    public int Number { get; private set; }
    public bool IsReserved { get; private set; }

    private Seat() { }

    public Seat(Guid showId, string row, int number)
    {
        Id = Guid.NewGuid();
        ShowId = showId;
        Row = row;
        Number = number;
        IsReserved = false;
    }

    public void Reserve()
    {
        if (IsReserved)
            throw new Exception("Seat already reserved");
        IsReserved = true;
    }

    public void Release()
    {
        IsReserved = false;
    }
}
