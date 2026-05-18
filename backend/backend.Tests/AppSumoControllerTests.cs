using backend.Application.Contracts;
using backend.Controllers;
using backend.Domain.Entities;
using backend.Infrastructure.Data;
using backend.Infrastructure.Repositories;
using backend.Infrastructure.Services;
using backend.Tests.Fakes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace backend.Tests;

/// <summary>
/// Tests for AppSumoController endpoints.
/// AppSumoOrchestratorService is replaced with a no-op fake to avoid browser automation.
/// </summary>
public class AppSumoControllerTests
{
    // ── GET /api/appsumo/categories ───────────────────────────────────────────

    [Fact]
    public async Task GetCategories_WhenEmpty_ReturnsEmptyList()
    {
        var (controller, _) = Build();

        var result = await controller.GetCategories();

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<System.Collections.IEnumerable>(ok.Value);
        Assert.Empty(list.Cast<object>());
    }

    [Fact]
    public async Task GetCategories_WithData_ReturnsAllCategories()
    {
        var (controller, db) = Build();
        db.AppSumoCategories.AddRange(
            new AppSumoCategory { Name = "Operations", Slug = "software/operations", Url = "/software/operations" },
            new AppSumoCategory { Name = "Marketing",  Slug = "software/marketing",  Url = "/software/marketing"  }
        );
        await db.SaveChangesAsync();

        var result = await controller.GetCategories();

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IEnumerable<AppSumoCategory>>(ok.Value);
        Assert.Equal(2, list.Count());
    }

    // ── GET /api/appsumo/products ─────────────────────────────────────────────

    [Fact]
    public async Task GetProducts_WhenEmpty_ReturnsPaged_ZeroTotal()
    {
        var (controller, _) = Build();

        var result = await controller.GetProducts(new AppSumoProductQuery());

        var ok = Assert.IsType<OkObjectResult>(result);
        var paged = Assert.IsType<PagedResult<AppSumoProductDto>>(ok.Value);
        Assert.Equal(0, paged.TotalCount);
        Assert.Empty(paged.Items);
    }

    [Fact]
    public async Task GetProducts_WithSeededData_ReturnsTotalCount()
    {
        var (controller, db) = Build();
        var cat = SeedCategory(db);
        db.AppSumoProducts.AddRange(
            BuildProduct(cat.Id, "tidycal", "TidyCal"),
            BuildProduct(cat.Id, "appflowy", "AppFlowy"),
            BuildProduct(cat.Id, "notion-alt", "NotionAlt")
        );
        await db.SaveChangesAsync();

        var result = await controller.GetProducts(new AppSumoProductQuery { PageSize = 10 });

        var ok    = Assert.IsType<OkObjectResult>(result);
        var paged = Assert.IsType<PagedResult<AppSumoProductDto>>(ok.Value);
        Assert.Equal(3, paged.TotalCount);
        Assert.Equal(3, paged.Items.Count);
    }

    [Fact]
    public async Task GetProducts_SearchFilter_ReturnMatchingProducts()
    {
        var (controller, db) = Build();
        var cat = SeedCategory(db);
        db.AppSumoProducts.AddRange(
            BuildProduct(cat.Id, "tidycal",   "TidyCal"),
            BuildProduct(cat.Id, "tidyforms", "TidyForms"),
            BuildProduct(cat.Id, "appflowy",  "AppFlowy")
        );
        await db.SaveChangesAsync();

        var result = await controller.GetProducts(new AppSumoProductQuery { Search = "Tidy" });

        var ok    = Assert.IsType<OkObjectResult>(result);
        var paged = Assert.IsType<PagedResult<AppSumoProductDto>>(ok.Value);
        Assert.Equal(2, paged.TotalCount);
    }

    // ── GET /api/appsumo/reviews ──────────────────────────────────────────────

    [Fact]
    public async Task GetReviews_WhenEmpty_ReturnsEmptyPage()
    {
        var (controller, _) = Build();

        var result = await controller.GetReviews(new AppSumoReviewQuery());

        var ok    = Assert.IsType<OkObjectResult>(result);
        var paged = Assert.IsType<PagedResult<AppSumoReviewDto>>(ok.Value);
        Assert.Equal(0, paged.TotalCount);
    }

