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
    IUpworkScraperClient upworkScraperClient,
    IConfiguration configuration,
    ILogger<JobOrchestrator> logger) : IJobOrchestrator
{
    private readonly Dictionary<string, IJobProvider> providersMap = providers
        .ToDictionary(p => p.Name.Trim().ToLowerInvariant(), StringComparer.OrdinalIgnoreCase);

    public async Task LoginAsync(string provider, string username, string password, bool showBrowser = false, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeProvider(provider);
        if (!ProviderRequiresAuthentication(normalized))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("provider is required");
        }

        var providerSectionName = normalized switch
        {
            "linkedin" => "LinkedIn",
            "upwork" => "Upwork",
            _ => char.ToUpperInvariant(normalized[0]) + normalized[1..]
        };

        var credentialsSection = configuration.GetSection($"Jobs:Credentials:{providerSectionName}");
        username = credentialsSection.GetValue<string>("Username") ?? string.Empty;
        password = credentialsSection.GetValue<string>("Password") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException($"Missing Jobs:Credentials:{normalized}:Username or Password in appsettings");
        }

        if (normalized == "upwork")
        {
            var loginResult = await upworkScraperClient.LoginAsync(username.Trim(), password, showBrowser, cancellationToken);
            if (!loginResult.IsAuthenticated)
            {
                throw new InvalidOperationException("Upwork login was not authenticated by scraper API.");
            }

            var defaultExpiration = DateTime.UtcNow.AddHours(Math.Clamp(configuration.GetValue<int?>("Jobs:Playwright:SessionHours") ?? 12, 1, 168));
            await sessionRepository.UpsertLoginAsync(normalized, username.Trim(), loginResult.ExpiresAt ?? defaultExpiration, cancellationToken);
            return;
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
        var isAuthenticated = normalized == "upwork"
            ? await sessionRepository.IsAuthenticatedAsync(normalized, cancellationToken)
            : await sessionManager.ValidateAsync(normalized, cancellationToken);
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
        bool showBrowser = false,
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
            EndPage: endPage,
            ShowBrowser: showBrowser);

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

    public Task<(List<JobOffer> Items, int TotalCount)> QueryJobsAsync(
        JobsQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        return jobRepository.QueryAsync(request, cancellationToken);
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
                throw new InvalidOperationException(
                    $"{provider.Name} session is not valid. Re-authenticate with POST /api/auth/login.");
            }

            var timeoutSeconds = provider.Name.Equals("upwork", StringComparison.OrdinalIgnoreCase)
                ? Math.Clamp(configuration.GetValue<int?>("Jobs:Upwork:ProviderTimeoutSeconds") ?? 900, 30, 1800)
                : Math.Clamp(configuration.GetValue<int?>("Jobs:Providers:TimeoutSeconds") ?? 900, 10, 3600);
            var timeoutPolicy = Policy.TimeoutAsync(timeoutSeconds, TimeoutStrategy.Optimistic);
            IAsyncPolicy policy;
            if (provider.Name.Equals("upwork", StringComparison.OrdinalIgnoreCase))
            {
                policy = timeoutPolicy;
            }
            else
            {
                policy = Policy.WrapAsync(
                    Policy
                        .Handle<Exception>(ex => ex is not InvalidOperationException)
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
                            }),
                    timeoutPolicy);
                            }

            var jobs = await policy.ExecuteAsync(
                async ct => await provider.SearchAsync(request, ct),
                cancellationToken);

            if (ProviderRequiresAuthentication(provider.Name))
            {
                await sessionRepository.MarkUsedAsync(provider.Name, cancellationToken);
            }

            return jobs ?? [];
        }
        catch (InvalidOperationException)
        {
            // Surface auth/session errors to controller so API returns 409 instead of silent zero results.
            throw;
        }
        catch (Exception ex)
        {
            if (ProviderRequiresAuthentication(provider.Name))
            {
                throw new InvalidOperationException(
                    $"{provider.Name} scraping failed and requires manual re-authentication.", ex);
            }

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
        var normalized = NormalizeProvider(provider);
        return normalized is "linkedin" or "upwork";
    }
}
