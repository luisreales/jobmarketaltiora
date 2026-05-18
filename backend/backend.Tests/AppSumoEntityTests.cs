using backend.Domain.Entities;
using backend.Tests.Fakes;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace backend.Tests;

/// <summary>
/// EF Core InMemory tests for AppSumo entities.
/// Validates persistence, cascade behaviour, and FK relationships.
/// </summary>
public class AppSumoEntityTests
{
    // ── AppSumoCategory ───────────────────────────────────────────────────────

    [Fact]
    public async Task Category_CanBeSavedAndQueried()
    {
        var db  = TestApplicationDbContextFactory.Create();
        var cat = BuildCategory();
        db.AppSumoCategories.Add(cat);
        await db.SaveChangesAsync();

        var loaded = await db.AppSumoCategories.AsNoTracking().SingleAsync(c => c.Id == cat.Id);
        Assert.Equal("Operations", loaded.Name);
        Assert.Equal("software/operations", loaded.Slug);
        Assert.Null(loaded.ParentSlug);
    }

    [Fact]
    public async Task Category_ChildSlug_StoresParentSlug()
    {
        var db  = TestApplicationDbContextFactory.Create();
        var cat = new AppSumoCategory
        {
            Name       = "Calendar & Scheduling",
            Slug       = "software/operations/calendar-scheduling",
            Url        = "/software/operations/calendar-scheduling",
            ParentSlug = "software/operations",
            CreatedAt  = DateTime.UtcNow
        };
        db.AppSumoCategories.Add(cat);
        await db.SaveChangesAsync();

        var loaded = await db.AppSumoCategories.AsNoTracking().SingleAsync(c => c.Id == cat.Id);
        Assert.Equal("software/operations", loaded.ParentSlug);
    }

    // ── AppSumoProduct ────────────────────────────────────────────────────────

    [Fact]
    public async Task Product_CanBeSavedUnderCategory()
    {
        var db  = TestApplicationDbContextFactory.Create();
        var cat = SeedCategory(db);
        var prod = BuildProduct(cat.Id);
        db.AppSumoProducts.Add(prod);
        await db.SaveChangesAsync();

        var loaded = await db.AppSumoProducts
            .Include(p => p.Category)
            .AsNoTracking()
            .SingleAsync(p => p.Id == prod.Id);

        Assert.Equal("TidyCal", loaded.Name);
        Assert.Equal("tidycal", loaded.Slug);
        Assert.Equal(cat.Id, loaded.CategoryId);
        Assert.Equal("Operations", loaded.Category.Name);
    }

    [Fact]
    public async Task Product_NullableRatingIsPreserved()
    {
        var db  = TestApplicationDbContextFactory.Create();
        var cat = SeedCategory(db);
        var prod = BuildProduct(cat.Id, rating: null);
        db.AppSumoProducts.Add(prod);
        await db.SaveChangesAsync();

        var loaded = await db.AppSumoProducts.AsNoTracking().SingleAsync(p => p.Id == prod.Id);
        Assert.Null(loaded.OverallRating);
    }

    // ── AppSumoReview ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Review_CanBeSavedWithTacoRating()
    {
        var db   = TestApplicationDbContextFactory.Create();
        var cat  = SeedCategory(db);
        var prod = SeedProduct(db, cat.Id);

        var review = new AppSumoReview
        {
            ProductId      = prod.Id,
            TacoRating     = 2,
            ReviewerName   = "Jane Doe",
            ReviewDate     = new DateOnly(2024, 6, 15),
            ReviewText     = "Missing key integrations. Zapier support is limited.",
            AppSumoReviewId = "rev-001",
            CreatedAt      = DateTime.UtcNow
        };
        db.AppSumoReviews.Add(review);
        await db.SaveChangesAsync();

        var loaded = await db.AppSumoReviews.AsNoTracking().SingleAsync(r => r.Id == review.Id);
        Assert.Equal(2, loaded.TacoRating);
        Assert.Equal("Jane Doe", loaded.ReviewerName);
        Assert.Equal("Missing key integrations. Zapier support is limited.", loaded.ReviewText);
        Assert.Equal(new DateOnly(2024, 6, 15), loaded.ReviewDate);
    }

    [Fact]
    public async Task Reviews_MultipleRatings_FilteredCorrectly()
    {
        var db   = TestApplicationDbContextFactory.Create();
        var cat  = SeedCategory(db);
        var prod = SeedProduct(db, cat.Id);

        db.AppSumoReviews.AddRange(
            new AppSumoReview { ProductId = prod.Id, TacoRating = 1, ReviewText = "Terrible, crashed on day 1." },
            new AppSumoReview { ProductId = prod.Id, TacoRating = 2, ReviewText = "Needs more features." },
            new AppSumoReview { ProductId = prod.Id, TacoRating = 3, ReviewText = "Decent but pricey." },
            new AppSumoReview { ProductId = prod.Id, TacoRating = 5, ReviewText = "Love it!" }
        );
        await db.SaveChangesAsync();

        var lowRating = await db.AppSumoReviews
            .Where(r => r.TacoRating <= 3)
            .ToListAsync();

        Assert.Equal(3, lowRating.Count);
        Assert.DoesNotContain(lowRating, r => r.TacoRating > 3);
    }

    // ── AppSumoScrapeRun ──────────────────────────────────────────────────────

