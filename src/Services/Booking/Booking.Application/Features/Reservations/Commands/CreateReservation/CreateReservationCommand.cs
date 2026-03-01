using MediatR;

namespace Booking.Application.Features.Reservations.Commands.CreateReservation;

public record SeatReservationRequest(Guid SeatId, decimal Price, string TicketType);

public record CreateReservationCommand(List<SeatReservationRequest> Seats, string UserId) : IRequest<List<Guid>>;
