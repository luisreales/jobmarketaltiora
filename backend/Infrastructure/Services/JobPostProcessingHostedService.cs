using backend.Application.Interfaces;

namespace backend.Infrastructure.Services;

public sealed class JobPostProcessingHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<JobPostProcessingHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = Math.Clamp(configuration.GetValue<int?>("Jobs:PostProcessing:IntervalSeconds") ?? 20, 5, 300);
        var batchSize = Math.Clamp(configuration.GetValue<int?>("Jobs:PostProcessing:BatchSize") ?? 100, 10, 500);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var processingService = scope.ServiceProvider.GetRequiredService<IJobProcessingService>();
                var processedCount = await processingService.ProcessUnprocessedJobsAsync(stoppingToken, batchSize, processAll: false);
                if (processedCount == 0)
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
                logger.LogError(ex, "Background post-processing failed. Retrying on next cycle.");
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
            }
        }
    }
}
