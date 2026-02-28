namespace Booking.Domain.Entities;

public enum SeatType { Normal, Wheelchair, Companion }

public class Seat
{
    public Guid Id { get; private set; }
    public Guid ShowId { get; private set; }
    public string Row { get; private set; }
    public int Number { get; private set; }
    public bool IsReserved { get; private set; }
    public SeatType Type { get; private set; }

    private Seat() { }

    public Seat(Guid showId, string row, int number, SeatType type = SeatType.Normal)
    {
        Id = Guid.NewGuid();
        ShowId = showId;
        Row = row;
        Number = number;
        IsReserved = false;
        Type = type;
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
