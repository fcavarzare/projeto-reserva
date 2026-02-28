namespace Booking.Domain.Entities;

public class Theater
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public int Rows { get; private set; }
    public int SeatsPerRow { get; private set; }

    private Theater() { }

    public Theater(string name, int rows, int seatsPerRow)
    {
        Id = Guid.NewGuid();
        Name = name;
        Rows = rows;
        SeatsPerRow = seatsPerRow;
    }
}
