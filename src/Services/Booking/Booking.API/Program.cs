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
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

using MassTransit;
using Booking.Application.Contracts;
using Booking.API.Consumers;

var builder = WebApplication.CreateBuilder(args);

// 1. Database
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

// 3. Autenticação PERMISSIVA para Depuração
builder.Services.AddAuthentication("GatewayAuth")
    .AddScheme<AuthenticationSchemeOptions, GatewayAuthHandler>("GatewayAuth", null);

builder.Services.AddAuthorization();

// 4. DI
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
    .AddRedis(redisConnectionString, name: "ready");

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

app.UseCors("AllowAll");
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
    context.Database.EnsureCreated();
}

app.MapControllers();

// Health Checks
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = (check) => check.Tags.Contains("live") }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = (check) => check.Tags.Contains("ready") }).AllowAnonymous();

app.Run();

// Handler com LOGS INTENSOS
public class GatewayAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly ILogger<GatewayAuthHandler> _logger;
    public GatewayAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, System.Text.Encodings.Web.UrlEncoder encoder) 
        : base(options, logger, encoder) {
            _logger = logger.CreateLogger<GatewayAuthHandler>();
        }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        _logger.LogInformation("--- NOVA REQUISIÇÃO: {Method} {Path} ---", Request.Method, Request.Path);
        
        foreach (var header in Request.Headers)
        {
            _logger.LogInformation("Header: {Key} = {Value}", header.Key, header.Value);
        }

        if (Request.Headers.TryGetValue("X-User-Id", out var userId))
        {
            _logger.LogInformation("SUCESSO: Usuário {User} encontrado no header.", userId);
            var claims = new[] { new Claim(ClaimTypes.Name, userId.ToString()) };
            var identity = new ClaimsIdentity(claims, "GatewayAuth");
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), "GatewayAuth")));
        }
        
        // MODO PÂNICO: Se for GET /api/movies, deixa passar como 'Guest' para testar o banco
        if (Request.Path.StartsWithSegments("/api/movies") && Request.Method == "GET")
        {
            _logger.LogWarning("AVISO: Header ausente, mas liberando GET /api/movies como Guest para teste.");
            var claims = new[] { new Claim(ClaimTypes.Name, "Guest") };
            var identity = new ClaimsIdentity(claims, "GatewayAuth");
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), "GatewayAuth")));
        }

        _logger.LogError("FALHA: Header X-User-Id não encontrado em rota protegida.");
        return Task.FromResult(AuthenticateResult.Fail("Header ausente."));
    }
}
