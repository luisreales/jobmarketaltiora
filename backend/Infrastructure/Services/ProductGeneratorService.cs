using System.Text.Json;
using backend.Application.Interfaces;
using backend.Domain.Entities;
using backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace backend.Infrastructure.Services;

/// <summary>
/// Converts actionable MarketClusters into consolidated, sellable ProductSuggestions.
/// Rule-based — no LLM. Groups clusters by ProductName (1 row per unique product).
/// Called automatically by ClusteringHostedService after each rebuild cycle.
/// </summary>
public sealed class ProductGeneratorService(
    ApplicationDbContext dbContext,
    ILogger<ProductGeneratorService> logger) : IProductGeneratorService
{
    public async Task<int> GenerateProductsAsync(CancellationToken cancellationToken = default)
    {
        var actionableClusters = await dbContext.MarketClusters
            .AsNoTracking()
            .Where(c => c.IsActionable)
            .OrderByDescending(c => c.PriorityScore)
            .ToListAsync(cancellationToken);

        if (actionableClusters.Count == 0)
        {
            logger.LogDebug("ProductGeneratorService: no actionable clusters — skipping.");
            return 0;
        }

        logger.LogInformation("ProductGeneratorService: {Count} actionable clusters to process.", actionableClusters.Count);

        // Map each cluster to a catalog entry (skip those with no match — shouldn't happen due to catch-all)
        var assignments = actionableClusters
            .Select(c => (Cluster: c, Entry: ProductCatalog.FindBestMatch(c)))
            .Where(x => x.Entry is not null)
            .ToList();

        // Group by ProductName → 1 ProductSuggestion per unique product
        var groups = assignments
            .GroupBy(x => x.Entry!.ProductName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        logger.LogInformation("ProductGeneratorService: {Groups} product groups from {Clusters} clusters.",
            groups.Count, assignments.Count);

        // Load existing suggestions keyed by ProductName for upsert
        var existingByName = await dbContext.ProductSuggestions
            .ToDictionaryAsync(p => p.ProductName, p => p, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var activeProductNames = groups.Select(g => g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Remove suggestions for products no longer in the active catalog
        var orphans = existingByName.Values
            .Where(p => !activeProductNames.Contains(p.ProductName))
            .ToList();

        if (orphans.Count > 0)
        {
            dbContext.ProductSuggestions.RemoveRange(orphans);
            logger.LogInformation("ProductGeneratorService: removing {Count} orphaned products.", orphans.Count);
        }

        var now = DateTime.UtcNow;
        var upserted = 0;

        foreach (var group in groups)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var productName = group.Key;
            var entry       = group.First().Entry!;
            var members     = group.Select(x => x.Cluster).ToList();

            // Aggregate metrics across all clusters in the group
            var totalJobCount        = members.Sum(c => c.JobCount);
            var totalWeight          = (double)totalJobCount;
            var avgDirectRatio       = totalWeight > 0
                ? members.Sum(c => c.DirectClientRatio * c.JobCount) / totalWeight
                : members.Average(c => c.DirectClientRatio);
            var avgUrgency           = totalWeight > 0
                ? members.Sum(c => c.AvgUrgencyScore * c.JobCount) / totalWeight
                : members.Average(c => c.AvgUrgencyScore);
            var topBlueOcean         = members.Max(c => c.BlueOceanScore);
            var priorityScore        = members.Max(c => c.PriorityScore);
            var opportunityType      = MostFrequent(members.Select(c => c.OpportunityType));
            var industry             = MostFrequent(members.Select(c => c.Industry));
            var clusterIds           = members.Select(c => c.Id).ToList();
            var clusterIdsJson       = JsonSerializer.Serialize(clusterIds);
            var techFocus            = BuildTechFocus(members);
            var whyNow               = $"{totalJobCount} empresas · {avgDirectRatio:P0} directo · urgencia {avgUrgency:F1}/10";
            var midpoint             = (entry.MinDealSizeUsd + entry.MaxDealSizeUsd) / 2;
            var offer                = $"Sprint de {entry.EstimatedBuildDays} días · ${midpoint:N0}";

            if (existingByName.TryGetValue(productName, out var existing))
            {
                existing.ClusterIdsJson      = clusterIdsJson;
                existing.ProductDescription  = entry.ProductDescription;
                existing.WhyNow              = whyNow;
                existing.Offer               = offer;
                existing.ActionToday         = entry.ActionToday;
                existing.TechFocus           = techFocus;
                existing.EstimatedBuildDays  = entry.EstimatedBuildDays;
                existing.MinDealSizeUsd      = entry.MinDealSizeUsd;
                existing.MaxDealSizeUsd      = entry.MaxDealSizeUsd;
                existing.TotalJobCount       = totalJobCount;
                existing.AvgDirectClientRatio = avgDirectRatio;
                existing.AvgUrgencyScore     = avgUrgency;
                existing.TopBlueOceanScore   = topBlueOcean;
                existing.ClusterCount        = members.Count;
                existing.PriorityScore       = priorityScore;
                existing.OpportunityType     = opportunityType;
                existing.Industry            = industry;
                existing.GeneratedAt         = now;
            }
            else
            {
                var suggestion = new ProductSuggestion
                {
                    ProductName          = productName,
                    ClusterIdsJson       = clusterIdsJson,
                    ProductDescription   = entry.ProductDescription,
                    WhyNow               = whyNow,
                    Offer                = offer,
                    ActionToday          = entry.ActionToday,
                    TechFocus            = techFocus,
                    EstimatedBuildDays   = entry.EstimatedBuildDays,
                    MinDealSizeUsd       = entry.MinDealSizeUsd,
                    MaxDealSizeUsd       = entry.MaxDealSizeUsd,
                    TotalJobCount        = totalJobCount,
                    AvgDirectClientRatio = avgDirectRatio,
                    AvgUrgencyScore      = avgUrgency,
                    TopBlueOceanScore    = topBlueOcean,
                    ClusterCount         = members.Count,
                    PriorityScore        = priorityScore,
                    OpportunityType      = opportunityType,
                    Industry             = industry,
                    LlmStatus            = "pending",
                    GeneratedAt          = now
                };

                dbContext.ProductSuggestions.Add(suggestion);
            }

            upserted++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("ProductGeneratorService: upserted {Upserted}, removed {Orphans}.", upserted, orphans.Count);

        return upserted;
    }

    public async Task<ProductSuggestion?> GenerateForClusterAsync(int clusterId, CancellationToken cancellationToken = default)
    {
        // Re-run full generation and return the product that contains this cluster
        await GenerateProductsAsync(cancellationToken);

        return await dbContext.ProductSuggestions
            .FirstOrDefaultAsync(p => EF.Functions.Like(p.ClusterIdsJson, $"%{clusterId}%"), cancellationToken);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static string BuildTechFocus(IEnumerable<MarketCluster> clusters)
    {
        var tokens = clusters
            .SelectMany(c => c.NormalizedTechStack
                .Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(t => !t.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToArray();

        return tokens.Length > 0 ? string.Join(" + ", tokens) : "General";
    }

    private static string MostFrequent(IEnumerable<string> values) =>
        values
            .GroupBy(v => v, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key ?? "Unknown";
}
