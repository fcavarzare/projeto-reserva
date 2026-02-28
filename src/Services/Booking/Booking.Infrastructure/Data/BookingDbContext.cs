using Booking.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Booking.Infrastructure.Data;

public class BookingDbContext : DbContext
{
    public BookingDbContext(DbContextOptions<BookingDbContext> options) : base(options) { }

    public DbSet<Movie> Movies { get; set; }
    public DbSet<Theater> Theaters { get; set; }
    public DbSet<Show> Shows { get; set; }
    public DbSet<Seat> Seats { get; set; }
    public DbSet<Reservation> Reservations { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Movie>().HasKey(x => x.Id);
        modelBuilder.Entity<Theater>().HasKey(x => x.Id);
        modelBuilder.Entity<Theater>().Property(x => x.Location).IsRequired().HasMaxLength(10).HasDefaultValue("SP");
        modelBuilder.Entity<Show>().HasKey(x => x.Id);
        modelBuilder.Entity<Seat>().HasKey(x => x.Id);
        modelBuilder.Entity<Reservation>().HasKey(x => x.Id);

        modelBuilder.Entity<Seat>().Property(x => x.Row).IsRequired().HasMaxLength(5);
        modelBuilder.Entity<Seat>().Property<byte[]>("RowVersion").IsRowVersion();
    }
}
