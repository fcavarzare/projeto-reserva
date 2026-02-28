namespace Booking.Domain.Enums;

public enum ReservationStatus
{
    Pending,
    Confirmed,
    Cancelled,
    Expired,
    Selected // Novo: Assento segurado pelo usuário, mas sem pagamento ainda
}
