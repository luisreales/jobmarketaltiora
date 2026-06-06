using backend.Application.Contracts;
using backend.Application.Interfaces;
using backend.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Route("api/technologies")]
public class TechnologiesController(
    ApplicationDbContext db,
    ITechnologyIntelligenceService intelligenceService,
    ILogger<TechnologiesController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResultDto<TechnologyDto>>> GetTechnologies(
        [FromQuery] TechQueryRequest query,
        CancellationToken ct)
    {
        var page     = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var q = db.Technologies.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToUpperInvariant();
            q = q.Where(t => t.Name.Contains(term) || t.DisplayName.ToUpper().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
            q = q.Where(t => t.Category == query.Category);

        if (!string.IsNullOrWhiteSpace(query.LifecycleStage))
            q = q.Where(t => t.LifecycleStage == query.LifecycleStage);

        if (query.IsAiRelated.HasValue)
            q = q.Where(t => t.IsAiRelated == query.IsAiRelated.Value);

        q = query.SortBy switch
        {
            "momentum"      => q.OrderByDescending(t => t.MomentumScore),
            "emerging"      => q.OrderByDescending(t => t.EmergingScore),
            "mentions"      => q.OrderByDescending(t => t.TotalMentions),
            "opportunity"   => q.OrderByDescending(t => t.OpportunityScore),
            "growth"        => q.OrderByDescending(t => t.GrowthRate),
            _               => q.OrderByDescending(t => t.DemandScore)
        };

        var totalCount = await q.CountAsync(ct);
        var items = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => ToDto(t))
            .ToListAsync(ct);

        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        return Ok(new PagedResultDto<TechnologyDto>(items, page, pageSize, totalCount, totalPages, query.SortBy, "desc"));
    }

    [HttpGet("trending")]
    public async Task<ActionResult<List<TechnologyDto>>> GetTrending(CancellationToken ct)
    {
        var items = await db.Technologies
            .AsNoTracking()
            .Where(t => t.TotalMentions >= 2)
            .OrderByDescending(t => t.MomentumScore)
            .Take(20)
            .Select(t => ToDto(t))
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpGet("emerging")]
    public async Task<ActionResult<List<TechnologyDto>>> GetEmerging(CancellationToken ct)
    {
        var items = await db.Technologies
            .AsNoTracking()
            .Where(t => t.LifecycleStage == "Emerging" || t.EmergingScore > 20)
            .OrderByDescending(t => t.EmergingScore)
            .Take(20)
            .Select(t => ToDto(t))
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpGet("declining")]
    public async Task<ActionResult<List<TechnologyDto>>> GetDeclining(CancellationToken ct)
    {
        var items = await db.Technologies
            .AsNoTracking()
            .Where(t => t.MomentumScore < -10 && t.TotalMentions >= 3)
            .OrderBy(t => t.MomentumScore)
            .Take(20)
            .Select(t => ToDto(t))
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpGet("ai")]
    public async Task<ActionResult<List<TechnologyDto>>> GetAiTechnologies(CancellationToken ct)
    {
        var items = await db.Technologies
            .AsNoTracking()
            .Where(t => t.IsAiRelated)
            .OrderByDescending(t => t.MomentumScore)
            .Select(t => ToDto(t))
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpGet("graph")]
    public async Task<ActionResult<TechnologyGraphDto>> GetGraph(CancellationToken ct)
    {
        var nodes = await db.Technologies
            .AsNoTracking()
            .Where(t => t.TotalMentions >= 2)
            .Select(t => new TechGraphNode(
                t.Id, t.Name, t.DisplayName, t.Category, t.LifecycleStage,
                t.TotalMentions, t.OpportunityScore, t.IsAiRelated))
            .ToListAsync(ct);

        var nodeIds = nodes.Select(n => n.Id).ToHashSet();

        var edges = await db.TechnologyRelationships
            .AsNoTracking()
            .Where(r => r.CoOccurrenceCount >= 3
                     && nodeIds.Contains(r.SourceTechnologyId)
                     && nodeIds.Contains(r.TargetTechnologyId))
            .Select(r => new TechGraphEdge(
                r.SourceTechnologyId, r.TargetTechnologyId,
                r.CoOccurrenceCount, r.CorrelationScore))
            .ToListAsync(ct);

        return Ok(new TechnologyGraphDto(nodes, edges));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TechnologyDetailDto>> GetById(int id, CancellationToken ct)
    {
        var tech = await db.Technologies
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (tech is null)
            return NotFound(new { message = $"Technology {id} not found." });

        var rels = await db.TechnologyRelationships
            .AsNoTracking()
            .Where(r => r.SourceTechnologyId == id || r.TargetTechnologyId == id)
            .OrderByDescending(r => r.CorrelationScore)
            .Take(10)
            .Select(r => new
            {
                OtherId = r.SourceTechnologyId == id ? r.TargetTechnologyId : r.SourceTechnologyId,
                r.CoOccurrenceCount, r.CorrelationScore, r.IndustryAffinity, r.AiAffinity
            })
            .ToListAsync(ct);

        var otherIds = rels.Select(r => r.OtherId).ToList();
        var others = await db.Technologies
            .AsNoTracking()
            .Where(t => otherIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t, ct);

        var relationships = rels
            .Where(r => others.ContainsKey(r.OtherId))
            .Select(r =>
            {
                var o = others[r.OtherId];
                return new TechnologyRelationshipDto(
                    o.Id, o.Name, o.DisplayName, o.Category,
                    r.CoOccurrenceCount, r.CorrelationScore,
                    r.IndustryAffinity, r.AiAffinity);
            })
            .ToList();

        return Ok(new TechnologyDetailDto(
            tech.Id, tech.Name, tech.DisplayName, tech.Category, tech.LifecycleStage,
            tech.TotalMentions, tech.WeeklyMentions, tech.GrowthRate, tech.MomentumScore,
            tech.DemandScore, tech.CompetitionScore, tech.OpportunityScore, tech.EmergingScore,
            tech.IndustryCoverageCount, tech.ClusterCoverageCount, tech.IsAiRelated,
            tech.IsCloudRelated, tech.IsLegacy, tech.AvgLeadScore, tech.AvgUrgency,
            tech.FirstSeenAt, tech.LastSeenAt, tech.UpdatedAt, relationships));
    }

    [HttpGet("/api/trends/industries")]
    public async Task<ActionResult<List<IndustryTechDto>>> GetIndustries(CancellationToken ct)
    {
        // Load all techs and their relationship data to approximate per-industry distribution
        // We use IndustryAffinity from relationships as a proxy
        var affinities = await db.TechnologyRelationships
            .AsNoTracking()
            .Where(r => r.IndustryAffinity != "Unknown")
            .GroupBy(r => r.IndustryAffinity)
            .Select(g => new { Industry = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .Take(10)
            .ToListAsync(ct);

        var result = new List<IndustryTechDto>();

        foreach (var aff in affinities)
        {
            var techIds = await db.TechnologyRelationships
                .AsNoTracking()
                .Where(r => r.IndustryAffinity == aff.Industry)
                .GroupBy(r => r.SourceTechnologyId)
                .OrderByDescending(g => g.Sum(r => r.CoOccurrenceCount))
                .Take(5)
                .Select(g => g.Key)
                .ToListAsync(ct);

            var techs = await db.Technologies
                .AsNoTracking()
                .Where(t => techIds.Contains(t.Id))
                .Select(t => ToDto(t))
                .ToListAsync(ct);

            result.Add(new IndustryTechDto(aff.Industry, techs));
        }

        return Ok(result);
    }

    [HttpPost("rebuild")]
    public async Task<ActionResult<TechRebuildResultDto>> Rebuild(CancellationToken ct)
    {
        logger.LogInformation("TechnologiesController: manual rebuild triggered.");
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, cts.Token);
        var result = await intelligenceService.RebuildAsync(linked.Token);
        return Ok(result);
    }

    private static TechnologyDto ToDto(backend.Domain.Entities.Technology t) => new(
        t.Id, t.Name, t.DisplayName, t.Category, t.LifecycleStage,
        t.TotalMentions, t.WeeklyMentions, t.GrowthRate, t.MomentumScore,
        t.DemandScore, t.CompetitionScore, t.OpportunityScore, t.EmergingScore,
        t.IndustryCoverageCount, t.ClusterCoverageCount, t.IsAiRelated,
        t.IsCloudRelated, t.IsLegacy, t.AvgLeadScore, t.AvgUrgency,
        t.FirstSeenAt, t.LastSeenAt, t.UpdatedAt);
}
