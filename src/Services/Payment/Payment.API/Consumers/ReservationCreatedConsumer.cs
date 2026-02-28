using MassTransit;
using Booking.Application.Contracts; // Na prática, esses contratos estariam num projeto compartilhado

namespace Payment.API.Consumers;

public class ReservationCreatedConsumer : IConsumer<ReservationCreatedEvent>
{
    private readonly ILogger<ReservationCreatedConsumer> _logger;

    public ReservationCreatedConsumer(ILogger<ReservationCreatedConsumer> logger)
    {
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ReservationCreatedEvent> context)
    {
        var message = context.Message;
        _logger.LogInformation("Processando pagamento da reserva {ReservationId} no valor de {Amount}", 
            message.ReservationId, message.Amount);

        // Simulando processamento de pagamento
        await Task.Delay(2000);

        _logger.LogInformation("Pagamento da reserva {ReservationId} APROVADO!", message.ReservationId);

        // Aqui, o Payment.API publicaria um novo evento "PaymentApprovedEvent" 
        // para o Booking.API confirmar a reserva definitivamente.
    }
}
