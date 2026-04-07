using backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace backend.Infrastructure.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<JobOffer> JobOffers => Set<JobOffer>();
    public DbSet<ProviderSession> ProviderSessions => Set<ProviderSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<JobOffer>(entity =>
        {
            entity.Property(e => e.ExternalId).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Title).HasMaxLength(300).IsRequired();
            entity.Property(e => e.Company).HasMaxLength(220).IsRequired();
            entity.Property(e => e.Location).HasMaxLength(220).IsRequired();
            entity.Property(e => e.Description).HasColumnType("text").IsRequired();
            entity.Property(e => e.Url).HasMaxLength(600).IsRequired();
            entity.Property(e => e.Contact).HasMaxLength(200);
            entity.Property(e => e.SalaryRange).HasMaxLength(120);
            entity.Property(e => e.Seniority).HasMaxLength(100);
            entity.Property(e => e.ContractType).HasMaxLength(100);
            entity.Property(e => e.Source).HasMaxLength(50).IsRequired();
            entity.Property(e => e.SearchTerm).HasMaxLength(200).IsRequired();
            entity.Property(e => e.MetadataJson).HasColumnType("jsonb");
            entity.HasIndex(e => new { e.Source, e.ExternalId }).IsUnique();
            entity.HasIndex(e => e.CapturedAt);
            entity.HasIndex(e => new { e.Company, e.Location });
        });

        modelBuilder.Entity<ProviderSession>(entity =>
        {
            entity.Property(e => e.Provider).HasMaxLength(80).IsRequired();
            entity.Property(e => e.Username).HasMaxLength(200).IsRequired();
            entity.HasIndex(e => e.Provider).IsUnique();
            entity.HasIndex(e => e.IsAuthenticated);
        });
    }
}
