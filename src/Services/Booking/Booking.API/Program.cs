using Booking.Application.Features.Reservations.Commands.CreateReservation;
using Booking.Application.Interfaces;
using Booking.Domain.Interfaces;
using Booking.Infrastructure.Data;
using Booking.Infrastructure.ExternalServices;
using Booking.Infrastructure.Repositories;
using Booking.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.Text;
using System.Text.Json;

using MassTransit;
using Booking.Application.Contracts;

var builder = WebApplication.CreateBuilder(args);

// 1. Database (SQL Server)
builder.Services.AddDbContext<BookingDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Redis (Distributed Lock)
var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "booking-redis:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(sp => 
{
    var config = ConfigurationOptions.Parse(redisConnectionString);
    config.ConnectRetry = 5;
    config.ConnectTimeout = 10000;
    config.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(config);
});

// 2.1 MassTransit (RabbitMQ)
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration.GetConnectionString("RabbitMQ") ?? "booking-rabbitmq", "/", h => {
            h.Username("guest");
            h.Password("guest");
        });
    });
});

// 3. Autenticacao JWT
var secretKey = "O_Segredo_Mais_Seguro_Do_Mundo_2026_!@#";
var key = Encoding.ASCII.GetBytes(secretKey);

builder.Services.AddAuthentication(x =>
{
    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(x =>
{
    x.RequireHttpsMetadata = false;
    x.SaveToken = true;
    x.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false,
        ValidateAudience = false
    };
});

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
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Garantir que o banco e as novas tabelas (Movies, Theaters) existam
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
    // Isso vai criar as tabelas que faltam sem apagar os dados existentes 
    // (ou criar tudo do zero se o banco for novo)
    context.Database.EnsureCreated();
}

app.UseCors("AllowAll");
app.UseHttpsRedirection();
app.UseAuthentication(); // Ordem importa!
app.UseAuthorization();
app.MapControllers();

app.Run();
