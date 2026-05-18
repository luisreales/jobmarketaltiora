using backend.Application.Contracts;
using backend.Infrastructure.Repositories;
using backend.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/appsumo")]
public class AppSumoController(
    AppSumoRepository repository,
    IServiceScopeFactory scopeFactory,
    ILogger<AppSumoController> logger) : ControllerBase
{
    // ── Scrape control ────────────────────────────────────────────────────────

    /// <summary>
    /// Start a new scrape run. Runs in the background — poll /runs for status.
    /// </summary>
    [HttpPost("scrape/start")]
    public IActionResult StartScrape([FromBody] StartScrapeRequest request)
    {
        var cts = new CancellationTokenSource();

        // Create a fresh DI scope so scoped services (DbContext, Repository, etc.)
        // survive beyond the HTTP request lifetime.
        _ = Task.Run(async () =>
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var orchestrator = scope.ServiceProvider.GetRequiredService<IAppSumoOrchestratorService>();
            await orchestrator.RunAsync(request.StartCategorySlug, request.DryRun, request.MaxProducts, cts.Token);
        }, cts.Token);

        logger.LogInformation("AppSumo: scrape started. DryRun={DryRun} Category={Category}",
            request.DryRun, request.StartCategorySlug ?? "all");

        return Accepted(new { message = "Scrape started. Poll /api/appsumo/runs for status." });
    }

    /// <summary>Recent scrape runs (latest 20).</summary>
    [HttpGet("runs")]
    public async Task<IActionResult> GetRuns()
        => Ok(await repository.GetRunsAsync());

    // ── Categories ────────────────────────────────────────────────────────────

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
        => Ok(await repository.GetAllCategoriesAsync());

    // ── Products ──────────────────────────────────────────────────────────────

    [HttpGet("products")]
    public async Task<IActionResult> GetProducts([FromQuery] AppSumoProductQuery query)
        => Ok(await repository.QueryProductsAsync(query));

    // ── Reviews ───────────────────────────────────────────────────────────────

    [HttpGet("reviews")]
    public async Task<IActionResult> GetReviews([FromQuery] AppSumoReviewQuery query)
        => Ok(await repository.QueryReviewsAsync(query));

    // ── Stats ─────────────────────────────────────────────────────────────────

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
        => Ok(await repository.GetStatsAsync());

    // ── Idea backfill ─────────────────────────────────────────────────────────

    /// <summary>
    /// Generates Idea Vault entries for all already-scraped AppSumo products
    /// that have low-rating reviews but no idea yet. Safe to call multiple times.
    /// </summary>
    [HttpPost("generate-ideas")]
    public async Task<IActionResult> GenerateIdeas(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IAppSumoOrchestratorService>();
        var created = await orchestrator.BackfillIdeasAsync(cancellationToken);
        logger.LogInformation("AppSumo: backfill generated {Count} new ideas.", created);
        return Ok(new { created, message = $"{created} new Idea Vault entries created from AppSumo reviews." });
    }
}
