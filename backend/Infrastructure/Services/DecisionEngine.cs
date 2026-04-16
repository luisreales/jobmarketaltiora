using backend.Application.Interfaces;
using backend.Domain.Entities;
using backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace backend.Infrastructure.Services;

/// <summary>
/// Worker 4 — Decision Engine (Fase 6).
/// Converts BlueOceanScore clusters into executable commercial decisions:
///   - OpportunityType classification (MVPProduct | QuickWin | Consulting | Ignore)
///   - IsActionable flag (gates LLM synthesis in Fase 3)
///   - RecommendedStrategy
///   - PriorityScore for the weekly dashboard ranking
///
/// Rules are deterministic — no LLM involved. The LLM only runs AFTER this stage,
/// on clusters that are already classified as actionable.
/// </summary>
public sealed class DecisionEngine(
    ApplicationDbContext dbContext,
    ILogger<DecisionEngine> logger) : IDecisionEngine
{
    public async Task<int> EvaluateClustersAsync(CancellationToken cancellationToken = default)
    {
        var clusters = await dbContext.MarketClusters
            .Where(c => c.BlueOceanScore > 0)
            .ToListAsync(cancellationToken);

        if (clusters.Count == 0)
        {
            logger.LogDebug("DecisionEngine: no clusters with BlueOceanScore found.");
            return 0;
        }

        var actionableCount = 0;
        var typeCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var cluster in clusters)
        {
            var opportunityType = ClassifyOpportunityType(cluster);
            var isActionable    = cluster.BlueOceanScore >= 40 && opportunityType != "Ignore";
            var strategy        = ResolveStrategy(opportunityType);
            var priorityScore   = ComputePriorityScore(cluster);

            cluster.OpportunityType     = opportunityType;
            cluster.IsActionable        = isActionable;
            cluster.RecommendedStrategy = strategy;
            cluster.PriorityScore       = priorityScore;

            if (isActionable)
            {
                actionableCount++;
            }

            typeCounts[opportunityType] = typeCounts.TryGetValue(opportunityType, out var c) ? c + 1 : 1;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "DecisionEngine: evaluated {Total} clusters. Actionable={Actionable}. Distribution={Distribution}",
            clusters.Count,
            actionableCount,
            string.Join(", ", typeCounts.Select(kv => $"{kv.Key}:{kv.Value}")));

        return clusters.Count;
    }

    // ── Classification rules ─────────────────────────────────────────────────────

    /// <summary>
    /// Rule-based classification in priority order.
    /// MVPProduct → QuickWin → Consulting → Ignore
    /// </summary>
    private static string ClassifyOpportunityType(MarketCluster cluster)
    {
        // MVPProduct: signal-filtered cluster with direct-client majority and positive growth.
        // Thresholds lowered because signal filter guarantees only real-signal clusters survive.
        if (cluster.JobCount >= 10
            && cluster.DirectClientRatio >= 0.60
            && cluster.GrowthRate >= 0.10)
        {
            return "MVPProduct";
        }

        // QuickWin: smaller but urgent — fast sale, quick delivery
        if (cluster.JobCount >= 5
            && cluster.AvgUrgencyScore >= 7.0)
        {
            return "QuickWin";
        }

        // Consulting: high opportunity score + direct clients — high ticket, fewer clients needed
        if (cluster.AvgOpportunityScore >= 70
            && cluster.DirectClientRatio >= 0.40)
        {
            return "Consulting";
        }

        return "Ignore";
    }

    private static string ResolveStrategy(string opportunityType) => opportunityType switch
    {
        "MVPProduct" => "Build MVP + Validate + Ads",
        "QuickWin"   => "Direct Outreach (manual)",
        "Consulting" => "High-ticket consulting outreach",
        _            => "No action"
    };

    // ── Priority score ───────────────────────────────────────────────────────────

    /// <summary>
    /// PriorityScore (0–100) — executive ranking for "what to attack this week".
    ///
    ///   BlueOceanScore      * 0.50
    ///   DirectClientRatio%  * 0.20
    ///   AvgUrgencyScore*10  * 0.20
    ///   GrowthRate%         * 0.10
    /// </summary>
    private static int ComputePriorityScore(MarketCluster cluster)
    {
        var blue        = Math.Clamp(cluster.BlueOceanScore, 0, 100);
        var direct      = Math.Clamp(cluster.DirectClientRatio * 100, 0, 100);
        var urgency     = Math.Clamp(cluster.AvgUrgencyScore * 10, 0, 100);
        var growth      = Math.Clamp(cluster.GrowthRate * 100, 0, 100);

        var score = blue * 0.50
                  + direct * 0.20
                  + urgency * 0.20
                  + growth * 0.10;

        return (int)Math.Round(Math.Clamp(score, 0, 100));
    }
}
