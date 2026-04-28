using backend.Domain.Entities;
using backend.Tests.Fakes;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace backend.Tests;

/// <summary>
/// EF Core InMemory tests for the Opportunity → ProductSuggestion FK relationship.
/// Verifies that the new entities persist, query, and cascade correctly.
/// </summary>
public class FunnelEntityTests
{
    // ── Opportunity persistence ───────────────────────────────────────────────

    [Fact]
    public async Task Opportunity_CanBeSavedAndQueried()
    {
        var db  = TestApplicationDbContextFactory.Create();
        var job = SeedJob(db);

        var opp = new Opportunity
        {
            JobId          = job.Id,
            Company        = "Globex",
            JobTitle       = "Platform Engineer",
            JobDescription = "Cloud migration project",
            TechStack      = "Azure+Kubernetes",
            LlmStatus      = "pending",
            CreatedAt      = DateTime.UtcNow
        };
        db.Opportunities.Add(opp);
        await db.SaveChangesAsync();

        var loaded = await db.Opportunities.AsNoTracking().SingleAsync(o => o.Id == opp.Id);
        Assert.Equal("Globex", loaded.Company);
        Assert.Equal("Platform Engineer", loaded.JobTitle);
        Assert.Equal("Azure+Kubernetes", loaded.TechStack);
        Assert.Equal("pending", loaded.LlmStatus);
        Assert.Null(loaded.ProductIdeasJson);
        Assert.Null(loaded.SynthesizedAt);
    }

    [Fact]
    public async Task Opportunity_CanStoreLlmResultAndUpdateStatus()
    {
        var db  = TestApplicationDbContextFactory.Create();
        var job = SeedJob(db);
        var opp = new Opportunity
        {
            JobId     = job.Id,
            Company   = "Initech",
            JobTitle  = "Backend Dev",
            LlmStatus = "pending",
            CreatedAt = DateTime.UtcNow
        };
        db.Opportunities.Add(opp);
        await db.SaveChangesAsync();

        // Simulate synthesize-ideas completing
        var ideas = """[{"name":"Audit Bot","shortTechnicalDescription":"Automates code review."}]""";
        opp.ProductIdeasJson = ideas;
        opp.LlmStatus        = "completed";
        opp.SynthesizedAt    = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var loaded = await db.Opportunities.AsNoTracking().SingleAsync(o => o.Id == opp.Id);
        Assert.Equal("completed", loaded.LlmStatus);
        Assert.Equal(ideas, loaded.ProductIdeasJson);
        Assert.NotNull(loaded.SynthesizedAt);
    }

    // ── ProductSuggestion.OpportunityId FK ───────────────────────────────────

    [Fact]
    public async Task ProductSuggestion_CanBeSavedWithOpportunityId()
    {
        var db  = TestApplicationDbContextFactory.Create();
        var job = SeedJob(db);
        var opp = SeedOpportunity(db, job.Id);

        var product = BuildManualProduct(opp.Id);
        db.ProductSuggestions.Add(product);
        await db.SaveChangesAsync();

        var loaded = await db.ProductSuggestions.AsNoTracking().SingleAsync(p => p.Id == product.Id);
        Assert.Equal(opp.Id, loaded.OpportunityId);
        Assert.Equal("Manual", loaded.OpportunityType);
    }

    [Fact]
    public async Task ProductSuggestion_CanHaveNullOpportunityId()
    {
        var db      = TestApplicationDbContextFactory.Create();
        var product = BuildManualProduct(opportunityId: null);
        db.ProductSuggestions.Add(product);
        await db.SaveChangesAsync();

        var loaded = await db.ProductSuggestions.AsNoTracking().SingleAsync(p => p.Id == product.Id);
        Assert.Null(loaded.OpportunityId);
    }

    [Fact]
    public async Task Opportunity_WithProducts_CanBeQueriedWithInclude()
    {
        var db  = TestApplicationDbContextFactory.Create();
        var job = SeedJob(db);
        var opp = SeedOpportunity(db, job.Id);

        db.ProductSuggestions.Add(BuildManualProduct(opp.Id, name: "Product A"));
        db.ProductSuggestions.Add(BuildManualProduct(opp.Id, name: "Product B"));
        await db.SaveChangesAsync();

        var loadedOpp = await db.Opportunities
            .Include(o => o.Products)
            .SingleAsync(o => o.Id == opp.Id);

        Assert.Equal(2, loadedOpp.Products.Count);
    }

    [Fact]
    public async Task MultipleOpportunities_SamJob_AreAllowed()
    {
        var db  = TestApplicationDbContextFactory.Create();
        var job = SeedJob(db);

        // Same job can be converted to multiple opportunities
        db.Opportunities.Add(new Opportunity { JobId = job.Id, Company = "A", JobTitle = "T1", LlmStatus = "pending", CreatedAt = DateTime.UtcNow });
        db.Opportunities.Add(new Opportunity { JobId = job.Id, Company = "A", JobTitle = "T1", LlmStatus = "pending", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        Assert.Equal(2, await db.Opportunities.CountAsync());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static JobOffer SeedJob(backend.Infrastructure.Data.ApplicationDbContext db)
    {
        var job = new JobOffer
        {
            ExternalId          = Guid.NewGuid().ToString("N"),
            Title               = "Platform Engineer",
            Company             = "Acme Corp",
            Location            = "Remote",
            Description         = "We need cloud migration expertise.",
            Url                 = "https://example.com/job/1",
            Source              = "linkedin",
            SearchTerm          = ".net",
            Category            = "Engineering",
            OpportunityScore    = 80,
            IsConsultingCompany = false,
            CompanyType         = "direct",
            CapturedAt          = DateTime.UtcNow
        };
        db.JobOffers.Add(job);
        db.SaveChanges();
        return job;
    }

    private static Opportunity SeedOpportunity(
        backend.Infrastructure.Data.ApplicationDbContext db,
        int jobId,
        string company = "Acme Corp",
        string title   = "Platform Engineer")
    {
        var opp = new Opportunity
        {
            JobId     = jobId,
            Company   = company,
            JobTitle  = title,
            LlmStatus = "pending",
            CreatedAt = DateTime.UtcNow
        };
        db.Opportunities.Add(opp);
        db.SaveChanges();
        return opp;
    }

    private static ProductSuggestion BuildManualProduct(int? opportunityId, string name = "Automation Suite")
        => new()
        {
            ProductName          = name,
            ProductDescription   = "Test product",
            WhyNow               = "Now",
            Offer                = "Quote",
            ActionToday          = "Send email",
            TechFocus            = "Azure",
            ClusterIdsJson       = "[]",
            EstimatedBuildDays   = 14,
            MinDealSizeUsd       = 5000,
            MaxDealSizeUsd       = 10000,
            TotalJobCount        = 1,
            AvgDirectClientRatio = 1.0,
            AvgUrgencyScore      = 7,
            TopBlueOceanScore    = 6,
            ClusterCount         = 0,
            PriorityScore        = 100,
            OpportunityType      = "Manual",
            Industry             = "Technology",
            LlmStatus            = "pending",
            OpportunityId        = opportunityId,
            GeneratedAt          = DateTime.UtcNow
        };
}
