using backend.Application.Contracts;
using backend.Application.Interfaces;
using backend.Domain.Entities;
using backend.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Route("api/market/clusters")]
public class MarketClusterController(
    ApplicationDbContext dbContext,
    IClusterEngine clusterEngine,
    IDecisionEngine decisionEngine,
    IMarketIntelligenceService marketService,
    IClusterSynthesisService clusterSynthesis,
    ILogger<MarketClusterController> logger) : ControllerBase
{
    /// <summary>
    /// Returns paginated clusters ordered by PriorityScore (highest first).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResultDto<MarketClusterDto>>> GetClusters(
        [FromQuery] MarketClusterQuery query,
        CancellationToken cancellationToken)
    {
        var page     = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 50);

        var q = dbContext.MarketClusters.AsNoTracking();

        if (query.MinBlueOceanScore.HasValue)
        {
            q = q.Where(c => c.BlueOceanScore >= query.MinBlueOceanScore.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.PainCategory))
        {
            q = q.Where(c => c.PainCategory == query.PainCategory);
        }

        if (!string.IsNullOrWhiteSpace(query.Industry))
        {
            q = q.Where(c => c.Industry == query.Industry);
        }

        if (!string.IsNullOrWhiteSpace(query.OpportunityType))
        {
            q = q.Where(c => c.OpportunityType == query.OpportunityType);
        }

        if (query.IsActionable.HasValue)
        {
            q = q.Where(c => c.IsActionable == query.IsActionable.Value);
        }

        var totalCount = await q.CountAsync(cancellationToken);

        var items = await q
            .OrderByDescending(c => c.PriorityScore)
            .ThenByDescending(c => c.BlueOceanScore)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => ToDto(c))
            .ToListAsync(cancellationToken);

        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        return Ok(new PagedResultDto<MarketClusterDto>(items, page, pageSize, totalCount, totalPages, "priorityScore", "desc"));
    }

    /// <summary>
    /// Returns a single cluster by ID with full detail.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<MarketClusterDto>> GetCluster(
        int id,
        CancellationToken cancellationToken)
    {
        var cluster = await dbContext.MarketClusters
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (cluster is null)
        {
            return NotFound(new { message = $"Cluster {id} not found." });
        }

        return Ok(ToDto(cluster));
    }

    /// <summary>
    /// Returns paginated leads (JobInsights + JobOffer) belonging to a cluster,
    /// ordered by LeadScore descending (best lead first).
    /// </summary>
    [HttpGet("{id:int}/leads")]
    public async Task<ActionResult<PagedResultDto<ClusterLeadDto>>> GetClusterLeads(
        int id,
        [FromQuery] ClusterLeadsQuery query,
        CancellationToken cancellationToken)
    {
        var clusterExists = await dbContext.MarketClusters
            .AsNoTracking()
            .AnyAsync(c => c.Id == id, cancellationToken);

        if (!clusterExists)
        {
            return NotFound(new { message = $"Cluster {id} not found." });
        }

        var page     = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 50);

        var q = dbContext.JobInsights
            .AsNoTracking()
            .Include(i => i.Job)
            .Where(i => i.ClusterId == id && i.Job != null);

        if (query.MinLeadScore.HasValue)
        {
            q = q.Where(i => i.LeadScore >= query.MinLeadScore.Value);
        }

        var totalCount = await q.CountAsync(cancellationToken);

        var leads = await q
            .OrderByDescending(i => i.LeadScore)
            .ThenByDescending(i => i.OpportunityScore)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new ClusterLeadDto(
                i.Job!.Id,
                i.Job.Company,
                i.Job.Title,
                i.PainCategory,
                i.OpportunityScore,
                i.UrgencyScore,
                i.LeadScore,
                i.SuggestedSolution,
                i.LeadMessage,
                i.IsDirectClient,
                i.Job.Source,
                i.Job.Url,
                i.Job.CapturedAt))
            .ToListAsync(cancellationToken);

        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        return Ok(new PagedResultDto<ClusterLeadDto>(leads, page, pageSize, totalCount, totalPages, "leadScore", "desc"));
    }

    /// <summary>
    /// Manually triggers a full cluster pipeline rebuild:
    ///   ClusterEngine → DecisionEngine
    /// Equivalent to the scheduled ClusteringHostedService cycle.
    /// </summary>
    [HttpPost("rebuild")]
    public async Task<ActionResult<ClusterRebuildResultDto>> RebuildClusters(CancellationToken cancellationToken)
    {
        logger.LogInformation("MarketClusterController: manual rebuild triggered.");

        var clustersUpserted = await clusterEngine.RebuildClustersAsync(cancellationToken);
        var evaluated        = await decisionEngine.EvaluateClustersAsync(cancellationToken);

        var actionable = await dbContext.MarketClusters
            .AsNoTracking()
            .CountAsync(c => c.IsActionable, cancellationToken);

        logger.LogInformation(
            "MarketClusterController: rebuild complete. upserted={U} evaluated={E} actionable={A}",
            clustersUpserted, evaluated, actionable);

        return Ok(new ClusterRebuildResultDto(
            ClustersUpserted: clustersUpserted,
            ClustersEvaluated: evaluated,
            ActionableClusters: actionable,
            RanAt: DateTime.UtcNow));
    }

    /// <summary>
    /// Re-enriches existing JobInsights that were processed before Fase 0
    /// and therefore have NormalizedTechStack="Unknown" and/or Industry="Unknown".
    ///
    /// Run this ONCE after deploying Fase 0 to make the 155 legacy records
    /// cluster-ready. Safe to call again — only touches records with missing fields.
    ///
    /// Returns: { backfilled: N, skipped: M, errors: K }
    /// </summary>
    [HttpPost("backfill-insights")]
    public async Task<ActionResult<object>> BackfillInsights(CancellationToken cancellationToken)
    {
        // Delegate entirely to the service — same logic used by Worker 1 automatically.
        // Runs all stale records in one shot (no batch cap here — manual trigger).
        var pending = await dbContext.JobInsights
            .AsNoTracking()
            .CountAsync(i => i.IsProcessed
                          && i.Job != null
                          && (i.NormalizedTechStack == "Unknown" || i.Industry == "Unknown"),
                        cancellationToken);

        if (pending == 0)
        {
            return Ok(new { backfilled = 0, message = "All insights already have data quality fields populated." });
        }

        logger.LogInformation("BackfillInsights: {Pending} stale insights found — delegating to ReenrichStaleInsightsAsync.", pending);

        // Process all stale records in batches of 100 until none remain
        var total = 0;
        int processed;
        do
        {
            processed = await marketService.ReenrichStaleInsightsAsync(100, cancellationToken);
            total += processed;
        }
        while (processed > 0);

        logger.LogInformation("BackfillInsights: done. total={Total}", total);

        return Ok(new
        {
            backfilled = total,
            message = $"Re-enriched {total} insights with Industry + NormalizedTechStack + LeadScore."
        });
    }

    /// <summary>
    /// On-demand LLM synthesis for a single cluster.
    /// If LlmStatus is already "completed" returns the cached result without calling the LLM.
    /// </summary>
    [HttpPost("{id:int}/synthesize")]
    public async Task<ActionResult<MarketClusterDto>> SynthesizeCluster(int id)
    {
        logger.LogInformation("MarketClusterController: on-demand synthesis requested for cluster {Id}.", id);

        // Use an independent CancellationToken so that slow LLM calls (>30 s) are not
        // cancelled by the HTTP request timeout. The HttpClient inside SK already has
        // its own 300-second timeout configured in SemanticKernelProvider.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(310));

        var result = await clusterSynthesis.SynthesizeClusterAsync(id, cts.Token);

        if (result is null)
            return NotFound(new { message = $"Cluster {id} not found." });

        return Ok(result);
    }

    /// <summary>
    /// Deletes small clusters with low signal (JobCount &lt; maxJobCount)
    /// that do not have previous successful synthesis.
    /// </summary>
    [HttpDelete("cleanup")]
    public async Task<ActionResult<object>> CleanupClusters(
        [FromQuery] int maxJobCount = 5,
        CancellationToken cancellationToken = default)
    {
        var threshold = Math.Max(1, maxJobCount);

        var candidateIds = await dbContext.MarketClusters
            .AsNoTracking()
            .Where(c => c.JobCount < threshold)
            .Where(c => string.IsNullOrWhiteSpace(c.SynthesizedPain)
                     && string.IsNullOrWhiteSpace(c.SynthesizedMvp)
                     && string.IsNullOrWhiteSpace(c.SynthesizedLeadMessage))
            .Where(c => c.LlmStatus != "completed")
            .Where(c => !dbContext.AiPromptLogs.Any(l => l.ClusterId == c.Id && l.IsSuccess))
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        if (candidateIds.Count == 0)
        {
            return Ok(new
            {
                deleted = 0,
                inspected = 0,
                message = "No clusters matched cleanup criteria."
            });
        }

        var entitiesToDelete = await dbContext.MarketClusters
            .Where(c => candidateIds.Contains(c.Id))
            .ToListAsync(cancellationToken);

        dbContext.MarketClusters.RemoveRange(entitiesToDelete);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "MarketClusterController: cleanup removed {Deleted} cluster(s) with JobCount < {Threshold} and no synthesis history.",
            entitiesToDelete.Count,
            threshold);

        return Ok(new
        {
            deleted = entitiesToDelete.Count,
            inspected = candidateIds.Count,
            message = $"Deleted {entitiesToDelete.Count} clusters with JobCount < {threshold} and no synthesis history."
        });
    }

    // ── Projection helper (keeps Select() strongly typed) ────────────────────────

    private static MarketClusterDto ToDto(MarketCluster c) => new(
        c.Id,
        c.Label,
        c.PainCategory,
        c.Industry,
        c.CompanyType,
        c.NormalizedTechStack,
        c.JobCount,
        c.DirectClientCount,
        c.DirectClientRatio,
        c.AvgOpportunityScore,
        c.AvgUrgencyScore,
        c.GrowthRate,
        c.BlueOceanScore,
        c.RoiRank,
        c.OpportunityType,
        c.IsActionable,
        c.RecommendedStrategy,
        c.PriorityScore,
        c.SynthesizedPain,
        c.SynthesizedMvp,
        c.SynthesizedLeadMessage,
        c.MvpType,
        c.EstimatedBuildDays,
        c.EstimatedDealSizeUsd,
        c.LlmStatus,
        c.LastUpdatedAt);
}
