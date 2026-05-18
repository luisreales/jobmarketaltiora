using backend.Application.Contracts;
using backend.Domain.Entities;
using backend.Infrastructure.Repositories;
using backend.Tests.Fakes;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace backend.Tests;

/// <summary>
/// Tests for AppSumoRepository: upsert logic, bulk review insert,
/// scrape-state management, pagination, and stats.
/// </summary>
public class AppSumoRepositoryTests
{
    // ── UpsertCategories ──────────────────────────────────────────────────────

    [Fact]
    public async Task UpsertCategories_InsertsNewCategories()
    {
        var db   = TestApplicationDbContextFactory.Create();
        var repo = new AppSumoRepository(db);

        var categories = new List<AppSumoCategory>
        {
            new() { Name = "Operations", Slug = "software/operations", Url = "/software/operations" },
            new() { Name = "Marketing",  Slug = "software/marketing",  Url = "/software/marketing"  }
        };

        await repo.UpsertCategoriesAsync(categories);

        Assert.Equal(2, await db.AppSumoCategories.CountAsync());
    }

    [Fact]
    public async Task UpsertCategories_DoesNotInsertDuplicateSlugs()
    {
        var db   = TestApplicationDbContextFactory.Create();
        var repo = new AppSumoRepository(db);

        var cats = new List<AppSumoCategory>
        {
            new() { Name = "Operations", Slug = "software/operations", Url = "/software/operations" }
        };
        await repo.UpsertCategoriesAsync(cats);
        await repo.UpsertCategoriesAsync(cats); // second call, same slug

        Assert.Equal(1, await db.AppSumoCategories.CountAsync());
    }

    // ── UpsertProducts ────────────────────────────────────────────────────────

    [Fact]
    public async Task UpsertProducts_InsertsNewProducts()
    {
        var db   = TestApplicationDbContextFactory.Create();
        var repo = new AppSumoRepository(db);
        var cat  = SeedCategory(db);

        var products = new List<AppSumoProduct>
        {
            BuildProduct(cat.Id, "tidycal",   "TidyCal"),
            BuildProduct(cat.Id, "appflowy",  "AppFlowy")
        };

        await repo.UpsertProductsAsync(products);

        Assert.Equal(2, await db.AppSumoProducts.CountAsync());
    }

    [Fact]
    public async Task UpsertProducts_UpdatesExistingProductRating()
    {
        var db   = TestApplicationDbContextFactory.Create();
        var repo = new AppSumoRepository(db);
        var cat  = SeedCategory(db);

        var prod = BuildProduct(cat.Id, "tidycal", "TidyCal", rating: 4.0m);
        db.AppSumoProducts.Add(prod);
        await db.SaveChangesAsync();

        // Upsert with updated rating
        await repo.UpsertProductsAsync(
        [
            BuildProduct(cat.Id, "tidycal", "TidyCal", rating: 4.8m)
        ]);

        var loaded = await db.AppSumoProducts.SingleAsync(p => p.Slug == "tidycal");
        Assert.Equal(1, await db.AppSumoProducts.CountAsync());
        Assert.Equal(4.8m, loaded.OverallRating);
    }

    // ── QueryProducts ─────────────────────────────────────────────────────────

    [Fact]
    public async Task QueryProducts_Pagination_ReturnsCorrectPage()
    {
        var db   = TestApplicationDbContextFactory.Create();
        var repo = new AppSumoRepository(db);
        var cat  = SeedCategory(db);

        for (var i = 1; i <= 5; i++)
            db.AppSumoProducts.Add(BuildProduct(cat.Id, $"slug-{i}", $"Product {i}"));
        await db.SaveChangesAsync();

        var result = await repo.QueryProductsAsync(new AppSumoProductQuery { Page = 1, PageSize = 3 });

        Assert.Equal(5, result.TotalCount);
        Assert.Equal(3, result.Items.Count);
        Assert.Equal(2, result.TotalPages);
    }

