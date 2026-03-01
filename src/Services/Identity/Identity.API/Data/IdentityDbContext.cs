using Microsoft.EntityFrameworkCore;

namespace Identity.API.Data;

public class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; } // Tornar nulo para não quebrar usuários antigos
    public string PasswordHash { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }
    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasIndex(u => u.Username).IsUnique();
        // Index opcional no Email para não quebrar nada agora
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique().HasFilter("[Email] IS NOT NULL");
    }
}
