using System.Net.Http.Json;
using Booking.Application.Interfaces;

namespace Booking.Infrastructure.ExternalServices;

public class PaymentService : IPaymentService
{
    private readonly HttpClient _httpClient;

    public PaymentService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> ProcessPaymentAsync(Guid reservationId, decimal amount)
    {
        var response = await _httpClient.PostAsJsonAsync("api/payments/process", new { reservationId, amount });
        return response.IsSuccessStatusCode;
    }
}
