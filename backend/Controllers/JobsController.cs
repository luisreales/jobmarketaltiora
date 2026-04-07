using backend.Application.Contracts;
using backend.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/jobs")]
[Route("api/linkedin")]
public class JobsController(IJobOrchestrator orchestrator) : ControllerBase
{
    [HttpPost("search/scrape")]
    public async Task<ActionResult<JobSearchResponse>> Scrape(
        [FromBody] JobSearchRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var (savedCount, totalFound) = await orchestrator.SearchAndSaveAsync(
                request.Query,
                request.Location,
                request.Limit,
                request.Providers,
                request.TotalPaging,
                request.StartPage,
                request.EndPage,
                cancellationToken);

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

    [HttpGet("jobs")]
    public async Task<ActionResult<List<JobSummaryDto>>> GetJobs(CancellationToken cancellationToken)
    {
        var jobs = await orchestrator.GetJobsAsync(cancellationToken);
        return Ok(jobs.Select(x => new JobSummaryDto(
            x.Id,
            x.Title,
            x.Company,
            x.Location,
            BuildDescriptionPreview(x.Description),
            x.Source,
            x.SearchTerm,
            x.CapturedAt,
            x.PublishedAt,
            x.SalaryRange,
            x.Seniority,
            x.ContractType,
            x.Url)).ToList());
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
