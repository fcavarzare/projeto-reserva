using MediatR;

namespace Booking.Application.Features.Reservations.Commands.CreateReservation;

public record CreateReservationCommand(Guid SeatId, string UserId, decimal Price, string TicketType) : IRequest<Guid>;
