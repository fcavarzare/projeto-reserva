using Identity.API.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Identity.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IdentityDbContext _context;
    // IMPORTANTE: Esta chave deve ser IDENTICA à do ApiGateway
    private const string SecretKey = "O_Segredo_Mais_Seguro_Do_Mundo_2026_!@#";

    public AuthController(IdentityDbContext context)
    {
        _context = context;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] LoginRequest request)
    {
        if (await _context.Users.AnyAsync(u => u.Username == request.Username))
            return BadRequest(new { message = "Usuário já existe" });

        var user = new User 
        { 
            Id = Guid.NewGuid(), 
            Username = request.Username,
            Email = $"{request.Username}@cinema.local", // Email default para não quebrar o banco
            PasswordHash = request.Password, // No mundo real usaria Hash
            CreatedAt = DateTime.UtcNow
        };

        try {
            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Usuário criado com sucesso" });
        }
        catch (Exception ex) {
            return BadRequest(new { message = "Erro ao criar usuário: " + ex.Message });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => 
            u.Username == request.Username && u.PasswordHash == request.Password);

        if (user != null)
        {
            var token = GenerateJwtToken(user.Username);
            return Ok(new { token, user = user.Username });
        }

        return Unauthorized(new { message = "Usuário ou senha inválidos" });
    }

    private string GenerateJwtToken(string username)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(SecretKey);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[] { 
                new Claim(ClaimTypes.Name, username),
                new Claim("unique_name", username) // Dupla garantia
            }),
            Expires = DateTime.UtcNow.AddHours(2),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        return tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
    }
}

public record LoginRequest(string Username, string Password);
