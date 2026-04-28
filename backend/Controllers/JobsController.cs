using backend.Application.Contracts;
using backend.Application.Interfaces;
using backend.Domain.Entities;
using backend.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Route("api/jobs")]
[Route("api/linkedin")]
public class JobsController(
    IJobOrchestrator orchestrator,
    IJobProcessingService processingService,
    ApplicationDbContext dbContext) : ControllerBase
{
    [HttpPost("search/scrape")]
    public async Task<ActionResult<JobSearchResponse>> Scrape(
        [FromBody] JobSearchRequest request)
    {
        try
        {
            // Use a detached token so browser disconnect doesn't cancel a long scrape
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(60));
            var (savedCount, totalFound) = await orchestrator.SearchAndSaveAsync(
                request.Query,
                request.Location,
                request.Limit,
                request.Providers,
                request.TotalPaging,
                request.StartPage,
                request.EndPage,
                cancellationToken: cts.Token);

            return Ok(new JobSearchResponse(savedCount, totalFound, DateTime.UtcNow));
        }
        catch (InvalidOperationException ex)
        {
            return Problem(
                title: "Scraping requires re-authentication",
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict);
        }
    }

    [HttpPost("search/scrape/upwork")]
    public async Task<ActionResult<object>> ScrapeUpwork(
        [FromBody] JobSearchRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var startedAtUtc = DateTime.UtcNow;

            var (savedCount, totalFound) = await orchestrator.SearchAndSaveAsync(
                request.Query,
                request.Location,
                request.Limit,
                ["upwork"],
                request.TotalPaging,
                request.StartPage,
                request.EndPage,
                cancellationToken: cancellationToken);

            var jobs = await orchestrator.GetJobsAsync(cancellationToken);
            var touched = jobs
                .Where(x => x.Source.Equals("upwork", StringComparison.OrdinalIgnoreCase) &&
                            x.CapturedAt >= startedAtUtc.AddMinutes(-5))
                .OrderByDescending(x => x.CapturedAt)
                .Take(25)
                .Select(x => new
                {
                    x.Id,
                    x.Title,
                    x.Company,
                    DescriptionPreview = BuildDescriptionPreview(x.Description),
                    x.Url,
                    x.SearchTerm,
                    x.CapturedAt,
                    DetailEndpoint = $"/api/jobs/upwork/{x.Id}/detail"
                })
                .ToList();

            return Ok(new
            {
                provider = "upwork",
                savedCount,
                totalFound,
                touchedCount = touched.Count,
                touched,
                executedAtUtc = DateTime.UtcNow
            });
        }
        catch (InvalidOperationException ex)
        {
            return Problem(
                title: "Upwork scraping requires re-authentication",
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict);
        }
    }

    [HttpPost("search/scrape/upwork/login-and-scrape")]
    public async Task<ActionResult<object>> LoginAndScrapeUpwork(
        [FromBody] JobSearchRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var status = await orchestrator.GetAuthStatusAsync("upwork", cancellationToken);
            if (!status.isAuthenticated)
            {
                await orchestrator.LoginAsync("upwork", string.Empty, string.Empty, request.ShowBrowser, cancellationToken);
            }

            var startedAtUtc = DateTime.UtcNow;
            var (savedCount, totalFound) = await orchestrator.SearchAndSaveAsync(
                request.Query,
                request.Location,
                request.Limit,
                ["upwork"],
                request.TotalPaging,
                request.StartPage,
                request.EndPage,
                request.ShowBrowser,
                cancellationToken);

            var jobs = await orchestrator.GetJobsAsync(cancellationToken);
            var touched = jobs
                .Where(x => x.Source.Equals("upwork", StringComparison.OrdinalIgnoreCase) &&
                            x.CapturedAt >= startedAtUtc.AddMinutes(-5))
                .OrderByDescending(x => x.CapturedAt)
                .Take(25)
                .Select(x => new
                {
                    x.Id,
                    x.Title,
                    x.Company,
                    DescriptionPreview = BuildDescriptionPreview(x.Description),
                    x.Url,
                    x.SearchTerm,
                    x.CapturedAt,
                    DetailEndpoint = $"/api/jobs/upwork/{x.Id}/detail"
                })
                .ToList();

            return Ok(new
            {
                provider = "upwork",
                mode = "login-and-scrape",
                savedCount,
                totalFound,
                touchedCount = touched.Count,
                touched,
                executedAtUtc = DateTime.UtcNow
            });
        }
        catch (InvalidOperationException ex)
        {
            return Problem(
                title: "Upwork login/scraping requires manual action",
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict);
        }
        catch (TimeoutException ex)
        {
            return Problem(
                title: "Upwork login/scraping timeout",
                detail: ex.Message,
                statusCode: StatusCodes.Status408RequestTimeout);
        }
    }

    [HttpGet("jobs")]
    public async Task<ActionResult<List<JobSummaryDto>>> GetJobs(CancellationToken cancellationToken)
    {
        var jobs = await orchestrator.GetJobsAsync(cancellationToken);
        var allJobIds = jobs.Select(x => x.Id).ToList();
        var allOpportunityMap = await dbContext.Opportunities
            .AsNoTracking()
            .Where(o => allJobIds.Contains(o.JobId))
            .GroupBy(o => o.JobId)
            .Select(g => new { JobId = g.Key, OpportunityId = g.Min(o => o.Id) })
            .ToDictionaryAsync(x => x.JobId, x => x.OpportunityId, cancellationToken);

        return Ok(jobs.Select(x => new JobSummaryDto(
            x.Id,
            x.Title,
            x.Company,
            x.Location,
            BuildDescriptionPreview(x.Description),
            x.Category,
            x.OpportunityScore,
            x.IsConsultingCompany,
            x.CompanyType,
            x.Source,
            x.SearchTerm,
            x.CapturedAt,
            x.PublishedAt,
            x.SalaryRange,
            x.Seniority,
            x.ContractType,
            x.Url,
            allOpportunityMap.ContainsKey(x.Id),
            allOpportunityMap.TryGetValue(x.Id, out var aOppId) ? aOppId : null)).ToList());
    }

    [HttpGet("jobs/query")]
    public async Task<ActionResult<PagedResultDto<JobSummaryDto>>> QueryJobs(
        [FromQuery] JobsQueryRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedPage = Math.Max(1, request.Page);
        var normalizedPageSize = Math.Clamp(request.PageSize, 1, 20);
        var normalizedSortBy = string.IsNullOrWhiteSpace(request.SortBy) ? "capturedAt" : request.SortBy.Trim();
        var normalizedSortDirection = string.Equals(request.SortDirection, "asc", StringComparison.OrdinalIgnoreCase)
            ? "asc"
            : "desc";

        request.Page = normalizedPage;
        request.PageSize = normalizedPageSize;
        request.SortBy = normalizedSortBy;
        request.SortDirection = normalizedSortDirection;

        var (items, totalCount) = await orchestrator.QueryJobsAsync(request, cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)normalizedPageSize));

        var jobIds = items.Select(x => x.Id).ToList();
        var opportunityMap = await dbContext.Opportunities
            .AsNoTracking()
            .Where(o => jobIds.Contains(o.JobId))
            .GroupBy(o => o.JobId)
            .Select(g => new { JobId = g.Key, OpportunityId = g.Min(o => o.Id) })
            .ToDictionaryAsync(x => x.JobId, x => x.OpportunityId, cancellationToken);

        var dtoItems = items.Select(x => new JobSummaryDto(
            x.Id,
            x.Title,
            x.Company,
            x.Location,
            BuildDescriptionPreview(x.Description),
            x.Category,
            x.OpportunityScore,
            x.IsConsultingCompany,
            x.CompanyType,
            x.Source,
            x.SearchTerm,
            x.CapturedAt,
            x.PublishedAt,
            x.SalaryRange,
            x.Seniority,
            x.ContractType,
            x.Url,
            opportunityMap.ContainsKey(x.Id),
            opportunityMap.TryGetValue(x.Id, out var oppId) ? oppId : null)).ToList();

        return Ok(new PagedResultDto<JobSummaryDto>(
            dtoItems,
            normalizedPage,
            normalizedPageSize,
            totalCount,
            totalPages,
            normalizedSortBy,
            normalizedSortDirection));
    }

    [HttpGet("jobs/full")]
    public async Task<ActionResult<List<JobFullListDto>>> GetJobsFull(CancellationToken cancellationToken)
    {
        var jobs = await orchestrator.GetJobsAsync(cancellationToken);
        return Ok(jobs.Select(x => new JobFullListDto(
            x.Id,
            x.ExternalId,
            x.Title,
            x.Company,
            x.Location,
            x.Description,
            x.Category,
            x.OpportunityScore,
            x.IsConsultingCompany,
            x.CompanyType,
            x.Url,
            x.Source,
            x.SearchTerm,
            x.CapturedAt,
            x.MetadataJson)).ToList());
    }

    [HttpGet("jobs/{id:int}")]
    public async Task<ActionResult<JobDetailDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var job = await orchestrator.GetJobByIdAsync(id, cancellationToken);
        if (job is null)
        {
            return NotFound();
        }

        return Ok(new JobDetailDto(
            job.Id,
            job.ExternalId,
            job.Title,
            job.Company,
            job.Location,
            job.Description,
            job.Category,
            job.OpportunityScore,
            job.IsConsultingCompany,
            job.CompanyType,
            job.Url,
            job.Contact,
            job.SalaryRange,
            job.PublishedAt,
            job.Seniority,
            job.ContractType,
            job.Source,
            job.SearchTerm,
            job.CapturedAt,
            job.MetadataJson));
    }

    [HttpGet("upwork/{id:int}/detail")]
    public async Task<ActionResult<JobDetailDto>> GetUpworkDetailById(int id, CancellationToken cancellationToken)
    {
        var job = await orchestrator.GetJobByIdAsync(id, cancellationToken);
        if (job is null || !job.Source.Equals("upwork", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }

        return Ok(new JobDetailDto(
            job.Id,
            job.ExternalId,
            job.Title,
            job.Company,
            job.Location,
            job.Description,
            job.Category,
            job.OpportunityScore,
            job.IsConsultingCompany,
            job.CompanyType,
            job.Url,
            job.Contact,
            job.SalaryRange,
            job.PublishedAt,
            job.Seniority,
            job.ContractType,
            job.Source,
            job.SearchTerm,
            job.CapturedAt,
            job.MetadataJson));
    }

    // ── Human-in-the-loop funnel ──────────────────────────────────────────────

    /// <summary>
    /// Converts a job posting into a manual sales Opportunity.
    /// Denormalizes key fields from the job for funnel independence.
    /// </summary>
    [HttpPost("jobs/{id:int}/create-opportunity")]
    public async Task<ActionResult<OpportunityDto>> CreateOpportunity(
        int id,
        CancellationToken cancellationToken)
    {
        var job = await dbContext.JobOffers
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

        if (job is null)
            return NotFound(new { message = $"Job {id} not found." });

        // Idempotent — return existing opportunity if already created for this job
        var existing = await dbContext.Opportunities
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.JobId == id, cancellationToken);

        if (existing is not null)
        {
            return Ok(new OpportunityDto(
                existing.Id,
                existing.JobId,
                existing.Company,
                existing.JobTitle,
                existing.JobDescription,
                existing.TechStack,
                existing.ProductIdeasJson,
                existing.LlmStatus,
                existing.Status,
                existing.CreatedAt,
                existing.SynthesizedAt,
                []));
        }

        var opportunity = new Opportunity
        {
            JobId          = job.Id,
            Company        = job.Company,
            JobTitle       = job.Title,
            JobDescription = job.Description,
            TechStack      = null,
            LlmStatus      = "pending",
            CreatedAt      = DateTime.UtcNow
        };

        // Try to enrich TechStack from JobInsight if already processed
        var insight = await dbContext.JobInsights
            .AsNoTracking()
            .Where(i => i.JobId == id)
            .Select(i => new { i.NormalizedTechStack })
            .FirstOrDefaultAsync(cancellationToken);

        if (insight is not null && insight.NormalizedTechStack != "Unknown")
            opportunity.TechStack = insight.NormalizedTechStack;

        dbContext.Opportunities.Add(opportunity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(
            actionName: "GetOpportunity",
            controllerName: "Opportunity",
            routeValues: new { id = opportunity.Id },
            value: new OpportunityDto(
                opportunity.Id,
                opportunity.JobId,
                opportunity.Company,
                opportunity.JobTitle,
                opportunity.JobDescription,
                opportunity.TechStack,
                opportunity.ProductIdeasJson,
                opportunity.LlmStatus,
                opportunity.Status,
                opportunity.CreatedAt,
                opportunity.SynthesizedAt,
                []));
    }

    /// <summary>
    /// Removes the Opportunity linked to a job (and its associated products).
    /// Idempotent — returns 204 even if no opportunity existed.
    /// </summary>
    [HttpDelete("jobs/{id:int}/remove-opportunity")]
    public async Task<IActionResult> RemoveOpportunity(int id, CancellationToken cancellationToken)
    {
        var opportunity = await dbContext.Opportunities
            .Include(o => o.Products)
            .FirstOrDefaultAsync(o => o.JobId == id, cancellationToken);

        if (opportunity is null)
            return NoContent(); // already removed — idempotent

        dbContext.Opportunities.Remove(opportunity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpGet("leads")]
    public async Task<ActionResult<List<JobLeadDto>>> GetLeads(
        [FromQuery] int? minScore,
        [FromQuery] bool directOnly = true,
        [FromQuery] string? search = null,
        [FromQuery] string? category = null,
        [FromQuery] string? companyType = null,
        CancellationToken cancellationToken = default)
    {
        var filter = new JobFilter
        {
            MinScore = minScore,
            DirectOnly = directOnly,
            Search = search,
            Category = category,
            CompanyType = companyType
        };

        var jobs = await processingService.GetLeadsAsync(filter, cancellationToken);
        return Ok(jobs.Select(x => new JobLeadDto(
            x.Id,
            x.Title,
            x.Company,
            x.Location,
            x.Category,
            x.OpportunityScore,
            x.CompanyType,
            x.Url,
            x.CapturedAt)).ToList());
    }

    [HttpGet("leads/filter")]
    public async Task<ActionResult<List<JobLeadDto>>> FilterLeads(
        [FromQuery] int? minScore,
        [FromQuery] bool directOnly = true,
        [FromQuery] string? search = null,
        [FromQuery] string? category = null,
        [FromQuery] string? companyType = null,
        CancellationToken cancellationToken = default)
    {
        var filter = new JobFilter
        {
            MinScore = minScore,
            DirectOnly = directOnly,
            Search = search,
            Category = category,
            CompanyType = companyType
        };

        var jobs = await processingService.GetLeadsAsync(filter, cancellationToken);
        return Ok(jobs.Select(x => new JobLeadDto(
            x.Id,
            x.Title,
            x.Company,
            x.Location,
            x.Category,
            x.OpportunityScore,
            x.CompanyType,
            x.Url,
            x.CapturedAt)).ToList());
    }

    [HttpGet("processed")]
    public async Task<ActionResult<List<JobFullListDto>>> GetProcessed(CancellationToken cancellationToken)
    {
        var jobs = await processingService.GetProcessedAsync(cancellationToken);
        return Ok(jobs.Select(x => new JobFullListDto(
            x.Id,
            x.ExternalId,
            x.Title,
            x.Company,
            x.Location,
            x.Description,
            x.Category,
            x.OpportunityScore,
            x.IsConsultingCompany,
            x.CompanyType,
            x.Url,
            x.Source,
            x.SearchTerm,
            x.CapturedAt,
            x.MetadataJson)).ToList());
    }

    [HttpGet("unprocessed")]
    public async Task<ActionResult<List<JobFullListDto>>> GetUnprocessed(CancellationToken cancellationToken)
    {
        var jobs = await processingService.GetUnprocessedAsync(cancellationToken);
        return Ok(jobs.Select(x => new JobFullListDto(
            x.Id,
            x.ExternalId,
            x.Title,
            x.Company,
            x.Location,
            x.Description,
            x.Category,
            x.OpportunityScore,
            x.IsConsultingCompany,
            x.CompanyType,
            x.Url,
            x.Source,
            x.SearchTerm,
            x.CapturedAt,
            x.MetadataJson)).ToList());
    }

    [HttpPost("process")]
    public async Task<IActionResult> Process(CancellationToken cancellationToken)
    {
        var processed = await processingService.ProcessUnprocessedJobsAsync(cancellationToken, processAll: true);
        return Ok(new { message = "Processing triggered", processedCount = processed });
    }

    private static string BuildDescriptionPreview(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return string.Empty;
        }

        var normalized = description.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        if (normalized.Length <= 280)
        {
            return normalized;
        }

        return normalized[..280].TrimEnd() + "...";
    }
}
