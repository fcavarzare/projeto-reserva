using MassTransit;
using Booking.Application.Contracts; // Na prática, esses contratos estariam num projeto compartilhado

namespace Payment.API.Consumers;

public class ReservationCreatedConsumer : IConsumer<ReservationCreatedEvent>
{
    private readonly ILogger<ReservationCreatedConsumer> _logger;
    private readonly IPublishEndpoint _publishEndpoint;

    public ReservationCreatedConsumer(ILogger<ReservationCreatedConsumer> logger, IPublishEndpoint publishEndpoint)
    {
        _logger = logger;
        _publishEndpoint = publishEndpoint;
    }

    public async Task Consume(ConsumeContext<ReservationCreatedEvent> context)
    {
        var message = context.Message;
        _logger.LogInformation("Processando pagamento da reserva {ReservationId} no valor de {Amount}", 
            message.ReservationId, message.Amount);

        // Simulando processamento de pagamento
        await Task.Delay(2000);

        _logger.LogInformation("Pagamento da reserva {ReservationId} APROVADO!", message.ReservationId);

        // Publicar o resultado do pagamento para o Booking.API
        await _publishEndpoint.Publish<PaymentProcessedEvent>(new
        {
            ReservationId = message.ReservationId,
            Success = true,
            Message = "Pagamento processado com sucesso via RabbitMQ"
        });
    }
}
