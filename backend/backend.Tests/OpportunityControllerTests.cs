using backend.Application.Contracts;
using backend.Application.Interfaces;
using backend.Controllers;
using backend.Domain.Entities;
using backend.Tests.Fakes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Xunit;
using System.Linq;

namespace backend.Tests;

/// <summary>
/// Tests for GET /api/opportunities (list + single) and the
/// SK-not-configured path of POST synthesize-ideas.
/// Real LLM calls are avoided by using a FakeSemanticKernelProvider
/// that returns a kernel with no ChatCompletion service registered.
/// </summary>
public class OpportunityControllerTests
{
    // ── GET /api/opportunities ────────────────────────────────────────────────

    [Fact]
    public async Task GetOpportunities_WhenDbIsEmpty_ReturnsEmptyPage()
    {
        var db = TestApplicationDbContextFactory.Create();
        var controller = BuildController(db);

        var result = await controller.GetOpportunities(new OpportunityQuery(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var paged = Assert.IsType<PagedResultDto<OpportunityDto>>(ok.Value);
        Assert.Equal(0, paged.TotalCount);
        Assert.Empty(paged.Items);
    }

    [Fact]
    public async Task GetOpportunities_ReturnsInsertedOpportunity()
    {
        var db = TestApplicationDbContextFactory.Create();
        var job = SeedJob(db);
        db.Opportunities.Add(new Opportunity
        {
            JobId     = job.Id,
            Company   = job.Company,
            JobTitle  = job.Title,
            LlmStatus = "pending",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var controller = BuildController(db);
        var result = await controller.GetOpportunities(new OpportunityQuery(), CancellationToken.None);

        var ok    = Assert.IsType<OkObjectResult>(result.Result);
        var paged = Assert.IsType<PagedResultDto<OpportunityDto>>(ok.Value);
        Assert.Equal(1, paged.TotalCount);
        Assert.Equal("Acme Corp", paged.Items.First().Company);
    }

    [Fact]
    public async Task GetOpportunities_FilterByLlmStatus_ReturnsOnlyMatching()
    {
        var db  = TestApplicationDbContextFactory.Create();
        var job = SeedJob(db);
        db.Opportunities.AddRange(
            new Opportunity { JobId = job.Id, Company = "A", JobTitle = "T1", LlmStatus = "pending",   CreatedAt = DateTime.UtcNow },
            new Opportunity { JobId = job.Id, Company = "B", JobTitle = "T2", LlmStatus = "completed", CreatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        var controller = BuildController(db);
        var result = await controller.GetOpportunities(
            new OpportunityQuery { LlmStatus = "pending" }, CancellationToken.None);

        var ok    = Assert.IsType<OkObjectResult>(result.Result);
        var paged = Assert.IsType<PagedResultDto<OpportunityDto>>(ok.Value);
        Assert.Equal(1, paged.TotalCount);
        Assert.Equal("pending", paged.Items.First().LlmStatus);
    }

    // ── GET /api/opportunities/{id} ───────────────────────────────────────────

    [Fact]
    public async Task GetOpportunity_WithValidId_ReturnsDto()
    {
        var db  = TestApplicationDbContextFactory.Create();
        var job = SeedJob(db);
        var opp = new Opportunity
        {
            JobId     = job.Id,
            Company   = "TechCo",
            JobTitle  = "Backend Developer",
            LlmStatus = "pending",
            CreatedAt = DateTime.UtcNow
        };
        db.Opportunities.Add(opp);
        await db.SaveChangesAsync();

        var controller = BuildController(db);
        var result = await controller.GetOpportunity(opp.Id, CancellationToken.None);

        var ok  = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<OpportunityDto>(ok.Value);
        Assert.Equal("TechCo", dto.Company);
        Assert.Equal("Backend Developer", dto.JobTitle);
        Assert.Equal("pending", dto.LlmStatus);
    }

    [Fact]
    public async Task GetOpportunity_WithInvalidId_Returns404()
    {
        var db  = TestApplicationDbContextFactory.Create();
        var controller = BuildController(db);

        var result = await controller.GetOpportunity(9999, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    // ── POST /api/opportunities/{id}/synthesize-ideas — SK not configured ─────

    [Fact]
    public async Task SynthesizeIdeas_WhenOpportunityNotFound_Returns404()
    {
        var db  = TestApplicationDbContextFactory.Create();
        var controller = BuildController(db, skConfigured: false);

        var result = await controller.SynthesizeIdeas(9999);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task SynthesizeIdeas_WhenSkNotConfigured_Returns503()
    {
        var db  = TestApplicationDbContextFactory.Create();
        var job = SeedJob(db);
        var opp = new Opportunity
        {
            JobId     = job.Id,
            Company   = "Acme",
            JobTitle  = "Cloud Engineer",
            LlmStatus = "pending",
            CreatedAt = DateTime.UtcNow
        };
        db.Opportunities.Add(opp);
        await db.SaveChangesAsync();

        var controller = BuildController(db, skConfigured: false);
        var result = await controller.SynthesizeIdeas(opp.Id);

        var problem = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(503, problem.StatusCode);
    }

    [Fact]
    public async Task SynthesizeIdeas_WhenAlreadySynthesized_ReturnsCachedResult()
    {
        var db  = TestApplicationDbContextFactory.Create();
        var job = SeedJob(db);
        var ideas = """[{"name":"Audit","shortTechnicalDescription":"A quick audit."}]""";
        var opp = new Opportunity
        {
            JobId            = job.Id,
            Company          = "Acme",
            JobTitle         = "Cloud Engineer",
            LlmStatus        = "completed",
            ProductIdeasJson = ideas,
            CreatedAt        = DateTime.UtcNow
        };
        db.Opportunities.Add(opp);
        await db.SaveChangesAsync();

        // Even with a configured SK provider, the cache-hit path should not call LLM
        var controller = BuildController(db, skConfigured: true);
        var result = await controller.SynthesizeIdeas(opp.Id);

        var ok  = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<OpportunityDto>(ok.Value);
        Assert.Equal("completed", dto.LlmStatus);
        Assert.Equal(ideas, dto.ProductIdeasJson);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static OpportunityController BuildController(
        backend.Infrastructure.Data.ApplicationDbContext db,
        bool skConfigured = false)
    {
        var provider = skConfigured
            ? new FakeSkProvider(isConfigured: true)
            : new FakeSkProvider(isConfigured: false);

        return new OpportunityController(
            db,
            provider,
            NullLogger<OpportunityController>.Instance);
    }

    private static JobOffer SeedJob(backend.Infrastructure.Data.ApplicationDbContext db)
    {
        var job = new JobOffer
        {
            ExternalId         = Guid.NewGuid().ToString("N"),
            Title              = "Platform Engineer",
            Company            = "Acme Corp",
            Location           = "Remote",
            Description        = "We need cloud migration expertise.",
            Url                = "https://example.com/job/1",
            Source             = "linkedin",
            SearchTerm         = ".net",
            Category           = "Engineering",
            OpportunityScore   = 80,
            IsConsultingCompany = false,
            CompanyType        = "direct",
            CapturedAt         = DateTime.UtcNow
        };
        db.JobOffers.Add(job);
        db.SaveChanges();
        return job;
    }

    private sealed class FakeSkProvider(bool isConfigured) : ISemanticKernelProvider
    {
        public bool IsConfigured { get; } = isConfigured;
        public Kernel GetKernel() => null!; // returns null so controller hits 503 path
    }
}
