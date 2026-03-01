using Identity.API.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProfileController : ControllerBase
{
    private readonly IdentityDbContext _context;

    public ProfileController(IdentityDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetProfile()
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null) return NotFound();

        return Ok(new {
            user.Username,
            user.Email,
            user.AvatarUrl,
            user.CreatedAt
        });
    }

    [HttpPut]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null) return NotFound();

        user.AvatarUrl = request.AvatarUrl;
        user.Email = request.Email ?? user.Email;
        
        await _context.SaveChangesAsync();
        return Ok(new { message = "Perfil atualizado com sucesso!" });
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null) return NotFound();

        // No mundo real, validar a senha antiga (request.OldPassword) com hash
        if (user.PasswordHash != request.OldPassword) 
            return BadRequest(new { message = "Senha atual incorreta." });

        user.PasswordHash = request.NewPassword;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Senha alterada com sucesso!" });
    }
}

public record UpdateProfileRequest(string? AvatarUrl, string? Email);
public record ChangePasswordRequest(string OldPassword, string NewPassword);
