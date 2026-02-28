using Dapper;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Booking.Application.Features.Seats.Queries.GetAvailableSeats;

public class GetAvailableSeatsQueryHandler : IRequestHandler<GetAvailableSeatsQuery, IEnumerable<AvailableSeatDto>>
{
    private readonly string _connectionString;

    public GetAvailableSeatsQueryHandler(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
                           ?? throw new ArgumentNullException("Connection string not found");
    }

    public async Task<IEnumerable<AvailableSeatDto>> Handle(GetAvailableSeatsQuery request, CancellationToken cancellationToken)
    {
        using var connection = new SqlConnection(_connectionString);
        
        // Fast query using Dapper (CQRS: Read side)
        const string sql = "SELECT Id, Row, Number FROM Seats WHERE ShowId = @ShowId AND IsReserved = 0";
        
        return await connection.QueryAsync<AvailableSeatDto>(sql, new { request.ShowId });
    }
}
