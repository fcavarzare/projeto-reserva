using Booking.Domain.Enums;

namespace Booking.Domain.Entities;

public class Reservation
{
    public Guid Id { get; private set; }
    public Guid SeatId { get; private set; }
    public string UserId { get; private set; }
    public ReservationStatus Status { get; private set; }
    public decimal Price { get; private set; }
    public string TicketType { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ExpiresAt { get; private set; }

    private Reservation() { }

    public Reservation(Guid seatId, string userId, decimal price, string ticketType, TimeSpan ttl)
    {
        Id = Guid.NewGuid();
        SeatId = seatId;
        UserId = userId;
        Price = price;
        TicketType = ticketType;
        Status = ReservationStatus.Pending;
        CreatedAt = DateTime.UtcNow;
        ExpiresAt = CreatedAt.Add(ttl);
    }

    public void Confirm()
    {
        if (Status != ReservationStatus.Pending)
            throw new Exception("Only pending reservations can be confirmed.");
        
        Status = ReservationStatus.Confirmed;
    }

    public void Expire()
    {
        if (Status == ReservationStatus.Pending)
            Status = ReservationStatus.Expired;
    }
}
