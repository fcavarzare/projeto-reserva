using MassTransit;
using Payment.API.Consumers;
using System.Security.Claims;
using System.Security.Principal;

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
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: new[] { "live" });

var app = builder.Build();

// Middleware para "confiar" no usuário que o Gateway autenticou
app.Use(async (context, next) =>
{
    if (context.Request.Headers.TryGetValue("X-User-Id", out var userId))
    {
        var identity = new GenericIdentity(userId!);
        context.User = new ClaimsPrincipal(identity);
    }
    await next();
});

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions {
    Predicate = (check) => check.Tags.Contains("live")
});

app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions {
    Predicate = (check) => check.Tags.Contains("live")
});

app.Run();
