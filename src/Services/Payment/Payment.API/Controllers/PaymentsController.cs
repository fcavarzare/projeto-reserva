using Microsoft.AspNetCore.Mvc;

namespace Payment.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    [HttpPost("process")]
    public async Task<IActionResult> Process([FromBody] PaymentRequest request)
    {
        // 1. Simular tempo de processamento real (Rede/Banco)
        // Isso fará o Dashboard mostrar o estado de "Processando"
        await Task.Delay(1000); 

        // 2. Simular taxa de sucesso de 80% (20% de falha para teste visual)
        var success = new Random().Next(0, 100) > 20; 
        
        if (success)
        {
            return Ok(new { transactionId = Guid.NewGuid(), status = "Success" });
        }
        
        return BadRequest(new { message = "Pagamento recusado pela operadora." });
    }
}

public record PaymentRequest(Guid ReservationId, decimal Amount);
