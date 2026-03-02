using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Yarp.ReverseProxy.Transforms;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// 1. JWT no Gateway
var secretKey = "O_Segredo_Mais_Seguro_Do_Mundo_2026_!@#";
var key = Encoding.ASCII.GetBytes(secretKey);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(x =>
    {
        x.RequireHttpsMetadata = false;
        x.SaveToken = true;
        x.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = false,
            ValidateAudience = false,
            NameClaimType = ClaimTypes.Name
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AuthenticatedUser", policy => policy.RequireAuthenticatedUser());
});

// 2. YARP com injeção de Header SEM UNDERLINE
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(builderContext =>
    {
        builderContext.AddRequestTransform(async transformContext =>
        {
            var user = transformContext.HttpContext.User;
            if (user.Identity?.IsAuthenticated == true)
            {
                var name = user.FindFirst(ClaimTypes.Name)?.Value 
                        ?? user.FindFirst("unique_name")?.Value 
                        ?? user.Identity.Name;

                if (!string.IsNullOrEmpty(name))
                {
                    // Remove Underline para evitar ser deletado por proxies
                    transformContext.ProxyRequest.Headers.Remove("X-UserId");
                    transformContext.ProxyRequest.Headers.Add("X-UserId", name);
                }
            }
        });
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

app.MapReverseProxy();

app.Run();
