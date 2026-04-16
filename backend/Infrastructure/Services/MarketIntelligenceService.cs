using backend.Application.Contracts;
using backend.Application.Interfaces;
using backend.Domain.Entities;
using backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace backend.Infrastructure.Services;

public sealed class MarketIntelligenceService(
    ApplicationDbContext dbContext,
    IAiEnrichmentService aiEnrichmentService,
    LeadScoringService leadScoringService,
    IConfiguration configuration,
    ILogger<MarketIntelligenceService> logger) : IMarketIntelligenceService
{
    public async Task<int> ProcessPendingJobsAsync(CancellationToken cancellationToken = default, int? batchSize = null)
    {
        var batchTimer = Stopwatch.StartNew();
        var resolvedBatchSize = Math.Clamp(batchSize ?? configuration.GetValue<int?>("Jobs:MarketIntelligence:BatchSize") ?? 50, 5, 200);

        var pendingJobs = await dbContext.JobOffers
            .AsNoTracking()
            .Where(job => !dbContext.Set<JobInsight>().Any(insight => insight.JobId == job.Id))
            .OrderByDescending(job => job.CapturedAt)
            .Take(resolvedBatchSize)
            .ToListAsync(cancellationToken);

        if (pendingJobs.Count == 0)
        {
            logger.LogDebug(
                "Market pipeline stage={Stage} processed={Processed} pending={Pending} latencyMs={LatencyMs}",
                "batch-empty",
                0,
                0,
                batchTimer.ElapsedMilliseconds);
            return 0;
        }

        logger.LogInformation(
            "Market pipeline stage={Stage} pending={Pending} batchSize={BatchSize}",
            "batch-start",
            pendingJobs.Count,
            resolvedBatchSize);

        var processed = 0;
        foreach (var job in pendingJobs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var itemTimer = Stopwatch.StartNew();

            var alreadyExists = await dbContext.Set<JobInsight>()
                .AsNoTracking()
                .AnyAsync(insight => insight.JobId == job.Id, cancellationToken);
            if (alreadyExists)
            {
                logger.LogDebug(
                    "Market pipeline stage={Stage} jobId={JobId} status={Status} latencyMs={LatencyMs}",
                    "dedupe-check",
                    job.Id,
                    "SkippedExisting",
                    itemTimer.ElapsedMilliseconds);
                continue;
            }

            try
            {
                var analysis = await aiEnrichmentService.AnalyzeJobAsync(job, cancellationToken);

                logger.LogInformation(
                    "Market pipeline stage={Stage} jobId={JobId} decisionSource={DecisionSource} confidence={Confidence} status={Status}",
                    "analyze",
                    job.Id,
                    analysis.DecisionSource,
                    analysis.ConfidenceScore,
                    analysis.Status);

                var insight = new JobInsight
                {
                    JobId = job.Id,
                    MainPainPoint = analysis.MainPainPoint,
                    PainCategory = analysis.PainCategory,
                    PainDescription = analysis.PainDescription,
                    TechStack = analysis.TechStack,
                    IsDirectClient = analysis.IsDirectClient,
                    CompanyType = analysis.CompanyType,
                    OpportunityScore = analysis.OpportunityScore,
                    UrgencyScore = analysis.UrgencyScore,
                    SuggestedSolution = analysis.SuggestedSolution,
                    LeadMessage = analysis.LeadMessage,
                    ConfidenceScore = analysis.ConfidenceScore,
                    DecisionSource = analysis.DecisionSource,
                    Status = analysis.Status,
                    RawModelResponse = analysis.RawModelResponse,
                    IsProcessed = true,
                    ProcessedAt = DateTime.UtcNow,
                    EngineVersion = "rules-v2",
                    // Fase 0 — Data Quality Layer
                    Industry = analysis.Industry,
                    NormalizedTechStack = analysis.NormalizedTechStack,
                    TechTokensJson = analysis.TechTokensJson,
                };

                // LeadScore needs the insight partially built (OpportunityScore, UrgencyScore, IsDirectClient)
                // and the job CapturedAt for recency — compute after fields are set.
                insight.LeadScore = leadScoringService.Calculate(insight, job.CapturedAt);

                dbContext.Set<JobInsight>().Add(insight);
                await dbContext.SaveChangesAsync(cancellationToken);
                processed++;

                logger.LogInformation(
                    "Market pipeline stage={Stage} jobId={JobId} status={Status} latencyMs={LatencyMs} decisionSource={DecisionSource} confidence={Confidence} fallback={Fallback}",
                    "persist",
                    job.Id,
                    "Processed",
                    itemTimer.ElapsedMilliseconds,
                    analysis.DecisionSource,
                    analysis.ConfidenceScore,
                    false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Market pipeline stage={Stage} jobId={JobId} status={Status} latencyMs={LatencyMs}",
                    "failed",
                    job.Id,
                    "Failed",
                    itemTimer.ElapsedMilliseconds);
            }
        }

        logger.LogInformation(
            "Market pipeline stage={Stage} processed={Processed} pending={Pending} latencyMs={LatencyMs}",
            "batch-end",
            processed,
            pendingJobs.Count,
            batchTimer.ElapsedMilliseconds);

        return processed;
    }

    /// <summary>
    /// Re-enriches JobInsights that are missing Fase 0 fields.
    /// Detects stale records by checking NormalizedTechStack = "Unknown" OR Industry = "Unknown".
    /// Called automatically by Worker 1 after each normal batch cycle.
    /// Safe to run repeatedly — only touches records that still need enrichment.
    /// </summary>
    public async Task<int> ReenrichStaleInsightsAsync(int? batchSize = null, CancellationToken cancellationToken = default)
    {
        var resolvedBatch = Math.Clamp(batchSize ?? configuration.GetValue<int?>("Jobs:MarketIntelligence:BatchSize") ?? 50, 5, 200);

        var stale = await dbContext.JobInsights
            .Include(i => i.Job)
            .Where(i => i.IsProcessed
                     && i.Job != null
                     && (i.NormalizedTechStack == "Unknown" || i.Industry == "Unknown"))
            .OrderBy(i => i.Id)
            .Take(resolvedBatch)
            .ToListAsync(cancellationToken);

        if (stale.Count == 0)
        {
            return 0;
        }

        logger.LogInformation("Market pipeline stage={Stage} stale={Count}", "reenrich-start", stale.Count);

        var timer = Stopwatch.StartNew();
        var updated = 0;

        // We need JobPreprocessorService and LeadScoringService — resolve from the service collection
        // via the injected dbContext scope (they are scoped services registered in DI).
        // Since MarketIntelligenceService itself is Scoped, we can inject them directly.
        // They are already injected: leadScoringService. We need preprocessor too.
        // For now: use the same approach as in BackfillInsights endpoint — re-run Fase 0 services.
        // NOTE: JobPreprocessorService is injected into RuleBasedAiEnrichmentService, but we
        // need it here too. We add it to the constructor below.

        foreach (var insight in stale)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var analysis = await aiEnrichmentService.AnalyzeJobAsync(insight.Job!, cancellationToken);

                insight.Industry            = analysis.Industry;
                insight.NormalizedTechStack = analysis.NormalizedTechStack;
                insight.TechTokensJson      = analysis.TechTokensJson;
                insight.LeadScore           = leadScoringService.Calculate(insight, insight.Job!.CapturedAt);
                insight.IsDirectClient      = analysis.IsDirectClient;
                insight.CompanyType         = analysis.CompanyType;
                insight.EngineVersion       = "rules-v2";

                updated++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Market pipeline stage={Stage} insightId={Id} status=Failed", "reenrich", insight.Id);
            }

            if (updated % 50 == 0 && updated > 0)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Market pipeline stage={Stage} updated={Updated} latencyMs={LatencyMs}",
            "reenrich-end", updated, timer.ElapsedMilliseconds);

        return updated;
    }

    public async Task<PagedResultDto<MarketOpportunityDto>> GetOpportunitiesAsync(MarketOpportunityQuery query, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 50);

        var baseQuery = BuildInsightsQuery(query.FromDate, query.ToDate, query.Source)
            .Where(x => !query.MinOpportunityScore.HasValue || x.OpportunityScore >= query.MinOpportunityScore.Value)
            .Where(x => !query.MinUrgencyScore.HasValue || x.UrgencyScore >= query.MinUrgencyScore.Value);

        var groupedQuery = baseQuery
            .GroupBy(x => new { x.MainPainPoint, x.PainCategory })
            .Select(g => new
            {
                g.Key.MainPainPoint,
                g.Key.PainCategory,
                OpportunityCount = g.Count(),
                AvgOpportunityScore = g.Average(x => x.OpportunityScore),
                AvgUrgencyScore = g.Average(x => x.UrgencyScore),
                TopTechStack = g.Select(x => x.TechStack).FirstOrDefault(),
                SuggestedMvp = g.Select(x => x.SuggestedSolution).FirstOrDefault()
            });

        var totalCount = await groupedQuery.CountAsync(cancellationToken);

        var items = await groupedQuery
            .OrderByDescending(g => g.OpportunityCount)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(g => new MarketOpportunityDto(
                g.MainPainPoint,
                g.PainCategory,
                g.OpportunityCount,
                g.AvgOpportunityScore,
                g.AvgUrgencyScore,
                g.TopTechStack ?? "Unknown",
                g.SuggestedMvp ?? "N/A"))
            .ToListAsync(cancellationToken);

        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        return new PagedResultDto<MarketOpportunityDto>(items, page, pageSize, totalCount, totalPages, "opportunityCount", "desc");
    }

    public async Task<PagedResultDto<MarketLeadDto>> GetLeadsAsync(MarketLeadsQuery query, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 50);

        var joined = BuildInsightsQuery(null, null, query.Source)
            .Where(x => x.Job != null)
            .Where(x => string.IsNullOrWhiteSpace(query.PainPoint) || x.MainPainPoint == query.PainPoint)
            .Where(x => !query.MinScore.HasValue || x.OpportunityScore >= query.MinScore.Value);

        var totalCount = await joined.CountAsync(cancellationToken);

        var leads = await joined
            .OrderByDescending(x => x.OpportunityScore)
            .ThenByDescending(x => x.Job!.CapturedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new MarketLeadDto(
                x.Job!.Id,
                x.Job.Company,
                x.Job.Title,
                x.MainPainPoint,
                x.OpportunityScore,
                x.UrgencyScore,
                x.SuggestedSolution,
                x.LeadMessage,
                x.Job.Source,
                x.Job.Url,
                x.Job.CapturedAt))
            .ToListAsync(cancellationToken);

        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        return new PagedResultDto<MarketLeadDto>(leads, page, pageSize, totalCount, totalPages, "opportunityScore", "desc");
    }

    public async Task<IReadOnlyCollection<MarketTrendDto>> GetTrendsAsync(MarketTrendsQuery query, CancellationToken cancellationToken = default)
    {
        var windowDays = Math.Clamp(query.WindowDays, 1, 90);
        var now = DateTime.UtcNow;
        var currentStart = now.AddDays(-windowDays);
        var previousStart = currentStart.AddDays(-windowDays);

        var baseQuery = BuildInsightsQuery(previousStart, now, query.Source);

        var grouped = await baseQuery
            .GroupBy(x => x.PainCategory)
            .Select(g => new
            {
                PainCategory = g.Key,
                CurrentCount = g.Count(x => x.ProcessedAt >= currentStart),
                PreviousCount = g.Count(x => x.ProcessedAt >= previousStart && x.ProcessedAt < currentStart)
            })
            .OrderByDescending(x => x.CurrentCount)
            .Take(20)
            .ToListAsync(cancellationToken);

        return grouped
            .Select(x =>
            {
                var trend = x.PreviousCount == 0
                    ? (x.CurrentCount > 0 ? 100 : 0)
                    : ((x.CurrentCount - x.PreviousCount) / (double)x.PreviousCount) * 100;

                return new MarketTrendDto(x.PainCategory, x.CurrentCount, x.PreviousCount, Math.Round(trend, 2));
            })
            .ToList();
    }

    private IQueryable<JobInsight> BuildInsightsQuery(DateTime? fromDate, DateTime? toDate, string? source)
    {
        var query = dbContext.Set<JobInsight>()
            .AsNoTracking()
            .Where(x => x.IsProcessed);

        if (fromDate.HasValue)
        {
            query = query.Where(x => x.ProcessedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(x => x.ProcessedAt <= toDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(source))
        {
            var normalizedSource = source.Trim();
            query = query.Where(x => x.Job != null && x.Job.Source == normalizedSource);
        }

        return query;
    }
}
