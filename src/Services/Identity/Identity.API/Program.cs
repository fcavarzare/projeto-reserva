using Identity.API.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Principal;

var builder = WebApplication.CreateBuilder(args);

// 1. Database para Identity
var connectionString = builder.Configuration.GetConnectionString("IdentityDbContext");
builder.Services.AddDbContext<IdentityDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddControllers();

// 2. Health Checks
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: new[] { "live" })
    .AddSqlServer(connectionString!, name: "sqlserver", tags: new[] { "ready" });

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

// Garantir que o banco seja criado
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    db.Database.EnsureCreated();
}

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

// Endpoints para o Kubernetes
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions {
    Predicate = (check) => check.Tags.Contains("live")
});
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions {
    Predicate = (check) => check.Tags.Contains("ready")
});

app.Run();
