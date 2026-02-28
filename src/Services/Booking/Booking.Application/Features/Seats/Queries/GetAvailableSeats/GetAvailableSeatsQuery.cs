using MediatR;

namespace Booking.Application.Features.Seats.Queries.GetAvailableSeats;

public record GetAvailableSeatsQuery(Guid ShowId) : IRequest<IEnumerable<AvailableSeatDto>>;

public record AvailableSeatDto(Guid Id, string Row, int Number);
