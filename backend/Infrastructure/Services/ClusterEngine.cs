using System.Security.Cryptography;
using System.Text;
using backend.Application.Interfaces;
using backend.Domain.Entities;
using backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace backend.Infrastructure.Services;

/// <summary>
/// Worker 2 — groups processed JobInsights into commercial MarketClusters.
///
/// Clustering key: SHA256(PainCategory|TechTop3|Industry|CompanyType)
/// This ensures ".NET+Azure Fintech DirectClient" and ".NET+Azure Fintech DirectClient"
/// from different scraping runs land in the same cluster, never creating duplicates.
///
/// BlueOceanScore v2 weights:
///   volume       * 0.30  (jobs in cluster, normalized to 0–100)
///   growth       * 0.20  (growth vs previous 7-day window, capped 0–100)
///   directRatio  * 0.20  (% of direct clients, 0–100)
///   urgency      * 0.10  (avg urgency score, normalized 0–100)
///   buyingPower  * 0.10  (industry weight, see table)
///   easeOfSale   * 0.10  (directRatio*70 + urgency*30)
/// </summary>
public sealed class ClusterEngine(
    ApplicationDbContext dbContext,
    ILogger<ClusterEngine> logger) : IClusterEngine
{
    // Volume normalization ceiling — a cluster with 50+ jobs gets volumeNorm = 100
    private const int VolumeCeiling = 50;

    // Window for growth calculation (days)
    private const int GrowthWindowDays = 7;

    public async Task<int> RebuildClustersAsync(CancellationToken cancellationToken = default)
    {
        // 1. Load all processed insights with their associated job offer
        var insights = await dbContext.JobInsights
            .AsNoTracking()
            .Where(i => i.IsProcessed)
            .Include(i => i.Job)
            .ToListAsync(cancellationToken);

        if (insights.Count == 0)
        {
            logger.LogDebug("ClusterEngine: no processed insights found — skipping rebuild.");
            return 0;
        }

        logger.LogInformation("ClusterEngine: rebuilding clusters from {Count} insights.", insights.Count);

        // 2. Group into raw clusters in memory (Signal Filter applied inside)
        var rawGroups = GroupIntoClusters(insights);

        logger.LogInformation(
            "ClusterEngine: {SignaledGroups} groups passed signal filter from {TotalInsights} insights ({Discarded} discarded).",
            rawGroups.Count, insights.Count, insights.Count - rawGroups.Sum(g => g.Value.Count));

        var now = DateTime.UtcNow;
        var previousWindowStart = now.AddDays(-(GrowthWindowDays * 2));
        var currentWindowStart  = now.AddDays(-GrowthWindowDays);

        // Pre-load counts per ClusterKey for growth calculation (avoids N+1 queries)
        var existingClusters = await dbContext.MarketClusters
            .ToDictionaryAsync(c => c.ClusterKey, c => c, cancellationToken);

        var upserted = 0;
        var insightClusterMap = new Dictionary<int, int>(); // insightId → clusterId

        foreach (var (clusterKey, members) in rawGroups)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var signals = ComputeSignals(members, currentWindowStart, previousWindowStart);
            var scores  = ComputeScores(signals, members[0].PainCategory, members[0].Industry);
            var label   = BuildLabel(members[0].PainCategory, members[0].NormalizedTechStack,
                                     members[0].Industry, signals.CompanyType);

            if (existingClusters.TryGetValue(clusterKey, out var existing))
            {
                // Update existing cluster — preserve LLM fields unless they need refresh
                existing.Label              = label;
                existing.JobCount           = signals.JobCount;
                existing.DirectClientCount  = signals.DirectClientCount;
                existing.DirectClientRatio  = signals.DirectClientRatio;
                existing.AvgOpportunityScore = signals.AvgOpportunityScore;
                existing.AvgUrgencyScore    = signals.AvgUrgencyScore;
                existing.GrowthRate         = signals.GrowthRate;
                existing.BuyingPowerScore   = scores.BuyingPower;
                existing.PainSpecificityScore = scores.PainSpecificity;
                existing.EaseOfSaleScore    = scores.EaseOfSale;
                existing.BlueOceanScore     = scores.BlueOcean;
                existing.LastUpdatedAt      = now;

                // If cluster gained >20% new jobs, reset LLM so it gets re-synthesized
                if (signals.JobCount > existing.JobCount * 1.20 && existing.LlmStatus == "done")
                {
                    existing.LlmStatus = "pending";
                }

                insightClusterMap[existing.Id] = existing.Id;
                upserted++;
            }
            else
            {
                var cluster = new MarketCluster
                {
                    ClusterKey          = clusterKey,
                    Label               = label,
                    PainCategory        = members[0].PainCategory,
                    NormalizedTechStack = members[0].NormalizedTechStack,
                    TechKeyPart         = members[0].TechTokensJson, // will be set below
                    Industry            = members[0].Industry,
                    CompanyType         = signals.CompanyType,
                    JobCount            = signals.JobCount,
                    DirectClientCount   = signals.DirectClientCount,
                    DirectClientRatio   = signals.DirectClientRatio,
                    AvgOpportunityScore = signals.AvgOpportunityScore,
                    AvgUrgencyScore     = signals.AvgUrgencyScore,
                    GrowthRate          = signals.GrowthRate,
                    BuyingPowerScore    = scores.BuyingPower,
                    PainSpecificityScore = scores.PainSpecificity,
                    EaseOfSaleScore     = scores.EaseOfSale,
                    BlueOceanScore      = scores.BlueOcean,
                    OpportunityType     = "Ignore",   // set by DecisionEngine
                    IsActionable        = false,
                    RecommendedStrategy = string.Empty,
                    PriorityScore       = 0,
                    LlmStatus           = "pending",
                    FirstSeenAt         = now,
                    LastUpdatedAt       = now,
                    EngineVersion       = "cluster-v1"
                };

                // Build TechKeyPart from the first member's tokens
                cluster.TechKeyPart = BuildTechKeyPart(members[0].NormalizedTechStack);

                dbContext.MarketClusters.Add(cluster);
                existingClusters[clusterKey] = cluster;
                upserted++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // 3. Recalculate RoiRank ordered by BlueOceanScore DESC
        await AssignRoiRanksAsync(cancellationToken);

        // 4. Update ClusterId on JobInsights
        await LinkInsightsToClustersAsync(rawGroups, existingClusters, cancellationToken);

        logger.LogInformation("ClusterEngine: upserted {Upserted} clusters.", upserted);
        return upserted;
    }

    // ── Grouping ────────────────────────────────────────────────────────────────

    private static Dictionary<string, List<JobInsight>> GroupIntoClusters(List<JobInsight> insights)
    {
        var groups = new Dictionary<string, List<JobInsight>>(StringComparer.Ordinal);

        foreach (var insight in insights)
        {
            var techKeyPart = BuildTechKeyPart(insight.NormalizedTechStack);
            var companyType = insight.IsDirectClient ? "DirectClient" : "Consulting";
            var key = ComputeClusterKey(insight.PainCategory, techKeyPart, insight.Industry, companyType);

            if (!groups.TryGetValue(key, out var list))
            {
                list = [];
                groups[key] = list;
            }

            list.Add(insight);
        }

        // ── Signal Filter ────────────────────────────────────────────────────────
        // Discard groups that don't have enough real-world signal:
        //   • At least 5 jobs
        //   • At least 3 distinct companies (not the same company posting repeatedly)
        //   • Jobs captured on at least 2 different days (not a single batch dump)
        var signaled = new Dictionary<string, List<JobInsight>>(StringComparer.Ordinal);

        foreach (var (key, members) in groups)
        {
            var uniqueCompanies = members
                .Select(m => m.Job?.Company)
                .Where(c => c is not null)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            var uniqueDays = members
                .Select(m => m.ProcessedAt.Date)
                .Distinct()
                .Count();

            if (members.Count >= 5 && uniqueCompanies >= 3 && uniqueDays >= 2)
            {
                signaled[key] = members;
            }
        }

        return signaled;
    }

    private static string BuildTechKeyPart(string normalizedTechStack)
    {
        // normalizedTechStack is already "AZURE, NET, SQL" from TechCanonicalizer
        // Extract tokens, sort, take top 3, join with pipe for key stability
        var tokens = normalizedTechStack
            .Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t != "Unknown")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t, StringComparer.Ordinal)
            .Take(3)
            .ToArray();

        return tokens.Length > 0 ? string.Join("|", tokens) : "UNKNOWN";
    }

    private static string ComputeClusterKey(string painCategory, string techKeyPart, string industry, string companyType)
    {
        var raw = $"{painCategory}|{techKeyPart}|{industry}|{companyType}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    // ── Signal computation ───────────────────────────────────────────────────────

    private static ClusterSignals ComputeSignals(
        List<JobInsight> members,
        DateTime currentWindowStart,
        DateTime previousWindowStart)
    {
        var jobCount          = members.Count;
        var directCount       = members.Count(m => m.IsDirectClient);
        var directRatio       = jobCount > 0 ? (double)directCount / jobCount : 0;
        var avgOpportunity    = members.Average(m => m.OpportunityScore);
        var avgUrgency        = members.Average(m => m.UrgencyScore);

        var currentWindow  = members.Count(m => m.ProcessedAt >= currentWindowStart);
        var previousWindow = members.Count(m => m.ProcessedAt >= previousWindowStart && m.ProcessedAt < currentWindowStart);
        var growthRate = previousWindow == 0
            ? (currentWindow > 0 ? 1.0 : 0.0)
            : (double)(currentWindow - previousWindow) / previousWindow;

        var companyType = directRatio >= 0.7 ? "DirectClient"
            : directRatio <= 0.3             ? "Consulting"
            :                                  "Mixed";

        return new ClusterSignals(
            JobCount: jobCount,
            DirectClientCount: directCount,
            DirectClientRatio: directRatio,
            AvgOpportunityScore: Math.Round(avgOpportunity, 1),
            AvgUrgencyScore: Math.Round(avgUrgency, 1),
            GrowthRate: Math.Round(growthRate, 3),
            CompanyType: companyType);
    }

    // ── Score computation ────────────────────────────────────────────────────────

    private static ClusterScores ComputeScores(ClusterSignals signals, string painCategory, string industry)
    {
        var volumeNorm    = Math.Min((double)signals.JobCount / VolumeCeiling * 100, 100);
        var growthNorm    = Math.Min(Math.Max(signals.GrowthRate * 100, 0), 100);
        var directNorm    = signals.DirectClientRatio * 100;
        var urgencyNorm   = Math.Clamp(signals.AvgUrgencyScore / 10.0 * 100, 0, 100);
        var buyingPower   = (double)IndustryClassifier.GetBuyingPower(industry);
        var painSpec      = painCategory.Equals("General", StringComparison.OrdinalIgnoreCase) ? 40.0 : 100.0;
        var easeOfSale    = Math.Clamp(directNorm * 0.70 + urgencyNorm * 0.30, 0, 100);

        var blueOcean = volumeNorm  * 0.30
                      + growthNorm  * 0.20
                      + directNorm  * 0.20
                      + urgencyNorm * 0.10
                      + buyingPower * 0.10
                      + easeOfSale  * 0.10;

        return new ClusterScores(
            BuyingPower: Math.Round(buyingPower, 1),
            PainSpecificity: Math.Round(painSpec, 1),
            EaseOfSale: Math.Round(easeOfSale, 1),
            BlueOcean: Math.Round(Math.Clamp(blueOcean, 0, 100), 1));
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static string BuildLabel(string painCategory, string techStack, string industry, string companyType)
    {
        var tech = techStack.Equals("Unknown", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : $" ({techStack})";

        var industryPart = industry.Equals("Unknown", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : $"{industry} · ";

        return $"{industryPart}{painCategory}{tech} — {companyType}";
    }

    private async Task AssignRoiRanksAsync(CancellationToken cancellationToken)
    {
        var clusters = await dbContext.MarketClusters
            .OrderByDescending(c => c.BlueOceanScore)
            .ToListAsync(cancellationToken);

        for (var i = 0; i < clusters.Count; i++)
        {
            clusters[i].RoiRank = i + 1;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task LinkInsightsToClustersAsync(
        Dictionary<string, List<JobInsight>> rawGroups,
        Dictionary<string, MarketCluster> clusterMap,
        CancellationToken cancellationToken)
    {
        // Build insightId → clusterId map
        var insightToCluster = new Dictionary<int, int>();
        foreach (var (key, members) in rawGroups)
        {
            if (!clusterMap.TryGetValue(key, out var cluster))
            {
                continue;
            }

            foreach (var insight in members)
            {
                insightToCluster[insight.Id] = cluster.Id;
            }
        }

        if (insightToCluster.Count == 0)
        {
            return;
        }

        // Batch update in chunks to avoid oversized queries
        const int chunkSize = 100;
        var insightIds = insightToCluster.Keys.ToList();

        for (var offset = 0; offset < insightIds.Count; offset += chunkSize)
        {
            var chunk = insightIds.Skip(offset).Take(chunkSize).ToList();
            var toUpdate = await dbContext.JobInsights
                .Where(i => chunk.Contains(i.Id))
                .ToListAsync(cancellationToken);

            foreach (var insight in toUpdate)
            {
                insight.ClusterId = insightToCluster[insight.Id];
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    // ── Value types ──────────────────────────────────────────────────────────────

    private sealed record ClusterSignals(
        int JobCount,
        int DirectClientCount,
        double DirectClientRatio,
        double AvgOpportunityScore,
        double AvgUrgencyScore,
        double GrowthRate,
        string CompanyType);

    private sealed record ClusterScores(
        double BuyingPower,
        double PainSpecificity,
        double EaseOfSale,
        double BlueOcean);
}
