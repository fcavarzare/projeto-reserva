using Booking.Application.Features.Reservations.Commands.CreateReservation;
using Booking.Application.Interfaces;
using Booking.Domain.Interfaces;
using Booking.Infrastructure.Data;
using Booking.Infrastructure.ExternalServices;
using Booking.Infrastructure.Repositories;
using Booking.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json;
using System.Security.Claims;
using System.Security.Principal;

using MassTransit;
using Booking.Application.Contracts;
using Booking.API.Consumers;

var builder = WebApplication.CreateBuilder(args);

// 1. Database (SQL Server)
builder.Services.AddDbContext<BookingDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Redis
var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "booking-redis:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(sp => 
{
    var config = ConfigurationOptions.Parse(redisConnectionString);
    config.ConnectRetry = 5;
    config.ConnectTimeout = 10000;
    config.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(config);
});

// 2.1 MassTransit
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<PaymentProcessedConsumer>();
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration.GetConnectionString("RabbitMQ") ?? "rabbitmq", "/", h => {
            h.Username("guest");
            h.Password("guest");
        });
        cfg.ReceiveEndpoint("payment-processed-queue", e => e.ConfigureConsumer<PaymentProcessedConsumer>(context));
    });
});

// 3. Autenticação agora é via Gateway (Lemos o Header X-User-Id)
builder.Services.AddAuthorization();

// 4. Dependency Injection
builder.Services.AddScoped<ISeatRepository, SeatRepository>();
builder.Services.AddScoped<IReservationRepository, ReservationRepository>();
builder.Services.AddScoped<IDistributedLockService, RedisDistributedLockService>();

builder.Services.AddHttpClient<IPaymentService, PaymentService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ExternalServices:PaymentApi"] ?? "http://localhost:5001");
});

builder.Services.AddMediatR(cfg => {
    cfg.RegisterServicesFromAssembly(typeof(CreateReservationCommand).Assembly);
});

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: new[] { "live" })
    .AddSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")!, name: "sqlserver", tags: new[] { "ready" })
    .AddRedis(redisConnectionString, name: "redis", tags: new[] { "ready" });

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

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

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
    context.Database.EnsureCreated();
}

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions {
    Predicate = (check) => check.Tags.Contains("live")
});
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions {
    Predicate = (check) => check.Tags.Contains("ready")
});

app.Run();
