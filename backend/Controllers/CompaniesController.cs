using System.Text.Json;
using backend.Application.Contracts;
using backend.Application.Interfaces;
using backend.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Route("api/companies")]
public class CompaniesController(
    ApplicationDbContext db,
    ICompanyIntelligenceService companySvc,
    ILogger<CompaniesController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IList<CompanyProfileDto>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? industry,
        [FromQuery] bool? directClient,
        [FromQuery] bool? hasAi,
        [FromQuery] bool? hasCloudMigration,
        [FromQuery] string sortBy = "prospectScore",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var query = db.CompanyProfiles.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.CompanyName.ToLower().Contains(search.ToLower()));

        if (!string.IsNullOrWhiteSpace(industry))
            query = query.Where(c => c.PrimaryIndustry == industry);

        if (directClient.HasValue)
            query = query.Where(c => c.IsDirectClient == directClient.Value);

        if (hasAi.HasValue)
            query = query.Where(c => c.HasAiInitiative == hasAi.Value);

        if (hasCloudMigration.HasValue)
            query = query.Where(c => c.HasCloudMigration == hasCloudMigration.Value);

        query = sortBy switch
        {
            "prospectScore" => query.OrderByDescending(c => c.ProspectScore),
            "jobCount" => query.OrderByDescending(c => c.TotalJobCount),
            "urgency" => query.OrderByDescending(c => c.AvgUrgencyScore),
            "hiringVelocity" => query.OrderByDescending(c => c.HiringVelocity),
            "lastSeen" => query.OrderByDescending(c => c.LastSeenAt),
            _ => query.OrderByDescending(c => c.ProspectScore)
        };

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var dtos = items.Select(c =>
        {
            var techStack = new List<string>();
            try { techStack = JsonSerializer.Deserialize<List<string>>(c.TechStackJson) ?? []; }
            catch { /* ignore */ }

            return new CompanyProfileDto(
                c.Id, c.CompanyName, c.CompanyType, c.PrimaryIndustry,
                techStack, c.TopPainCategory, c.TotalJobCount,
                c.AvgUrgencyScore, c.AvgOpportunityScore, c.AvgLeadScore,
                c.HiringVelocity, c.IsDirectClient, c.HasAiInitiative,
                c.HasCloudMigration, c.ProspectScore, c.FirstSeenAt, c.LastSeenAt);
        }).ToList();

        return Ok(dtos);
    }

    [HttpPost("rebuild")]
    public async Task<ActionResult<CompanyRebuildResultDto>> Rebuild(CancellationToken ct)
    {
        logger.LogInformation("CompaniesController: rebuild triggered.");
        var result = await companySvc.RebuildAsync(ct);
        return Ok(result);
    }
}
