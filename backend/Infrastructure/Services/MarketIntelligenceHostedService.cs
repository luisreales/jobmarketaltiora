using backend.Application.Interfaces;

namespace backend.Infrastructure.Services;

public sealed class MarketIntelligenceHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    MarketIntelligenceExecutionTracker executionTracker,
    ILogger<MarketIntelligenceHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = Math.Clamp(configuration.GetValue<int?>("Jobs:MarketIntelligence:IntervalSeconds") ?? 60, 10, 600);
        var batchSize = Math.Clamp(configuration.GetValue<int?>("Jobs:MarketIntelligence:BatchSize") ?? 50, 5, 200);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                executionTracker.MarkStarted("scheduled");

                using var scope = scopeFactory.CreateScope();
                var marketService = scope.ServiceProvider.GetRequiredService<IMarketIntelligenceService>();

                // Phase 1: process new JobOffers that have no insight yet
                var processed = await marketService.ProcessPendingJobsAsync(stoppingToken, batchSize);

                // Phase 2: re-enrich existing insights missing Fase 0 fields (Industry, NormalizedTechStack, LeadScore)
                // Runs after new jobs so stale records get fixed in the same cycle without blocking new ones.
                var reenriched = await marketService.ReenrichStaleInsightsAsync(batchSize, stoppingToken);

                executionTracker.MarkCompleted(processed + reenriched);

                if (processed > 0)
                {
                    logger.LogInformation("Market intelligence processed {Processed} jobs.", processed);
                }
                else
                {
                    await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                executionTracker.MarkFailed(ex.Message);
                logger.LogError(ex, "Market intelligence worker failed. Retrying on next cycle.");
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
            }
        }
    }
}
