using backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace backend.Infrastructure.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<JobOffer> JobOffers => Set<JobOffer>();
    public DbSet<Opportunity> Opportunities => Set<Opportunity>();
    public DbSet<JobInsight> JobInsights => Set<JobInsight>();
    public DbSet<MarketCluster> MarketClusters => Set<MarketCluster>();
    public DbSet<ProductSuggestion> ProductSuggestions => Set<ProductSuggestion>();
    public DbSet<AiPromptLog> AiPromptLogs => Set<AiPromptLog>();
    public DbSet<AiPromptTemplate> AiPromptTemplates => Set<AiPromptTemplate>();
    public DbSet<ProviderSession> ProviderSessions => Set<ProviderSession>();
    public DbSet<OpportunityIdea> OpportunityIdeas => Set<OpportunityIdea>();
    public DbSet<CommercialStrategy> CommercialStrategies => Set<CommercialStrategy>();
    public DbSet<MvpRequirement> MvpRequirements => Set<MvpRequirement>();

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
            entity.Property(e => e.Category).HasMaxLength(80).IsRequired();
            entity.Property(e => e.OpportunityScore).IsRequired();
            entity.Property(e => e.IsConsultingCompany).IsRequired();
            entity.Property(e => e.CompanyType).HasMaxLength(40).IsRequired();
            entity.Property(e => e.IsProcessed).IsRequired();
            entity.Property(e => e.ProcessedAt);
            entity.HasIndex(e => new { e.Source, e.ExternalId }).IsUnique();
            entity.HasIndex(e => e.CapturedAt);
            entity.HasIndex(e => new { e.Company, e.Location });
            entity.HasIndex(e => new { e.IsConsultingCompany, e.OpportunityScore });
            entity.HasIndex(e => new { e.IsProcessed, e.CapturedAt });
        });

        modelBuilder.Entity<JobInsight>(entity =>
        {
            entity.Property(e => e.MainPainPoint).HasMaxLength(180).IsRequired();
            entity.Property(e => e.PainCategory).HasMaxLength(80).IsRequired();
            entity.Property(e => e.PainDescription).HasColumnType("text").IsRequired();
            entity.Property(e => e.TechStack).HasMaxLength(300).IsRequired();
            entity.Property(e => e.CompanyType).HasMaxLength(40).IsRequired();
            entity.Property(e => e.SuggestedSolution).HasColumnType("text").IsRequired();
            entity.Property(e => e.LeadMessage).HasColumnType("text").IsRequired();
            entity.Property(e => e.DecisionSource).HasMaxLength(40).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(40).IsRequired();
            entity.Property(e => e.EngineVersion).HasMaxLength(40).IsRequired();
            entity.Property(e => e.RawModelResponse).HasColumnType("text");
            // Fase 0 — Data Quality Layer
            entity.Property(e => e.Industry).HasMaxLength(60).IsRequired().HasDefaultValue("Unknown");
            entity.Property(e => e.NormalizedTechStack).HasMaxLength(400).IsRequired().HasDefaultValue("Unknown");
            entity.Property(e => e.TechTokensJson).HasColumnType("text").IsRequired().HasDefaultValue("[]");
            entity.Property(e => e.LeadScore).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.ClusterId);
            entity.HasIndex(e => e.JobId).IsUnique();
            entity.HasIndex(e => new { e.IsProcessed, e.ProcessedAt });
            entity.HasIndex(e => new { e.MainPainPoint, e.ProcessedAt });
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.Industry);
            entity.HasIndex(e => e.ClusterId);
            entity.HasIndex(e => e.LeadScore);

            entity.HasOne(e => e.Job)
                .WithMany()
                .HasForeignKey(e => e.JobId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<MarketCluster>()
                .WithMany(c => c.Insights)
                .HasForeignKey(e => e.ClusterId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<MarketCluster>(entity =>
        {
            entity.Property(e => e.ClusterKey).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Label).HasMaxLength(300).IsRequired();
            entity.Property(e => e.PainCategory).HasMaxLength(80).IsRequired();
            entity.Property(e => e.NormalizedTechStack).HasMaxLength(400).IsRequired();
            entity.Property(e => e.TechKeyPart).HasMaxLength(120).IsRequired();
            entity.Property(e => e.Industry).HasMaxLength(60).IsRequired();
            entity.Property(e => e.CompanyType).HasMaxLength(40).IsRequired();
            entity.Property(e => e.OpportunityType).HasMaxLength(40).IsRequired();
            entity.Property(e => e.RecommendedStrategy).HasMaxLength(200).IsRequired();
            entity.Property(e => e.LlmStatus).HasMaxLength(30).IsRequired();
            entity.Property(e => e.SynthesizedPain).HasColumnType("text");
            entity.Property(e => e.SynthesizedMvp).HasColumnType("text");
            entity.Property(e => e.SynthesizedLeadMessage).HasColumnType("text");
            entity.Property(e => e.MvpType).HasMaxLength(40);
            entity.Property(e => e.EstimatedDealSizeUsd).HasPrecision(12, 2);
            entity.Property(e => e.EngineVersion).HasMaxLength(40).IsRequired();

            entity.HasIndex(e => e.ClusterKey).IsUnique();
            entity.HasIndex(e => new { e.PainCategory, e.Industry, e.CompanyType });
            entity.HasIndex(e => e.BlueOceanScore);
            entity.HasIndex(e => new { e.IsActionable, e.PriorityScore });
            entity.HasIndex(e => e.LlmStatus);
            entity.HasIndex(e => e.LastUpdatedAt);
        });

        modelBuilder.Entity<ProviderSession>(entity =>
        {
            entity.Property(e => e.Provider).HasMaxLength(80).IsRequired();
            entity.Property(e => e.Username).HasMaxLength(200).IsRequired();
            entity.HasIndex(e => e.Provider).IsUnique();
            entity.HasIndex(e => e.IsAuthenticated);
        });

        modelBuilder.Entity<AiPromptLog>(entity =>
        {
            entity.Property(e => e.Provider).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ModelId).HasMaxLength(120).IsRequired();
            entity.Property(e => e.PromptVersion).HasMaxLength(40).IsRequired();
            entity.Property(e => e.PromptHash).HasMaxLength(64).IsRequired();
            entity.Property(e => e.PromptText).HasColumnType("text").IsRequired();
            entity.Property(e => e.ResponseText).HasColumnType("text").IsRequired();
            entity.Property(e => e.Status).HasMaxLength(60).IsRequired();
            entity.Property(e => e.ErrorMessage).HasColumnType("text");
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => new { e.Provider, e.ModelId, e.CreatedAt });
            entity.HasIndex(e => new { e.PromptHash, e.CreatedAt });
            entity.HasIndex(e => e.JobId);
            entity.HasIndex(e => e.ClusterId);

            entity.HasOne(e => e.Job)
                .WithMany()
                .HasForeignKey(e => e.JobId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne<MarketCluster>()
                .WithMany()
                .HasForeignKey(e => e.ClusterId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Opportunity>(entity =>
        {
            entity.Property(e => e.Company).HasMaxLength(220).IsRequired();
            entity.Property(e => e.JobTitle).HasMaxLength(300).IsRequired();
            entity.Property(e => e.JobDescription).HasColumnType("text");
            entity.Property(e => e.TechStack).HasMaxLength(300);
            entity.Property(e => e.ProductIdeasJson).HasColumnType("text");
            entity.Property(e => e.LlmStatus).HasMaxLength(20).IsRequired().HasDefaultValue("pending");
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired().HasDefaultValue("active");

            entity.HasIndex(e => e.JobId);
            entity.HasIndex(e => e.LlmStatus);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.Status);

            entity.HasOne(e => e.Job)
                .WithMany()
                .HasForeignKey(e => e.JobId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductSuggestion>(entity =>
        {
            entity.Property(e => e.ProductName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.ClusterIdsJson).HasColumnType("text").IsRequired().HasDefaultValue("[]");
            entity.Property(e => e.ProductDescription).HasColumnType("text").IsRequired();
            entity.Property(e => e.WhyNow).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Offer).HasMaxLength(200).IsRequired();
            entity.Property(e => e.ActionToday).HasMaxLength(300).IsRequired();
            entity.Property(e => e.TechFocus).HasMaxLength(400).IsRequired();
            entity.Property(e => e.MinDealSizeUsd).HasPrecision(12, 2);
            entity.Property(e => e.MaxDealSizeUsd).HasPrecision(12, 2);
            entity.Property(e => e.OpportunityType).HasMaxLength(40).IsRequired();
            entity.Property(e => e.Industry).HasMaxLength(60).IsRequired().HasDefaultValue("Unknown");
            entity.Property(e => e.LlmStatus).HasMaxLength(20).IsRequired().HasDefaultValue("pending");
            entity.Property(e => e.SynthesisDetailJson).HasColumnType("text");
            entity.Property(e => e.TechnicalMvpJson).HasColumnType("text");
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired().HasDefaultValue("open");
            entity.Property(e => e.OpportunityId);
            entity.Property(e => e.ImageUrl).HasMaxLength(1200);

            entity.HasIndex(e => e.ProductName).IsUnique();
            entity.HasIndex(e => e.PriorityScore);
            entity.HasIndex(e => e.GeneratedAt);
            entity.HasIndex(e => e.OpportunityType);
            entity.HasIndex(e => e.OpportunityId);
            entity.HasIndex(e => e.Status);

            entity.HasOne(e => e.Opportunity)
                .WithMany(o => o.Products)
                .HasForeignKey(e => e.OpportunityId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<OpportunityIdea>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(300).IsRequired();
            entity.Property(e => e.BusinessJustification).HasColumnType("text").IsRequired();
            entity.Property(e => e.OpportunityId);

            entity.HasIndex(e => e.OpportunityId);
            entity.HasIndex(e => e.CreatedAt);

            entity.HasOne(e => e.Opportunity)
                .WithMany(o => o.Ideas)
                .HasForeignKey(e => e.OpportunityId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CommercialStrategy>(entity =>
        {
            entity.Property(e => e.ProductName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.CompanyContext).HasColumnType("text").IsRequired();
            entity.Property(e => e.RealBusinessProblem).HasColumnType("text").IsRequired();
            entity.Property(e => e.FinancialImpact).HasColumnType("text").IsRequired();
            entity.Property(e => e.MvpDefinition).HasColumnType("text").IsRequired();
            entity.Property(e => e.TargetBuyer).HasColumnType("text").IsRequired();
            entity.Property(e => e.PricingStrategy).HasColumnType("text").IsRequired();
            entity.Property(e => e.OutreachMessage).HasColumnType("text").IsRequired();

            entity.HasIndex(e => e.ProductId);
            entity.HasIndex(e => e.GeneratedAt);

            entity.HasOne(e => e.Product)
                .WithMany()
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<MvpRequirement>(entity =>
        {
            entity.Property(e => e.ProductName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.CompanyContext).HasColumnType("text").IsRequired();
            entity.Property(e => e.ArchitectureStrategy).HasColumnType("text").IsRequired();
            entity.Property(e => e.RequiredTechStackJson).HasColumnType("text").IsRequired().HasDefaultValue("[]");
            entity.Property(e => e.EstimatedTimelines).HasColumnType("text").IsRequired();
            entity.Property(e => e.CoreFeaturesJson).HasColumnType("text").IsRequired().HasDefaultValue("[]");

            entity.HasIndex(e => e.ProductId);
            entity.HasIndex(e => e.GeneratedAt);

            entity.HasOne(e => e.Product)
                .WithMany()
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AiPromptTemplate>(entity =>
        {
            entity.Property(e => e.Key).HasMaxLength(120).IsRequired();
            entity.Property(e => e.Template).HasColumnType("text").IsRequired();
            entity.Property(e => e.Version).HasMaxLength(40).IsRequired();
            entity.Property(e => e.UpdatedBy).HasMaxLength(120);
            entity.Property(e => e.UpdatedAt).IsRequired();
            entity.HasIndex(e => e.Key).IsUnique();
            entity.HasIndex(e => new { e.Key, e.IsActive });
        });
    }
}
