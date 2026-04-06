using backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace backend.Infrastructure.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Hotel> Hotels => Set<Hotel>();
    public DbSet<HotelPrice> HotelPrices => Set<HotelPrice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Hotel>(entity =>
        {
            entity.Property(e => e.Name).HasMaxLength(250).IsRequired();
            entity.Property(e => e.City).HasMaxLength(120).IsRequired();
            entity.HasIndex(e => new { e.Name, e.City }).IsUnique();
        });

        modelBuilder.Entity<HotelPrice>(entity =>
        {
            entity.Property(e => e.Price).HasPrecision(18, 2);
            entity.Property(e => e.Currency).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Source).HasMaxLength(40).IsRequired();
            entity.Property(e => e.SearchCity).HasMaxLength(120);
            entity.HasOne(e => e.Hotel)
                .WithMany(h => h.Prices)
                .HasForeignKey(e => e.HotelId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.HotelId, e.DateCaptured });
        });
    }
}
