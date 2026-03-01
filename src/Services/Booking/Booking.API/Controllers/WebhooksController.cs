using Booking.Application.Contracts;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;

namespace Booking.API.Controllers;

[ApiController]
[Route("api/webhooks")]
public class WebhooksController : ControllerBase
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WebhooksController> _logger;

    // Em produção, isso estaria no Kubernetes Secret
    private const string WebhookSecret = "Chave_Secreta_Do_Cinema_2026";

    public WebhooksController(IPublishEndpoint publishEndpoint, IConfiguration configuration, ILogger<WebhooksController> logger)
    {
        _publishEndpoint = publishEndpoint;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("payment-callback")]
    public async Task<IActionResult> PaymentCallback([FromHeader(Name = "X-Signature")] string incomingSignature)
    {
        // 1. Ler o corpo da requisição (JSON)
        using var reader = new StreamReader(Request.Body);
        var jsonPayload = await reader.ReadToEndAsync();

        // 2. Calcular o nosso Hash HMAC usando a chave secreta
        var computedSignature = GenerateHmacSignature(jsonPayload, WebhookSecret);

        // 3. PROVA REAL: Comparar as assinaturas
        if (incomingSignature != computedSignature)
        {
            _logger.LogWarning("❌ TENTATIVA DE FRAUDE: Assinatura inválida recebida!");
            return Unauthorized("Assinatura Inválida");
        }

        // 4. Se for válido, desserializar e jogar no RabbitMQ
        _logger.LogInformation("✅ Webhook verificado com sucesso. Encaminhando para o RabbitMQ...");
        
        // Simulação rápida de parse (num cenário real usaria System.Text.Json)
        // Aqui apenas publicamos o evento de sucesso para o fluxo que já criamos
        // Para este teste, vamos supor que o JSON tenha o ReservationId
        
        // await _publishEndpoint.Publish<PaymentProcessedEvent>(...);

        return Ok(new { message = "Webhook processado" });
    }

    private string GenerateHmacSignature(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(payloadBytes);
        return Convert.ToHexString(hashBytes).ToLower();
    }
}