    [Fact]
    public async Task GetReviews_FilterByTacoRating_OnlyReturnsMatchingRating()
    {
        var (controller, db) = Build();
        var cat  = SeedCategory(db);
        var prod = SeedProduct(db, cat.Id);
        db.AppSumoReviews.AddRange(
            new AppSumoReview { ProductId = prod.Id, TacoRating = 1, ReviewText = "Terrible." },
            new AppSumoReview { ProductId = prod.Id, TacoRating = 2, ReviewText = "Not great." },
            new AppSumoReview { ProductId = prod.Id, TacoRating = 5, ReviewText = "Amazing!" }
        );
        await db.SaveChangesAsync();

        var result = await controller.GetReviews(new AppSumoReviewQuery { TacoRating = 1 });

        var ok    = Assert.IsType<OkObjectResult>(result);
        var paged = Assert.IsType<PagedResult<AppSumoReviewDto>>(ok.Value);
        Assert.Equal(1, paged.TotalCount);
        Assert.All(paged.Items, item => Assert.Equal(1, item.TacoRating));
    }

    // ── GET /api/appsumo/stats ────────────────────────────────────────────────

    [Fact]
    public async Task GetStats_ReturnsExpectedShape()
    {
        var (controller, db) = Build();
        var cat  = SeedCategory(db);
        var prod = SeedProduct(db, cat.Id);
        db.AppSumoReviews.Add(
            new AppSumoReview { ProductId = prod.Id, TacoRating = 2, ReviewText = "Meh." });
        await db.SaveChangesAsync();

        var result = await controller.GetStats();

        var ok   = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        var doc  = System.Text.Json.JsonDocument.Parse(json).RootElement;

        Assert.True(doc.TryGetProperty("categories", out _));
        Assert.True(doc.TryGetProperty("products",   out _));
        Assert.True(doc.TryGetProperty("reviews",    out _));
    }

    // ── POST /api/appsumo/scrape/start ────────────────────────────────────────

    [Fact]
    public void StartScrape_Returns202Accepted()
    {
        var (controller, _) = Build();

        var result = controller.StartScrape(new StartScrapeRequest(DryRun: true, MaxProducts: 5));

        Assert.IsType<AcceptedResult>(result);
    }

    [Fact]
    public void StartScrape_WithDryRun_Returns202()
    {
        var (controller, _) = Build();

        var result = controller.StartScrape(new StartScrapeRequest(
            StartCategorySlug: "software/operations",
            DryRun: true,
            MaxProducts: 3));

        var accepted = Assert.IsType<AcceptedResult>(result);
        Assert.NotNull(accepted.Value);
    }

    // ── GET /api/appsumo/runs ─────────────────────────────────────────────────

    [Fact]
    public async Task GetRuns_WhenNoRuns_ReturnsEmptyList()
    {
        var (controller, _) = Build();

        var result = await controller.GetRuns();

        var ok   = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IEnumerable<AppSumoScrapeRunDto>>(ok.Value);
        Assert.Empty(list);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (AppSumoController controller, ApplicationDbContext db) Build()
    {
        var db         = TestApplicationDbContextFactory.Create();
        var repo       = new AppSumoRepository(db);
        var fakeOrch   = new FakeOrchestratorService();
        var controller = new AppSumoController(repo, fakeOrch, NullLogger<AppSumoController>.Instance);
        return (controller, db);
    }

    private static AppSumoCategory SeedCategory(ApplicationDbContext db)
    {
        var cat = new AppSumoCategory
        {
            Name = "Operations", Slug = "software/operations", Url = "/software/operations"
        };
        db.AppSumoCategories.Add(cat);
        db.SaveChanges();
        return cat;
    }

    private static AppSumoProduct BuildProduct(int categoryId, string slug, string name) => new()
    {
        CategoryId = categoryId,
        Slug       = slug,
        Name       = name,
        Url        = $"https://appsumo.com/products/{slug}/",
        CreatedAt  = DateTime.UtcNow
    };

    private static AppSumoProduct SeedProduct(ApplicationDbContext db, int categoryId)
    {
        var prod = BuildProduct(categoryId, "tidycal", "TidyCal");
        db.AppSumoProducts.Add(prod);
        db.SaveChanges();
        return prod;
    }

    // No-op orchestrator — avoids any browser/Playwright invocation in unit tests
    private sealed class FakeOrchestratorService : IAppSumoOrchestratorService
    {
        public Task RunAsync(
            string? startCategorySlug, bool dryRun, int maxProducts, CancellationToken ct)
            => Task.CompletedTask;
    }
}
