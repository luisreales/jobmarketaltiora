using backend.Application.Contracts;
using backend.Application.Interfaces;
using backend.Controllers;
using backend.Domain.Entities;
using backend.Tests.Fakes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace backend.Tests;

/// <summary>
/// Tests for POST /api/jobs/jobs/{id}/create-opportunity.
/// Orchestrator and ProcessingService are faked with minimal no-op implementations.
/// </summary>
public class JobsControllerFunnelTests
{
    // ── POST /api/jobs/jobs/{id}/create-opportunity ───────────────────────────

    [Fact]
    public async Task CreateOpportunity_WhenJobNotFound_Returns404()
    {
        var db         = TestApplicationDbContextFactory.Create();
        var controller = BuildController(db);

        var result = await controller.CreateOpportunity(9999, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateOpportunity_WhenJobExists_Returns201WithOpportunityDto()
    {
        var db  = TestApplicationDbContextFactory.Create();
        var job = SeedJob(db, company: "Globex", title: "Site Reliability Engineer");
        var controller = BuildController(db);

        var result = await controller.CreateOpportunity(job.Id, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(201, created.StatusCode);
        var dto = Assert.IsType<OpportunityDto>(created.Value);
        Assert.Equal(job.Id,   dto.JobId);
        Assert.Equal("Globex", dto.Company);
        Assert.Equal("Site Reliability Engineer", dto.JobTitle);
        Assert.Equal("pending", dto.LlmStatus);
        Assert.Null(dto.ProductIdeasJson);
    }

    [Fact]
    public async Task CreateOpportunity_PersistsOpportunityInDb()
    {
        var db  = TestApplicationDbContextFactory.Create();
        var job = SeedJob(db, company: "Initech", title: "DevOps Engineer");
        var controller = BuildController(db);

        await controller.CreateOpportunity(job.Id, CancellationToken.None);

        var count = db.Opportunities.Count();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task CreateOpportunity_WhenInsightExists_EnrichesTechStack()
    {
        var db  = TestApplicationDbContextFactory.Create();
        var job = SeedJob(db, company: "Acme", title: "Cloud Architect");
        db.JobInsights.Add(new JobInsight
        {
            JobId               = job.Id,
            NormalizedTechStack = "Azure+Kubernetes+Terraform"
        });
        await db.SaveChangesAsync();
        var controller = BuildController(db);

        var result = await controller.CreateOpportunity(job.Id, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var dto     = Assert.IsType<OpportunityDto>(created.Value);
        Assert.Equal("Azure+Kubernetes+Terraform", dto.TechStack);
    }

    [Fact]
    public async Task CreateOpportunity_WhenInsightIsUnknown_LeavesNullTechStack()
    {
        var db  = TestApplicationDbContextFactory.Create();
        var job = SeedJob(db, company: "Umbrella", title: "Backend Dev");
        db.JobInsights.Add(new JobInsight
        {
            JobId               = job.Id,
            NormalizedTechStack = "Unknown"
        });
        await db.SaveChangesAsync();
        var controller = BuildController(db);

        var result = await controller.CreateOpportunity(job.Id, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var dto     = Assert.IsType<OpportunityDto>(created.Value);
        Assert.Null(dto.TechStack);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static JobsController BuildController(backend.Infrastructure.Data.ApplicationDbContext db)
        => new(new NullJobOrchestrator(), new NullJobProcessingService(), db);

    private static JobOffer SeedJob(
        backend.Infrastructure.Data.ApplicationDbContext db,
        string company = "Acme Corp",
        string title   = "Platform Engineer")
    {
        var job = new JobOffer
        {
            ExternalId          = Guid.NewGuid().ToString("N"),
            Title               = title,
            Company             = company,
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

    // ── Minimal fakes (avoid making IJobOrchestrator / IJobProcessingService real) ──

    private sealed class NullJobOrchestrator : IJobOrchestrator
    {
        public Task<(int savedCount, int totalFound)> SearchAndSaveAsync(
            string query, string? location, int limit,
            IReadOnlyCollection<string>? providers = null,
            int? totalPaging = null, int? startPage = null, int? endPage = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult((0, 0));

        public Task<List<JobOffer>> GetJobsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new List<JobOffer>());

        public Task<(List<JobOffer> Items, int TotalCount)> QueryJobsAsync(
            JobsQueryRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult((new List<JobOffer>(), 0));

        public Task<List<JobOffer>> GetHighValueLeadsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new List<JobOffer>());

        public Task<JobOffer?> GetJobByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult<JobOffer?>(null);

        public Task<(bool isAuthenticated, DateTime? lastLoginAt, DateTime? lastUsedAt, DateTime? expiresAt)>
            GetAuthStatusAsync(string provider, CancellationToken cancellationToken = default)
            => Task.FromResult((false, (DateTime?)null, (DateTime?)null, (DateTime?)null));

        public Task LoginAsync(string provider, string username, string password,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task LogoutAsync(string provider, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class NullJobProcessingService : IJobProcessingService
    {
        public Task<List<JobOffer>> GetLeadsAsync(JobFilter filter, CancellationToken ct = default)
            => Task.FromResult(new List<JobOffer>());

        public Task<List<JobOffer>> GetProcessedAsync(CancellationToken ct = default)
            => Task.FromResult(new List<JobOffer>());

        public Task<List<JobOffer>> GetUnprocessedAsync(CancellationToken ct = default)
            => Task.FromResult(new List<JobOffer>());

        public Task<int> ProcessUnprocessedJobsAsync(CancellationToken ct, int? batchSize = null, bool processAll = false)
            => Task.FromResult(0);
    }
}
