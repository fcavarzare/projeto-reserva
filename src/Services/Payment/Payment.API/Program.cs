using MassTransit;
using Payment.API.Consumers;

var builder = WebApplication.CreateBuilder(args);

// Configurar MassTransit (RabbitMQ)
builder.Services.AddMassTransit(x =>
{
    // Registrar o Consumidor
    x.AddConsumer<ReservationCreatedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration.GetConnectionString("RabbitMQ") ?? "rabbitmq", "/", h => {
            h.Username("guest");
            h.Password("guest");
        });

        cfg.ReceiveEndpoint("reservation-created-queue", e =>
        {
            e.ConfigureConsumer<ReservationCreatedConsumer>(context);
        });
    });
});

builder.Services.AddControllers();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