    [Fact]
    public async Task QueryProducts_SearchFilter_ReturnsMatchingItems()
    {
        var db   = TestApplicationDbContextFactory.Create();
        var repo = new AppSumoRepository(db);
        var cat  = SeedCategory(db);

        db.AppSumoProducts.AddRange(
            BuildProduct(cat.Id, "tidycal",  "TidyCal"),
            BuildProduct(cat.Id, "appflowy", "AppFlowy"),
            BuildProduct(cat.Id, "tidyforms","TidyForms")
        );
        await db.SaveChangesAsync();

        var result = await repo.QueryProductsAsync(new AppSumoProductQuery { Search = "Tidy", Page = 1, PageSize = 20 });

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, item => Assert.Contains("Tidy", item.Name));
    }

    // ── BulkInsertReviews ─────────────────────────────────────────────────────

    [Fact]
    public async Task BulkInsertReviews_InsertsAllReviews()
    {
        var db   = TestApplicationDbContextFactory.Create();
        var repo = new AppSumoRepository(db);
        var cat  = SeedCategory(db);
        var prod = SeedProduct(db, cat.Id);

        var reviews = new List<AppSumoReview>
        {
            BuildReview(prod.Id, 1, "Crashes on startup."),
            BuildReview(prod.Id, 2, "Missing integrations."),
            BuildReview(prod.Id, 3, "Needs more templates.")
        };

        var saved = await repo.BulkInsertReviewsAsync(reviews);

        Assert.Equal(3, saved);
        Assert.Equal(3, await db.AppSumoReviews.CountAsync());
    }

    [Fact]
    public async Task BulkInsertReviews_SkipsDuplicateReviewIds()
    {
        var db   = TestApplicationDbContextFactory.Create();
        var repo = new AppSumoRepository(db);
        var cat  = SeedCategory(db);
        var prod = SeedProduct(db, cat.Id);

        var first = new List<AppSumoReview>
        {
            BuildReview(prod.Id, 2, "Needs work.", reviewId: "rev-100")
        };
        await repo.BulkInsertReviewsAsync(first);

        var second = new List<AppSumoReview>
        {
            BuildReview(prod.Id, 2, "Needs work.", reviewId: "rev-100"), // duplicate
            BuildReview(prod.Id, 1, "Broken.",      reviewId: "rev-200")  // new
        };
        var saved = await repo.BulkInsertReviewsAsync(second);

        Assert.Equal(1, saved);
        Assert.Equal(2, await db.AppSumoReviews.CountAsync());
    }

    [Fact]
    public async Task BulkInsertReviews_EmptyList_ReturnsZero()
    {
        var db   = TestApplicationDbContextFactory.Create();
        var repo = new AppSumoRepository(db);

        var saved = await repo.BulkInsertReviewsAsync([]);

        Assert.Equal(0, saved);
    }

    // ── QueryReviews ──────────────────────────────────────────────────────────

    [Fact]
    public async Task QueryReviews_FilterByTacoRating_ReturnsOnlyMatchingRatings()
    {
        var db   = TestApplicationDbContextFactory.Create();
        var repo = new AppSumoRepository(db);
        var cat  = SeedCategory(db);
        var prod = SeedProduct(db, cat.Id);

        db.AppSumoReviews.AddRange(
            BuildReview(prod.Id, 1, "Bad."),
            BuildReview(prod.Id, 2, "Meh."),
            BuildReview(prod.Id, 3, "Ok."),
            BuildReview(prod.Id, 5, "Great!")
        );
        await db.SaveChangesAsync();

        var result = await repo.QueryReviewsAsync(new AppSumoReviewQuery { TacoRating = 1 });

        Assert.Equal(1, result.TotalCount);
        Assert.All(result.Items, item => Assert.Equal(1, item.TacoRating));
    }

    [Fact]
    public async Task QueryReviews_SearchFilter_MatchesReviewText()
    {
        var db   = TestApplicationDbContextFactory.Create();
        var repo = new AppSumoRepository(db);
        var cat  = SeedCategory(db);
        var prod = SeedProduct(db, cat.Id);

        db.AppSumoReviews.AddRange(
            BuildReview(prod.Id, 1, "Missing Zapier integration."),
            BuildReview(prod.Id, 2, "No API access."),
            BuildReview(prod.Id, 3, "Zapier support is broken.")
        );
        await db.SaveChangesAsync();

        var result = await repo.QueryReviewsAsync(new AppSumoReviewQuery { Search = "Zapier" });

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, item => Assert.Contains("Zapier", item.ReviewText));
    }

    // ── Scrape run & state ────────────────────────────────────────────────────

    [Fact]
    public async Task StartRun_CreatesRunWithRunningStatus()
    {
        var db   = TestApplicationDbContextFactory.Create();
        var repo = new AppSumoRepository(db);

        var run = await repo.StartRunAsync();

        Assert.NotEqual(0, run.Id);
        Assert.Equal("Running", run.Status);
        Assert.Null(run.FinishedAt);
    }

    [Fact]
    public async Task SetProductState_CreatesAndThenUpdatesState()
    {
        var db   = TestApplicationDbContextFactory.Create();
        var repo = new AppSumoRepository(db);
        var cat  = SeedCategory(db);
        var prod = SeedProduct(db, cat.Id);
        var run  = await repo.StartRunAsync();

        await repo.SetProductStateAsync(prod.Id, run.Id, "Done");

        var state = await db.ProductScrapeStates.SingleAsync(s => s.ProductId == prod.Id);
        Assert.Equal("Done", state.Status);
        Assert.Equal(1, state.AttemptCount);
        Assert.Null(state.LastError);
    }

    [Fact]
    public async Task SetProductState_WithError_StoresErrorMessage()
    {
        var db   = TestApplicationDbContextFactory.Create();
        var repo = new AppSumoRepository(db);
        var cat  = SeedCategory(db);
        var prod = SeedProduct(db, cat.Id);
        var run  = await repo.StartRunAsync();

        await repo.SetProductStateAsync(prod.Id, run.Id, "Failed", "Timeout after 30s");

        var state = await db.ProductScrapeStates.SingleAsync(s => s.ProductId == prod.Id);
        Assert.Equal("Failed", state.Status);
        Assert.Equal("Timeout after 30s", state.LastError);
    }

    [Fact]
    public async Task SetProductState_CalledTwice_IncrementsAttemptCount()
    {
        var db   = TestApplicationDbContextFactory.Create();
        var repo = new AppSumoRepository(db);
        var cat  = SeedCategory(db);
        var prod = SeedProduct(db, cat.Id);
        var run  = await repo.StartRunAsync();

        await repo.SetProductStateAsync(prod.Id, run.Id, "Failed", "Error 1");
        await repo.SetProductStateAsync(prod.Id, run.Id, "Done");

        var state = await db.ProductScrapeStates.SingleAsync(s => s.ProductId == prod.Id);
        Assert.Equal("Done", state.Status);
        Assert.Equal(2, state.AttemptCount);
    }

    // ── GetPendingProducts ────────────────────────────────────────────────────

    [Fact]
    public async Task GetPendingProducts_ExcludesAlreadyDoneProducts()
    {
        var db   = TestApplicationDbContextFactory.Create();
        var repo = new AppSumoRepository(db);
        var cat  = SeedCategory(db);
        var run  = await repo.StartRunAsync();

        var pending = SeedProduct(db, cat.Id, "pending-1");
        var done    = SeedProduct(db, cat.Id, "done-product");

        db.ProductScrapeStates.Add(new ProductScrapeState
        {
            ProductId    = done.Id,
            LastRunId    = run.Id,
            Status       = "Done",
            AttemptCount = 1,
            UpdatedAt    = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var results = await repo.GetPendingProductsAsync();

        Assert.Single(results);
        Assert.Equal(pending.Id, results[0].Id);
    }

    // ── Stats ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetStats_ReturnsAccurateCounts()
    {
        var db   = TestApplicationDbContextFactory.Create();
        var repo = new AppSumoRepository(db);
        var cat  = SeedCategory(db);
        var prod = SeedProduct(db, cat.Id);

        db.AppSumoReviews.AddRange(
            BuildReview(prod.Id, 1, "Bad."),
            BuildReview(prod.Id, 2, "So-so."),
            BuildReview(prod.Id, 3, "Okay.")
        );
        await db.SaveChangesAsync();

        var stats = await repo.GetStatsAsync();
        var json  = System.Text.Json.JsonSerializer.Serialize(stats);
        var doc   = System.Text.Json.JsonDocument.Parse(json).RootElement;

        Assert.Equal(1, doc.GetProperty("categories").GetInt32());
        Assert.Equal(1, doc.GetProperty("products").GetInt32());
        Assert.Equal(3, doc.GetProperty("reviews").GetInt32());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AppSumoCategory SeedCategory(backend.Infrastructure.Data.ApplicationDbContext db)
    {
        var cat = new AppSumoCategory
        {
            Name = "Operations", Slug = "software/operations", Url = "/software/operations"
        };
        db.AppSumoCategories.Add(cat);
        db.SaveChanges();
        return cat;
    }

    private static AppSumoProduct BuildProduct(int categoryId, string slug, string name, decimal? rating = 4.2m) => new()
    {
        CategoryId    = categoryId,
        Slug          = slug,
        Name          = name,
        Url           = $"https://appsumo.com/products/{slug}/",
        OverallRating = rating,
        CreatedAt     = DateTime.UtcNow
    };

    private static AppSumoProduct SeedProduct(
        backend.Infrastructure.Data.ApplicationDbContext db,
        int categoryId,
        string slug = "tidycal")
    {
        var prod = BuildProduct(categoryId, slug, slug);
        db.AppSumoProducts.Add(prod);
        db.SaveChanges();
        return prod;
    }

    private static AppSumoReview BuildReview(int productId, byte rating, string text, string? reviewId = null) => new()
    {
        ProductId       = productId,
        TacoRating      = rating,
        ReviewText      = text,
        AppSumoReviewId = reviewId,
        CreatedAt       = DateTime.UtcNow
    };
}
