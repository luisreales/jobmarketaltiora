using backend.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace backend.Infrastructure.Services;

/// <summary>
/// Worker 2/4 — Background service that orchestrates the cluster pipeline:
///   1. ClusterEngine.RebuildClustersAsync()       — group insights → clusters + BlueOceanScore
///   2. DecisionEngine.EvaluateClustersAsync()      — OpportunityType + IsActionable + PriorityScore
///   3. ClusterSynthesisService (Fase 3)            — LLM synthesis for actionable clusters (not yet wired)
///
/// Only runs when new JobInsights have been processed since the last cycle.
/// Interval is configurable via Jobs:Clustering:IntervalSeconds (default: 1800 = 30 minutes).
/// </summary>
public sealed class ClusteringHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<ClusteringHostedService> logger) : BackgroundService
{
    private DateTime _lastRunAt = DateTime.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = Math.Clamp(
            configuration.GetValue<int?>("Jobs:Clustering:IntervalSeconds") ?? 1800,
            60, 7200);

        var startupDelay = Math.Clamp(
            configuration.GetValue<int?>("Jobs:Clustering:StartupDelaySeconds") ?? 30,
            0, 300);

        // Stagger startup to avoid competing with the Insights worker on boot.
        // In dev this is 5s; in prod 30s.
        if (startupDelay > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(startupDelay), stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunPipelineAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "ClusteringHostedService pipeline failed. Will retry next cycle.");
            }

            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
        }
    }

    private async Task RunPipelineAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();

        // Check if there are new insights since last run to avoid wasted cycles
        var dbContext = scope.ServiceProvider.GetRequiredService<backend.Infrastructure.Data.ApplicationDbContext>();
        var hasNewInsights = await dbContext.JobInsights
            .AsNoTracking()
            .AnyAsync(i => i.IsProcessed && i.ProcessedAt > _lastRunAt, cancellationToken);

        if (!hasNewInsights)
        {
            logger.LogDebug("ClusteringHostedService: no new insights since {LastRun} — skipping cycle.", _lastRunAt);
            return;
        }

        logger.LogInformation("ClusteringHostedService: starting pipeline cycle. LastRun={LastRun}", _lastRunAt);

        // Stage 1 — Cluster Engine
        var clusterEngine = scope.ServiceProvider.GetRequiredService<IClusterEngine>();
        var clustersUpdated = await clusterEngine.RebuildClustersAsync(cancellationToken);

        logger.LogInformation("ClusteringHostedService stage=ClusterEngine clustersUpdated={Count}", clustersUpdated);

        // Stage 2 — Decision Engine (Fase 6)
        var decisionEngine = scope.ServiceProvider.GetRequiredService<IDecisionEngine>();
        var evaluated = await decisionEngine.EvaluateClustersAsync(cancellationToken);

        logger.LogInformation("ClusteringHostedService stage=DecisionEngine evaluated={Count}", evaluated);

        // Stage 3 — Product Generator (rule-based, no LLM)
        var productGenerator = scope.ServiceProvider.GetRequiredService<IProductGeneratorService>();
        var productsGenerated = await productGenerator.GenerateProductsAsync(cancellationToken);

        logger.LogInformation("ClusteringHostedService stage=ProductGenerator productsGenerated={Count}", productsGenerated);

        // Stage 4 (LLM Synthesis) is on-demand only — triggered from the UI per cluster.
        // Use POST /api/market/clusters/{id}/synthesize to generate pain/mvp/leadMessage for a specific cluster.

        _lastRunAt = DateTime.UtcNow;

        logger.LogInformation(
            "ClusteringHostedService: pipeline complete. clusters={Clusters} evaluated={Evaluated} products={Products}",
            clustersUpdated,
            evaluated,
            productsGenerated);
    }
}
