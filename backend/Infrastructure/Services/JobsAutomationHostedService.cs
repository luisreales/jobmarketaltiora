using backend.Application.Interfaces;

namespace backend.Infrastructure.Services;

public class JobsAutomationHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<JobsAutomationHostedService> logger) : BackgroundService
{
    private const string SectionName = "Jobs:Automation";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = ReadSettings();
        if (!settings.Enabled)
        {
            logger.LogInformation("Jobs automation is disabled. Set {Section}:Enabled=true to activate it.", SectionName);
            return;
        }

        if (settings.RunOnStartup)
        {
            await RunCycleAsync(settings, stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            settings = ReadSettings();
            if (!settings.Enabled)
            {
                return;
            }

            await RunCycleAsync(settings, stoppingToken);
        }
    }

    private async Task RunCycleAsync(Settings settings, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IJobOrchestrator>();

        foreach (var query in settings.Queries)
        {
            if (string.IsNullOrWhiteSpace(query.Term))
            {
                continue;
            }

            try
            {
                var (savedCount, totalFound) = await orchestrator.SearchAndSaveAsync(
                    query.Term,
                    query.Location,
                    query.Limit,
                    settings.Providers,
                    null,
                    null,
                    null,
                    cancellationToken);

                logger.LogInformation(
                    "Hourly jobs scraping done for query={Query}. found={Found}, saved={Saved}",
                    query.Term,
                    totalFound,
                    savedCount);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Hourly jobs scraping failed for query={Query}", query.Term);
            }
        }
    }

    private Settings ReadSettings()
    {
        var section = configuration.GetSection(SectionName);
        var queries = section.GetSection("Queries").Get<List<JobQuery>>() ?? [];
        var providers = configuration.GetSection("Jobs:Providers:Enabled").Get<List<string>>() ?? [];

        return new Settings(
            section.GetValue("Enabled", true),
            section.GetValue("RunOnStartup", false),
            queries,
            providers);
    }

    private sealed record Settings(bool Enabled, bool RunOnStartup, List<JobQuery> Queries, List<string> Providers);

    public sealed record JobQuery
    {
        public string Term { get; init; } = ".NET";
        public string? Location { get; init; } = "Remote";
        public int Limit { get; init; } = 20;
    }
}