    [Fact]
    public async Task ScrapeRun_DefaultStatus_IsRunning()
    {
        var db  = TestApplicationDbContextFactory.Create();
        var run = new AppSumoScrapeRun { StartedAt = DateTime.UtcNow };
        db.AppSumoScrapeRuns.Add(run);
        await db.SaveChangesAsync();

        var loaded = await db.AppSumoScrapeRuns.AsNoTracking().SingleAsync(r => r.Id == run.Id);
        Assert.Equal("Running", loaded.Status);
        Assert.Null(loaded.FinishedAt);
        Assert.Equal(0, loaded.ReviewsSaved);
        Assert.Equal(0, loaded.ProductsScraped);
    }

    [Fact]
    public async Task ScrapeRun_CanBeMarkedCompleted()
    {
        var db  = TestApplicationDbContextFactory.Create();
        var run = new AppSumoScrapeRun { StartedAt = DateTime.UtcNow };
        db.AppSumoScrapeRuns.Add(run);
        await db.SaveChangesAsync();

        run.Status        = "Completed";
        run.FinishedAt    = DateTime.UtcNow;
        run.ProductsScraped = 42;
        run.ReviewsSaved  = 187;
        db.AppSumoScrapeRuns.Update(run);
        await db.SaveChangesAsync();

        var loaded = await db.AppSumoScrapeRuns.AsNoTracking().SingleAsync(r => r.Id == run.Id);
        Assert.Equal("Completed", loaded.Status);
        Assert.NotNull(loaded.FinishedAt);
        Assert.Equal(42, loaded.ProductsScraped);
        Assert.Equal(187, loaded.ReviewsSaved);
    }

    // ── ProductScrapeState ────────────────────────────────────────────────────

    [Fact]
    public async Task ScrapeState_CanBeCreatedAndUpdated()
    {
        var db   = TestApplicationDbContextFactory.Create();
        var cat  = SeedCategory(db);
        var prod = SeedProduct(db, cat.Id);
        var run  = SeedRun(db);

        var state = new ProductScrapeState
        {
            ProductId    = prod.Id,
            LastRunId    = run.Id,
            Status       = "Pending",
            AttemptCount = 0
        };
        db.ProductScrapeStates.Add(state);
        await db.SaveChangesAsync();

        state.Status       = "Done";
        state.AttemptCount = 1;
        state.UpdatedAt    = DateTime.UtcNow;
        db.ProductScrapeStates.Update(state);
        await db.SaveChangesAsync();

        var loaded = await db.ProductScrapeStates.AsNoTracking().SingleAsync(s => s.ProductId == prod.Id);
        Assert.Equal("Done", loaded.Status);
        Assert.Equal(1, loaded.AttemptCount);
        Assert.Null(loaded.LastError);
    }

    [Fact]
    public async Task ScrapeState_CanStoreError()
    {
        var db   = TestApplicationDbContextFactory.Create();
        var cat  = SeedCategory(db);
        var prod = SeedProduct(db, cat.Id);
        var run  = SeedRun(db);

        var state = new ProductScrapeState
        {
            ProductId    = prod.Id,
            LastRunId    = run.Id,
            Status       = "Failed",
            AttemptCount = 2,
            LastError    = "Bot challenge detected",
            UpdatedAt    = DateTime.UtcNow
        };
        db.ProductScrapeStates.Add(state);
        await db.SaveChangesAsync();

        var loaded = await db.ProductScrapeStates.AsNoTracking().SingleAsync(s => s.ProductId == prod.Id);
        Assert.Equal("Failed", loaded.Status);
        Assert.Equal("Bot challenge detected", loaded.LastError);
        Assert.Equal(2, loaded.AttemptCount);
    }

    // ── Cascade: deleting category removes products and reviews ───────────────

    [Fact]
    public async Task Category_Delete_CascadesToProductsAndReviews()
    {
        var db   = TestApplicationDbContextFactory.Create();
        var cat  = SeedCategory(db);
        var prod = SeedProduct(db, cat.Id);
        db.AppSumoReviews.Add(new AppSumoReview
        {
            ProductId  = prod.Id,
            TacoRating = 1,
            ReviewText = "Bad product"
        });
        await db.SaveChangesAsync();

        db.AppSumoCategories.Remove(cat);
        await db.SaveChangesAsync();

        Assert.Equal(0, await db.AppSumoProducts.CountAsync());
        Assert.Equal(0, await db.AppSumoReviews.CountAsync());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AppSumoCategory BuildCategory() => new()
    {
        Name      = "Operations",
        Slug      = "software/operations",
        Url       = "/software/operations",
        CreatedAt = DateTime.UtcNow
    };

    private static AppSumoCategory SeedCategory(backend.Infrastructure.Data.ApplicationDbContext db)
    {
        var cat = BuildCategory();
        db.AppSumoCategories.Add(cat);
        db.SaveChanges();
        return cat;
    }

    private static AppSumoProduct BuildProduct(int categoryId, decimal? rating = 4.5m) => new()
    {
        CategoryId    = categoryId,
        Name          = "TidyCal",
        Slug          = "tidycal",
        Url           = "https://appsumo.com/products/tidycal/",
        Description   = "Simple scheduling tool",
        OverallRating = rating,
        PricingModel  = "Lifetime Deal",
        CreatedAt     = DateTime.UtcNow
    };

    private static AppSumoProduct SeedProduct(backend.Infrastructure.Data.ApplicationDbContext db, int categoryId)
    {
        var prod = BuildProduct(categoryId);
        db.AppSumoProducts.Add(prod);
        db.SaveChanges();
        return prod;
    }

    private static AppSumoScrapeRun SeedRun(backend.Infrastructure.Data.ApplicationDbContext db)
    {
        var run = new AppSumoScrapeRun { StartedAt = DateTime.UtcNow, Status = "Running" };
        db.AppSumoScrapeRuns.Add(run);
        db.SaveChanges();
        return run;
    }
}
