using backend.Domain.Entities;
using backend.Infrastructure.Data;
using backend.Infrastructure.Repositories;
using backend.Infrastructure.Scraping;
using Microsoft.EntityFrameworkCore;

namespace backend.Infrastructure.Services;

public interface IAppSumoOrchestratorService
{
    Task RunAsync(string? startCategorySlug, bool dryRun, int maxProducts, CancellationToken ct);
    Task<int> BackfillIdeasAsync(CancellationToken ct);
}

/// <summary>
/// Drives the full Category → Product → Review → IdeaVault scrape loop.
/// Designed to be called from the controller (fire-and-track pattern via ScrapeRun).
/// </summary>
public sealed class AppSumoOrchestratorService(
    AppSumoScraperService scraper,
    AppSumoRepository repository,
    ApplicationDbContext dbContext,
    ILogger<AppSumoOrchestratorService> logger) : IAppSumoOrchestratorService
{
    public async Task RunAsync(
        string? startCategorySlug,
        bool dryRun,
        int maxProducts,
        CancellationToken ct)
    {
        var run = await repository.StartRunAsync();
        logger.LogInformation("AppSumo scrape run #{RunId} started. DryRun={DryRun}", run.Id, dryRun);

        try
        {
            // ── Step 1: Categories ────────────────────────────────────────────
            var categories = await scraper.ExtractCategoriesAsync(ct);

            if (!dryRun)
                await repository.UpsertCategoriesAsync(categories);

            var allCategories = await repository.GetAllCategoriesAsync();
            if (!string.IsNullOrWhiteSpace(startCategorySlug))
                allCategories = allCategories.Where(c => c.Slug == startCategorySlug).ToList();

            // ── Step 2: Products per category ─────────────────────────────────
            foreach (var category in allCategories)
            {
                ct.ThrowIfCancellationRequested();
                logger.LogInformation("AppSumo: scraping products for category {Slug}", category.Slug);

                var products = await scraper.ExtractProductsForCategoryAsync(category, maxProducts, ct);
                if (!dryRun && products.Count > 0)
                    await repository.UpsertProductsAsync(products);

                category.ScrapedAt = DateTime.UtcNow;
            }

            // ── Step 3: Reviews per pending product ───────────────────────────
            var pendingProducts = await repository.GetPendingProductsAsync(startCategorySlug);
            if (maxProducts > 0)
                pendingProducts = pendingProducts.Take(maxProducts).ToList();

            foreach (var product in pendingProducts)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    var reviews = await scraper.ExtractLowRatingReviewsAsync(product, ct);
                    logger.LogInformation("AppSumo: {Slug} → {Count} low-rating reviews", product.Slug, reviews.Count);

                    int saved = 0;
                    if (!dryRun && reviews.Count > 0)
                    {
                        foreach (var r in reviews) r.ProductId = product.Id;
                        saved = await repository.BulkInsertReviewsAsync(reviews);
                        await repository.MarkProductScrapedAsync(product);
                        await repository.SetProductStateAsync(product.Id, run.Id, "Done");
                    }

                    run.ProductsScraped++;
                    run.ReviewsSaved += saved;
                    await repository.UpdateRunAsync(run);

                    // Rate-limit: pause between products
                    await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(2000, 5000)), ct);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    logger.LogError(ex, "AppSumo: failed to scrape reviews for {Slug}", product.Slug);
                    run.ErrorCount++;
                    if (!dryRun)
                        await repository.SetProductStateAsync(product.Id, run.Id, "Failed", ex.Message);
                    await repository.UpdateRunAsync(run);
                }
            }

            // ── Step 4: Auto-generate Idea Vault entries ──────────────────────
            if (!dryRun)
                await GenerateIdeasForProductsAsync(ct);

            run.Status     = "Completed";
            run.FinishedAt = DateTime.UtcNow;
            run.Notes      = dryRun ? "Dry run — no data written" : null;
            await repository.UpdateRunAsync(run);

            logger.LogInformation(
                "AppSumo scrape run #{RunId} completed. Products={Products} Reviews={Reviews} Errors={Errors}",
                run.Id, run.ProductsScraped, run.ReviewsSaved, run.ErrorCount);
        }
        catch (OperationCanceledException)
        {
            run.Status     = "Cancelled";
            run.FinishedAt = DateTime.UtcNow;
            await repository.UpdateRunAsync(run);
            logger.LogWarning("AppSumo scrape run #{RunId} was cancelled", run.Id);
        }
        catch (Exception ex)
        {
            run.Status     = "Failed";
            run.FinishedAt = DateTime.UtcNow;
            run.Notes      = ex.Message;
            await repository.UpdateRunAsync(run);
            logger.LogError(ex, "AppSumo scrape run #{RunId} failed", run.Id);
        }
    }

    // ── Backfill: generate ideas for all existing Done products ──────────────

    public async Task<int> BackfillIdeasAsync(CancellationToken ct)
    {
        var before = await dbContext.OpportunityIdeas.CountAsync(i => i.Source == "AppSumo", ct);
        await GenerateIdeasForProductsAsync(ct);
        var after = await dbContext.OpportunityIdeas.CountAsync(i => i.Source == "AppSumo", ct);
        return after - before;
    }

    // ── Step 4: Auto-generate Idea Vault entries ──────────────────────────────

    private async Task GenerateIdeasForProductsAsync(CancellationToken ct)
    {
        // Products already linked to an idea
        var linkedProductIds = await dbContext.OpportunityIdeas
            .AsNoTracking()
            .Where(i => i.AppSumoProductId != null)
            .Select(i => i.AppSumoProductId!.Value)
            .ToHashSetAsync(ct);

        // Products with at least one low-rating (≤2 taco) review not yet in the vault
        var products = await dbContext.AppSumoProducts
            .AsNoTracking()
            .Include(p => p.Reviews)
            .Where(p => !linkedProductIds.Contains(p.Id)
                     && p.Reviews.Any(r => r.TacoRating <= 2))
            .ToListAsync(ct);

        if (products.Count == 0) return;

        // Existing slugs to ensure uniqueness
        var usedSlugs = await dbContext.OpportunityIdeas
            .AsNoTracking()
            .Select(i => i.Id)
            .ToHashSetAsync(ct);

        var newIdeas = new List<OpportunityIdea>(products.Count);

        foreach (var product in products)
        {
            ct.ThrowIfCancellationRequested();

            var lowReviews = product.Reviews
                .Where(r => r.TacoRating <= 2)
                .OrderBy(r => r.TacoRating)
                .Take(10)
                .ToList();

            if (lowReviews.Count == 0) continue;

            // Concatenate review excerpts into business justification (max 2000 chars)
            var justification = string.Join(" | ", lowReviews.Select(r =>
                r.ReviewText.Length > 200 ? r.ReviewText[..200] + "…" : r.ReviewText));
            if (justification.Length > 2000) justification = justification[..2000];

            // Generate unique slug
            var baseSlug = NormalizeSlug(product.Name);
            var slug     = baseSlug;
            var suffix   = 2;
            while (!usedSlugs.Add(slug))
                slug = $"{baseSlug}-{suffix++}";

            newIdeas.Add(new OpportunityIdea
            {
                Id                    = slug,
                Name                  = product.Name,
                BusinessJustification = justification,
                Source                = "AppSumo",
                AppSumoProductId      = product.Id,
                CreatedAt             = DateTime.UtcNow,
            });
        }

        if (newIdeas.Count == 0) return;

        dbContext.OpportunityIdeas.AddRange(newIdeas);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("AppSumo: created {Count} Idea Vault entries from low-rating reviews.", newIdeas.Count);
    }

    private static string NormalizeSlug(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "appsumo-idea";
        var sb          = new System.Text.StringBuilder();
        var lastWasDash = false;
        foreach (var ch in name.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch)) { sb.Append(ch); lastWasDash = false; }
            else if (!lastWasDash)        { sb.Append('-'); lastWasDash = true; }
        }
        var result = sb.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(result) ? "appsumo-idea" : result;
    }
}
