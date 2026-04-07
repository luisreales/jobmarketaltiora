using backend.Application.Contracts;
using backend.Application.Interfaces;
using backend.Domain.Entities;
using backend.Infrastructure.Sessions;
using Polly;
using Polly.Timeout;

namespace backend.Infrastructure.Services;

public class JobOrchestrator(
    IJobRepository jobRepository,
    IProviderSessionRepository sessionRepository,
    IEnumerable<IJobProvider> providers,
    ISessionManager sessionManager,
    IConfiguration configuration,
    ILogger<JobOrchestrator> logger) : IJobOrchestrator
{
    private readonly Dictionary<string, IJobProvider> providersMap = providers
        .ToDictionary(p => p.Name.Trim().ToLowerInvariant(), StringComparer.OrdinalIgnoreCase);

    public async Task LoginAsync(string provider, string username, string password, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeProvider(provider);
        if (!ProviderRequiresAuthentication(normalized))
        {
            return;
        }

        // Credentials are loaded from configuration to avoid accidental runtime input leakage.
        username = configuration.GetValue<string>("Jobs:Credentials:LinkedIn:Username") ?? string.Empty;
        password = configuration.GetValue<string>("Jobs:Credentials:LinkedIn:Password") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Missing Jobs:Credentials:LinkedIn:Username or Password in appsettings");
        }

        await sessionManager.LoginAsync(normalized, username.Trim(), password, cancellationToken);
    }

    public async Task LogoutAsync(string provider, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeProvider(provider);
        await sessionRepository.ClearSessionAsync(normalized, cancellationToken);
        await sessionManager.ClearAsync(normalized, cancellationToken);
    }

    public async Task<(bool isAuthenticated, DateTime? lastLoginAt, DateTime? lastUsedAt, DateTime? expiresAt)> GetAuthStatusAsync(
        string provider,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeProvider(provider);
        if (!ProviderRequiresAuthentication(normalized))
        {
            return (true, null, null, null);
        }

        var session = await sessionRepository.GetCurrentAsync(normalized, cancellationToken);
        var isAuthenticated = await sessionManager.ValidateAsync(normalized, cancellationToken);
        return (isAuthenticated, session?.LastLoginAt, session?.LastUsedAt, session?.ExpiresAt);
    }

    public async Task<(int savedCount, int totalFound)> SearchAndSaveAsync(
        string query,
        string? location,
        int limit,
        IReadOnlyCollection<string>? providersFilter = null,
        int? totalPaging = null,
        int? startPage = null,
        int? endPage = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new InvalidOperationException("query is required");
        }

        var selectedProviderNames = ResolveSearchProviders(providersFilter);
        var selectedProviders = selectedProviderNames
            .Where(name => providersMap.ContainsKey(name))
            .Select(name => providersMap[name])
            .ToList();

        var request = new SearchRequest(
            Query: query.Trim(),
            Location: location,
            Limit: limit,
            TotalPaging: totalPaging,
            StartPage: startPage,
            EndPage: endPage);

        var maxParallel = Math.Clamp(configuration.GetValue<int?>("Jobs:Providers:MaxParallel") ?? 2, 1, 8);
        using var throttler = new SemaphoreSlim(maxParallel, maxParallel);

        var tasks = selectedProviders.Select(async provider =>
        {
            await throttler.WaitAsync(cancellationToken);
            try
            {
                return await ExecuteProviderWithResilienceAsync(provider, request, cancellationToken);
            }
            finally
            {
                throttler.Release();
            }
        });

        var resultSets = await Task.WhenAll(tasks);
        var allResults = resultSets.SelectMany(x => x).ToList();

        var savedCount = await jobRepository.UpsertRangeAsync(allResults, cancellationToken);
        return (savedCount, allResults.Count);
    }

    public Task<List<JobOffer>> GetJobsAsync(CancellationToken cancellationToken = default)
    {
        return jobRepository.GetAllAsync(cancellationToken);
    }

    public async Task<List<JobOffer>> GetHighValueLeadsAsync(CancellationToken cancellationToken = default)
    {
        var jobs = await jobRepository.GetAllAsync(cancellationToken);
        return jobs
            .Where(job => job.IsProcessed && !job.IsConsultingCompany && job.OpportunityScore >= 60)
            .OrderByDescending(job => job.OpportunityScore)
            .ThenByDescending(job => job.CapturedAt)
            .ToList();
    }

    public Task<JobOffer?> GetJobByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return jobRepository.GetByIdAsync(id, cancellationToken);
    }

    private async Task<List<JobOffer>> ExecuteProviderWithResilienceAsync(
        IJobProvider provider,
        SearchRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var isAuth = await provider.IsAuthenticatedAsync(cancellationToken);
            if (!isAuth && ProviderRequiresAuthentication(provider.Name))
            {
                logger.LogWarning("Skipping provider {Provider} because auth session is not valid.", provider.Name);
                return [];
            }

            var retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    3,
                    attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)),
                    (ex, delay, attempt, _) =>
                    {
                        logger.LogWarning(ex,
                            "Provider {Provider} failed on attempt {Attempt}. Retrying in {Delay}s.",
                            provider.Name,
                            attempt,
                            delay.TotalSeconds);
                    });

            var timeoutSeconds = Math.Clamp(configuration.GetValue<int?>("Jobs:Providers:TimeoutSeconds") ?? 90, 10, 300);
            var timeoutPolicy = Policy.TimeoutAsync(timeoutSeconds, TimeoutStrategy.Optimistic);
            var policy = Policy.WrapAsync(retryPolicy, timeoutPolicy);

            var jobs = await policy.ExecuteAsync(
                async ct => await provider.SearchAsync(request, ct),
                cancellationToken);

            if (ProviderRequiresAuthentication(provider.Name))
            {
                await sessionRepository.MarkUsedAsync(provider.Name, cancellationToken);
            }

            return jobs ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Provider {Provider} failed while scraping query {Query}", provider.Name, request.Query);
            return [];
        }
    }

    private List<string> ResolveSearchProviders(IReadOnlyCollection<string>? providersFilter)
    {
        if (providersFilter is { Count: > 0 })
        {
            return providersFilter
                .Select(NormalizeProvider)
                .Distinct()
                .ToList();
        }

        var enabledProviders = configuration.GetSection("Jobs:Providers:Enabled").Get<List<string>>() ?? [];
        return enabledProviders.Count == 0
            ? ["linkedin", "indeed"]
            : enabledProviders.Select(NormalizeProvider).Distinct().ToList();
    }

    private static string NormalizeProvider(string provider)
    {
        return provider.Trim().ToLowerInvariant();
    }

    private static bool ProviderRequiresAuthentication(string provider)
    {
        return NormalizeProvider(provider) == "linkedin";
    }
}
