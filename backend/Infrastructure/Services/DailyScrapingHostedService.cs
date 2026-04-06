using backend.Application.Interfaces;

namespace backend.Infrastructure.Services;

public class DailyScrapingHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<DailyScrapingHostedService> logger) : BackgroundService
{
    private const string SectionName = "Tracking:DailyScraping";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = ReadSettings();
        if (!settings.Enabled)
        {
            logger.LogInformation("Daily scraping monitor is disabled. Set {Section}:Enabled=true to activate it.", SectionName);
            return;
        }

        if (settings.RunOnStartup)
        {
            await RunCycleAsync(settings.Targets, stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            settings = ReadSettings();
            if (!settings.Enabled)
            {
                logger.LogInformation("Daily scraping monitor is now disabled. Stopping scheduler loop.");
                return;
            }

            var now = DateTime.UtcNow;
            var nextRunUtc = GetNextRunUtc(now, settings.RunDailyAtUtc);
            var delay = nextRunUtc - now;

            logger.LogInformation("Daily scraping next run at {NextRunUtc} for {TargetCount} targets", nextRunUtc, settings.Targets.Count);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await RunCycleAsync(settings.Targets, stoppingToken);
        }
    }

    private async Task RunCycleAsync(List<ScrapingTarget> targets, CancellationToken cancellationToken)
    {
        if (targets.Count == 0)
        {
            logger.LogWarning("Daily scraping monitor is enabled but no targets were configured under {Section}:Targets", SectionName);
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var bookingHybridService = scope.ServiceProvider.GetRequiredService<IBookingHybridService>();
        var priceTrackingService = scope.ServiceProvider.GetRequiredService<IPriceTrackingService>();

        foreach (var target in targets)
        {
            if (!target.Enabled || string.IsNullOrWhiteSpace(target.Location))
            {
                continue;
            }

            var checkIn = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(Math.Max(0, target.CheckInOffsetDays)));
            var checkOut = checkIn.AddDays(Math.Max(1, target.Nights));

            try
            {
                logger.LogInformation(
                    "Daily scraping target run started. location={Location}, checkIn={CheckIn}, checkOut={CheckOut}, adults={Adults}, kids={Kids}, rooms={Rooms}",
                    target.Location,
                    checkIn,
                    checkOut,
                    target.Adults,
                    target.Kids,
                    target.Rooms);

                var offers = await bookingHybridService.SearchAsync(
                    target.Location,
                    checkIn,
                    checkOut,
                    target.Adults,
                    target.Kids,
                    target.Rooms,
                    cancellationToken);

                if (offers.Count == 0)
                {
                    logger.LogWarning("Daily scraping target produced no offers. location={Location}", target.Location);
                    continue;
                }

                await priceTrackingService.TrackAsync(offers, cancellationToken);

                var minOffer = offers.OrderBy(x => x.Price).First();
                logger.LogInformation(
                    "Daily scraping target finished. location={Location}, offers={Count}, minPrice={MinPrice} {Currency}, hotel={Hotel}",
                    target.Location,
                    offers.Count,
                    minOffer.Price,
                    minOffer.Currency,
                    minOffer.Name);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Daily scraping target failed. location={Location}", target.Location);
            }
        }
    }

    private Settings ReadSettings()
    {
        var section = configuration.GetSection(SectionName);

        var runAtRaw = section.GetValue("RunDailyAtUtc", "06:00");
        if (!TimeSpan.TryParse(runAtRaw, out var runAtUtc))
        {
            runAtUtc = new TimeSpan(6, 0, 0);
        }

        var targets = section.GetSection("Targets").Get<List<ScrapingTarget>>() ?? [];

        return new Settings(
            section.GetValue("Enabled", false),
            section.GetValue("RunOnStartup", true),
            runAtUtc,
            targets);
    }

    private static DateTime GetNextRunUtc(DateTime nowUtc, TimeSpan runAtUtc)
    {
        var next = nowUtc.Date.Add(runAtUtc);
        if (next <= nowUtc)
        {
            next = next.AddDays(1);
        }

        return DateTime.SpecifyKind(next, DateTimeKind.Utc);
    }

    private sealed record Settings(bool Enabled, bool RunOnStartup, TimeSpan RunDailyAtUtc, List<ScrapingTarget> Targets);

    public sealed record ScrapingTarget
    {
        public bool Enabled { get; init; } = true;
        public string Location { get; init; } = string.Empty;
        public int Adults { get; init; } = 2;
        public int Kids { get; init; } = 0;
        public int Rooms { get; init; } = 1;
        public int CheckInOffsetDays { get; init; } = 30;
        public int Nights { get; init; } = 2;
    }
}