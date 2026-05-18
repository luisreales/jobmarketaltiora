using backend.Application.Contracts;
using backend.Domain.Entities;
using backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace backend.Infrastructure.Repositories;

public sealed class AppSumoRepository(ApplicationDbContext db)
{
    // ── Categories ────────────────────────────────────────────────────────────

    public async Task<List<AppSumoCategory>> GetAllCategoriesAsync()
        => await db.AppSumoCategories.OrderBy(c => c.Name).ToListAsync();

    public async Task UpsertCategoriesAsync(List<AppSumoCategory> categories)
    {
        var existingSlugs = await db.AppSumoCategories
            .Select(c => c.Slug)
            .ToHashSetAsync();

        var toInsert = categories.Where(c => !existingSlugs.Contains(c.Slug)).ToList();
        if (toInsert.Count > 0)
        {
            await db.AppSumoCategories.AddRangeAsync(toInsert);
            await db.SaveChangesAsync();
        }

        // Update ScrapedAt for existing
        var toUpdate = await db.AppSumoCategories
            .Where(c => categories.Select(x => x.Slug).Contains(c.Slug))
            .ToListAsync();
        foreach (var cat in toUpdate)
            cat.ScrapedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
    }

    // ── Products ──────────────────────────────────────────────────────────────

    public async Task<PagedResult<AppSumoProductDto>> QueryProductsAsync(AppSumoProductQuery query)
    {
        var q = db.AppSumoProducts
            .Include(p => p.Category)
            .Include(p => p.ScrapeState)
            .AsQueryable();

        if (query.CategoryId.HasValue)
            q = q.Where(p => p.CategoryId == query.CategoryId.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(p => p.Name.Contains(query.Search) || (p.Description != null && p.Description.Contains(query.Search)));

        if (!string.IsNullOrWhiteSpace(query.ScrapeStatus))
            q = q.Where(p => p.ScrapeState != null && p.ScrapeState.Status == query.ScrapeStatus);

        var total = await q.CountAsync();
        var items = await q
            .OrderByDescending(p => p.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return new PagedResult<AppSumoProductDto>(
            items.Select(p => ToProductDto(p)).ToList(),
            total,
            query.Page,
            query.PageSize,
            (int)Math.Ceiling(total / (double)query.PageSize));
    }

    public async Task UpsertProductsAsync(List<AppSumoProduct> products)
    {
        if (products.Count == 0) return;

        var slugs = products.Select(p => p.Slug).ToList();
        var existingMap = await db.AppSumoProducts
            .Where(p => slugs.Contains(p.Slug))
            .ToDictionaryAsync(p => p.Slug);

        foreach (var product in products)
        {
            if (existingMap.TryGetValue(product.Slug, out var existing))
            {
                // Update enrichment fields if new data
                if (product.OverallRating.HasValue)  existing.OverallRating  = product.OverallRating;
                if (product.TotalReviewCount.HasValue) existing.TotalReviewCount = product.TotalReviewCount;
                if (!string.IsNullOrWhiteSpace(product.Description)) existing.Description = product.Description;
                if (!string.IsNullOrWhiteSpace(product.PricingModel)) existing.PricingModel = product.PricingModel;
                if (!string.IsNullOrWhiteSpace(product.TagsJson)) existing.TagsJson = product.TagsJson;
            }
            else
            {
                await db.AppSumoProducts.AddAsync(product);
            }
        }

        await db.SaveChangesAsync();
    }

    public async Task<List<AppSumoProduct>> GetPendingProductsAsync(string? startCategorySlug = null)
    {
        var q = db.AppSumoProducts
            .Include(p => p.ScrapeState)
            .Include(p => p.Category)
            .Where(p => p.ScrapeState == null || p.ScrapeState.Status == "Pending" || p.ScrapeState.Status == "Failed")
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(startCategorySlug))
            q = q.Where(p => p.Category.Slug == startCategorySlug);

        return await q.OrderBy(p => p.Id).ToListAsync();
    }

    // ── Reviews ───────────────────────────────────────────────────────────────

    public async Task<PagedResult<AppSumoReviewDto>> QueryReviewsAsync(AppSumoReviewQuery query)
    {
        var q = db.AppSumoReviews
            .Include(r => r.Product).ThenInclude(p => p.Category)
            .AsQueryable();

        if (query.ProductId.HasValue)  q = q.Where(r => r.ProductId == query.ProductId.Value);
        if (query.CategoryId.HasValue) q = q.Where(r => r.Product.CategoryId == query.CategoryId.Value);
        if (query.TacoRating.HasValue) q = q.Where(r => r.TacoRating == query.TacoRating.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(r => r.ReviewText.Contains(query.Search) || (r.ReviewerName != null && r.ReviewerName.Contains(query.Search)));

        var total = await q.CountAsync();
        var items = await q
            .OrderByDescending(r => r.ReviewDate)
            .ThenByDescending(r => r.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return new PagedResult<AppSumoReviewDto>(
            items.Select(ToReviewDto).ToList(),
            total,
            query.Page,
            query.PageSize,
            (int)Math.Ceiling(total / (double)query.PageSize));
    }

    public async Task<int> BulkInsertReviewsAsync(List<AppSumoReview> reviews)
    {
        if (reviews.Count == 0) return 0;

        // Avoid duplicates by AppSumoReviewId when available
        var withIds = reviews.Where(r => r.AppSumoReviewId != null).ToList();
        if (withIds.Count > 0)
        {
            var knownIds = await db.AppSumoReviews
                .Where(r => r.ProductId == withIds[0].ProductId && r.AppSumoReviewId != null)
                .Select(r => r.AppSumoReviewId!)
                .ToHashSetAsync();

            reviews = reviews.Where(r => r.AppSumoReviewId == null || !knownIds.Contains(r.AppSumoReviewId)).ToList();
        }

        if (reviews.Count == 0) return 0;
        await db.AppSumoReviews.AddRangeAsync(reviews);
        await db.SaveChangesAsync();
        return reviews.Count;
    }

    // ── Scrape runs ───────────────────────────────────────────────────────────

    public async Task<AppSumoScrapeRun> StartRunAsync()
    {
        var run = new AppSumoScrapeRun { StartedAt = DateTime.UtcNow, Status = "Running" };
        db.AppSumoScrapeRuns.Add(run);
        await db.SaveChangesAsync();
        return run;
    }

    public async Task UpdateRunAsync(AppSumoScrapeRun run)
    {
        db.AppSumoScrapeRuns.Update(run);
        await db.SaveChangesAsync();
    }

    public async Task<List<AppSumoScrapeRunDto>> GetRunsAsync(int limit = 20)
        => await db.AppSumoScrapeRuns
            .OrderByDescending(r => r.StartedAt)
            .Take(limit)
            .Select(r => new AppSumoScrapeRunDto(
                r.Id, r.StartedAt, r.FinishedAt, r.Status,
                r.ProductsScraped, r.ReviewsSaved, r.ErrorCount, r.Notes))
            .ToListAsync();

    // ── Scrape state ──────────────────────────────────────────────────────────

    public async Task SetProductStateAsync(int productId, int runId, string status, string? error = null)
    {
        var state = await db.ProductScrapeStates.FindAsync(productId);
        if (state is null)
        {
            state = new ProductScrapeState { ProductId = productId };
            db.ProductScrapeStates.Add(state);
        }

        state.LastRunId     = runId;
        state.Status        = status;
        state.LastError     = error;
        state.AttemptCount  = (byte)Math.Min(255, state.AttemptCount + 1);
        state.UpdatedAt     = DateTime.UtcNow;

        await db.SaveChangesAsync();
    }

    public async Task MarkProductScrapedAsync(AppSumoProduct product)
    {
        product.ScrapedAt = DateTime.UtcNow;
        db.AppSumoProducts.Update(product);
        await db.SaveChangesAsync();
    }

    // ── Stats ─────────────────────────────────────────────────────────────────

    public async Task<object> GetStatsAsync()
    {
        var categories = await db.AppSumoCategories.CountAsync();
        var products   = await db.AppSumoProducts.CountAsync();
        var scraped    = await db.AppSumoProducts.CountAsync(p => p.ScrapedAt != null);
        var reviews    = await db.AppSumoReviews.CountAsync();
        var byRating   = await db.AppSumoReviews
            .GroupBy(r => r.TacoRating)
            .Select(g => new { Rating = g.Key, Count = g.Count() })
            .ToListAsync();

        return new { categories, products, scraped, reviews, byRating };
    }

    // ── Mappers ───────────────────────────────────────────────────────────────

    private static AppSumoProductDto ToProductDto(AppSumoProduct p) => new(
        p.Id, p.CategoryId, p.Category?.Name ?? "",
        p.Name, p.Slug, p.Url, p.Description,
        p.OverallRating, p.TotalReviewCount, p.PricingModel, p.TagsJson,
        p.ScrapeState?.Status ?? "Pending",
        0, // LowRatingReviewCount filled separately for list queries (perf)
        p.ScrapedAt);

    private static AppSumoReviewDto ToReviewDto(AppSumoReview r) => new(
        r.Id, r.ProductId,
        r.Product?.Name ?? "", r.Product?.Category?.Name ?? "",
        r.AppSumoReviewId, r.TacoRating,
        r.ReviewerName, r.ReviewDate, r.ReviewText,
        r.FoundHelpful, r.IsVerified, r.CreatedAt);
}
