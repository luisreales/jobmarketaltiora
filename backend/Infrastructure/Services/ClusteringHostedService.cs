using backend.Application.Interfaces;
using backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace backend.Infrastructure.Services;

/// <summary>
/// Background service that orchestrates the full cluster intelligence pipeline:
///   Stage 0  — SemanticClusterEngine.GenerateEmbeddingsAsync()  — embed insights (additive)
///   Stage 1  — ClusterEngine.RebuildClustersAsync()             — SHA256 clustering + BlueOceanScore
///   Stage 2  — DecisionEngine.EvaluateClustersAsync()           — OpportunityType + IsActionable
///   Stage 2b — SemanticClusterEngine.AssignSemanticGroupsAsync()— semantic group keys (additive)
///   Stage 3  — OpportunityEngineV2.EnrichClustersAsync()        — TAM, BuyingIntent, SalesAngle, etc.
///   Stage 4  — ProductGeneratorService.GenerateProductsAsync()  — rule-based product consolidation
///   Stage 5  — ClusterSynthesisService.SynthesizePendingClustersAsync() — LLM synthesis (batch 5)
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
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasNewInsights = await dbContext.JobInsights
            .AsNoTracking()
            .AnyAsync(i => i.IsProcessed && i.ProcessedAt > _lastRunAt, cancellationToken);

        if (!hasNewInsights)
        {
            logger.LogDebug("ClusteringHostedService: no new insights since {LastRun} — skipping cycle.", _lastRunAt);
            return;
        }

        logger.LogInformation("ClusteringHostedService: starting pipeline cycle. LastRun={LastRun}", _lastRunAt);

        // Stage 0 — Semantic embeddings (additive, falls back silently if SK not configured)
        var semanticEngine = scope.ServiceProvider.GetRequiredService<ISemanticClusterEngine>();
        var embedded = await semanticEngine.GenerateEmbeddingsAsync(cancellationToken);
        logger.LogInformation("ClusteringHostedService stage=SemanticEmbeddings embedded={Count}", embedded);

        // Stage 1 — Cluster Engine
        var clusterEngine = scope.ServiceProvider.GetRequiredService<IClusterEngine>();
        var clustersUpdated = await clusterEngine.RebuildClustersAsync(cancellationToken);

        logger.LogInformation("ClusteringHostedService stage=ClusterEngine clustersUpdated={Count}", clustersUpdated);

        // Stage 2 — Decision Engine (Fase 6)
        var decisionEngine = scope.ServiceProvider.GetRequiredService<IDecisionEngine>();
        var evaluated = await decisionEngine.EvaluateClustersAsync(cancellationToken);

        logger.LogInformation("ClusteringHostedService stage=DecisionEngine evaluated={Count}", evaluated);

        // Stage 2b — Semantic group assignment (additive, depends on embeddings from Stage 0)
        var semanticGrouped = await semanticEngine.AssignSemanticGroupsAsync(ct: cancellationToken);
        logger.LogInformation("ClusteringHostedService stage=SemanticGroups grouped={Count}", semanticGrouped);

        // Stage 3 — Opportunity Engine V2 (commercial intelligence enrichment)
        var opportunityEngineV2 = scope.ServiceProvider.GetRequiredService<IOpportunityEngineV2>();
        var enriched = await opportunityEngineV2.EnrichClustersAsync(cancellationToken);

        logger.LogInformation("ClusteringHostedService stage=OpportunityEngineV2 enriched={Count}", enriched);

        // Stage 4 — Product Generator (rule-based, no LLM)
        var productGenerator = scope.ServiceProvider.GetRequiredService<IProductGeneratorService>();
        var productsGenerated = await productGenerator.GenerateProductsAsync(cancellationToken);

        logger.LogInformation("ClusteringHostedService stage=ProductGenerator productsGenerated={Count}", productsGenerated);

        // Stage 5 — LLM Synthesis (batch, up to 5 actionable pending clusters per cycle)
        var synthesisService = scope.ServiceProvider.GetRequiredService<IClusterSynthesisService>();
        await synthesisService.SynthesizePendingClustersAsync(cancellationToken);

        logger.LogInformation("ClusteringHostedService stage=LLMSynthesis completed.");

        // Stage 6 — Technology Intelligence rebuild (keeps tech scores current so Stage 3
        // momentum boost uses fresh lifecycle signals on the next cycle)
        var techIntelSvc = scope.ServiceProvider.GetRequiredService<ITechnologyIntelligenceService>();
        await techIntelSvc.RebuildAsync(cancellationToken);
        logger.LogInformation("ClusteringHostedService stage=TechnologyIntelligence completed.");

        // Stage 7 — Company Intelligence rebuild (keeps prospect list current after new jobs)
        var companyIntelSvc = scope.ServiceProvider.GetRequiredService<ICompanyIntelligenceService>();
        await companyIntelSvc.RebuildAsync(cancellationToken);
        logger.LogInformation("ClusteringHostedService stage=CompanyIntelligence completed.");

        _lastRunAt = DateTime.UtcNow;

        logger.LogInformation(
            "ClusteringHostedService: pipeline complete. clusters={Clusters} evaluated={Evaluated} enriched={Enriched} products={Products}",
            clustersUpdated,
            evaluated,
            enriched,
            productsGenerated);
    }
}
