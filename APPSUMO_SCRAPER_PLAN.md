# AppSumo Complaint & Market Gap Scraper — Architecture Plan

## Project Summary

A .NET 9 + Playwright web scraper that navigates AppSumo's software directory, enters each product page, filters reviews by 1–3 Taco ratings (complaints / feature requests), and persists structured data to SQL Server for downstream Market Intelligence analysis.

---

## 1. Project Structure

```
AppSumoScraper/
├── AppSumoScraper.sln
├── src/
│   ├── AppSumoScraper.Console/          # Entry point (CLI runner)
│   │   ├── Program.cs
│   │   └── appsettings.json
│   ├── AppSumoScraper.Core/             # Domain + orchestration
│   │   ├── Models/
│   │   │   ├── Category.cs
│   │   │   ├── AppSumoProduct.cs
│   │   │   └── ProductReview.cs
│   │   ├── Interfaces/
│   │   │   ├── ICategoryExtractor.cs
│   │   │   ├── IProductListExtractor.cs
│   │   │   ├── IReviewExtractor.cs
│   │   │   └── IReviewRepository.cs
│   │   └── Orchestration/
│   │       └── ScraperOrchestrator.cs   # Main nested loop
│   ├── AppSumoScraper.Playwright/       # Browser automation layer
│   │   ├── BrowserSessionManager.cs     # Singleton page pool
│   │   ├── CategoryExtractor.cs
│   │   ├── ProductListExtractor.cs
│   │   └── ReviewExtractor.cs
│   └── AppSumoScraper.Data/             # EF Core / ADO.NET persistence
│       ├── ApplicationDbContext.cs
│       ├── Migrations/
│       └── Repositories/
│           └── ReviewRepository.cs
└── tests/
    └── AppSumoScraper.Tests/
```

---

## 2. Tech Stack

| Layer | Technology | Reason |
|---|---|---|
| Runtime | .NET 9 | LTS, top-class async support |
| Browser automation | Microsoft.Playwright | Handles JS-rendered DOM, can intercept network |
| Persistence | EF Core 9 + SQL Server | Easy schema migrations, already used in platform |
| Resilience | Polly v8 | Retry, circuit-breaker, rate-limit policies |
| Logging | Serilog + Seq | Structured logs, easy to query failed products |
| Configuration | `appsettings.json` + env vars | Separate dev / prod config |
| Scheduling (optional) | Hangfire | Background recurring scrape jobs |

---

## 3. Database Schema (T-SQL)

```sql
-- ─────────────────────────────────────────────────────────────────────────────
-- AppSumo Scraper — SQL Server schema
-- Safe to run on existing databases (checks before creating)
-- ─────────────────────────────────────────────────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AppSumoCategories')
BEGIN
    CREATE TABLE AppSumoCategories (
        Id            INT IDENTITY(1,1) PRIMARY KEY,
        Name          NVARCHAR(200)  NOT NULL,
        Slug          NVARCHAR(200)  NOT NULL,
        Url           NVARCHAR(500)  NOT NULL,
        ParentSlug    NVARCHAR(200)  NULL,          -- NULL = top-level category
        ScrapedAt     DATETIME2      NULL,
        CreatedAt     DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT UQ_AppSumoCategories_Slug UNIQUE (Slug)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AppSumoProducts')
BEGIN
    CREATE TABLE AppSumoProducts (
        Id                INT IDENTITY(1,1) PRIMARY KEY,
        CategoryId        INT            NOT NULL REFERENCES AppSumoCategories(Id) ON DELETE CASCADE,
        Name              NVARCHAR(300)  NOT NULL,
        Slug              NVARCHAR(300)  NOT NULL,
        Url               NVARCHAR(500)  NOT NULL,
        Description       NVARCHAR(MAX)  NULL,
        OverallRating     DECIMAL(3,2)   NULL,       -- e.g. 4.5
        TotalReviewCount  INT            NULL,
        PricingModel      NVARCHAR(100)  NULL,        -- "Lifetime Deal", "Monthly", etc.
        Tags              NVARCHAR(MAX)  NULL,        -- JSON array of tag strings
        ScrapedAt         DATETIME2      NULL,
        CreatedAt         DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT UQ_AppSumoProducts_Slug UNIQUE (Slug)
    );

    CREATE INDEX IX_AppSumoProducts_CategoryId ON AppSumoProducts (CategoryId);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AppSumoReviews')
BEGIN
    CREATE TABLE AppSumoReviews (
        Id              BIGINT IDENTITY(1,1) PRIMARY KEY,
        ProductId       INT             NOT NULL REFERENCES AppSumoProducts(Id) ON DELETE CASCADE,
        AppSumoReviewId NVARCHAR(100)   NULL,         -- native review id from DOM if present
        TacoRating      TINYINT         NOT NULL,      -- 1, 2, or 3 only
        ReviewerName    NVARCHAR(200)   NULL,
        ReviewDate      DATE            NULL,
        ReviewText      NVARCHAR(MAX)   NOT NULL,
        FoundHelpful    INT             NULL,
        IsVerified      BIT             NOT NULL DEFAULT 0,
        RawHtml         NVARCHAR(MAX)   NULL,          -- optional: keep raw HTML for re-parsing
        CreatedAt       DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT UQ_AppSumoReviews_ProductReviewId UNIQUE (ProductId, AppSumoReviewId)
    );

    CREATE INDEX IX_AppSumoReviews_ProductId    ON AppSumoReviews (ProductId);
    CREATE INDEX IX_AppSumoReviews_TacoRating   ON AppSumoReviews (TacoRating);
    CREATE INDEX IX_AppSumoReviews_ReviewDate    ON AppSumoReviews (ReviewDate DESC);
END;
GO

-- Scrape run log — track progress so the scraper is resumable
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ScrapeRuns')
BEGIN
    CREATE TABLE ScrapeRuns (
        Id              INT IDENTITY(1,1) PRIMARY KEY,
        StartedAt       DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
        FinishedAt      DATETIME2       NULL,
        Status          NVARCHAR(50)    NOT NULL DEFAULT 'Running',  -- Running | Completed | Failed
        ProductsScraped INT             NOT NULL DEFAULT 0,
        ReviewsSaved    INT             NOT NULL DEFAULT 0,
        ErrorCount      INT             NOT NULL DEFAULT 0,
        Notes           NVARCHAR(MAX)   NULL
    );
END;
GO

-- Per-product scrape state — resumable at product granularity
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductScrapeState')
BEGIN
    CREATE TABLE ProductScrapeState (
        ProductId       INT             NOT NULL PRIMARY KEY REFERENCES AppSumoProducts(Id) ON DELETE CASCADE,
        LastRunId       INT             NOT NULL REFERENCES ScrapeRuns(Id),
        Status          NVARCHAR(50)    NOT NULL DEFAULT 'Pending',  -- Pending | Done | Failed | Skipped
        AttemptCount    TINYINT         NOT NULL DEFAULT 0,
        LastError       NVARCHAR(MAX)   NULL,
        UpdatedAt       DATETIME2       NOT NULL DEFAULT GETUTCDATE()
    );
END;
GO
```

---

## 4. Orchestration — Nested Loop Logic

```
ScraperOrchestrator
│
├── 1. Load / refresh category list
│       GET https://appsumo.com/software/
│       CategoryExtractor.ExtractAll() → List<Category>
│       Upsert into AppSumoCategories
│
├── 2. For each Category (top-level then children)
│   │
│   ├── 3. Paginate product listing
│   │       ProductListExtractor.ExtractPage(url, page) → List<AppSumoProduct>
│   │       Repeat while nextPage exists
│   │       Upsert into AppSumoProducts
│   │
│   └── 4. For each Product
│       │   Skip if ProductScrapeState.Status == Done (resumable)
│       │
│       ├── 5. Navigate to product URL
│       ├── 6. Find "Tacos" filter dropdown
│       ├── 7. For each rating in [3, 2, 1]
│       │       Click rating option
│       │       Wait for review list to re-render
│       │       Paginate through all review pages
│       │       ReviewExtractor.ExtractVisible() → List<ProductReview>
│       │       Bulk upsert into AppSumoReviews (skip duplicates)
│       │
│       └── 8. Mark ProductScrapeState.Status = Done
```

---

## 5. Bot Detection & Anti-Ban Strategy

### 5.1 Browser Fingerprint
- Use Playwright with a **real Chromium** browser (not headless by default in prod — use `Headless = false` with `--window-size=1920,1080`)
- Set a realistic `UserAgent` matching the Chromium version
- Disable WebDriver flags: `--disable-blink-features=AutomationControlled`
- Randomise viewport size per session (1280–1920 width)

### 5.2 Human-like Delays
```csharp
// Between each product navigation
await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(1800, 4500)));

// Between taco rating filter clicks
await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(800, 2000)));

// Between review page turns
await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(600, 1500)));
```

### 5.3 Polly Retry Policy
```csharp
var retryPolicy = Policy
    .Handle<PlaywrightException>()
    .Or<TimeoutException>()
    .WaitAndRetryAsync(
        retryCount: 3,
        sleepDurationProvider: attempt =>
            TimeSpan.FromSeconds(Math.Pow(2, attempt)) + // exponential back-off
            TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000)),
        onRetry: (ex, delay, attempt, ctx) =>
            Log.Warning("Retry {Attempt} for {Url} after {Delay}ms — {Error}",
                attempt, ctx["url"], delay.TotalMilliseconds, ex.Message));
```

### 5.4 Rate Limiting
- Hard cap: **≤ 30 product pages / minute**
- Use `System.Threading.RateLimiter` (TokenBucketRateLimiter) to enforce ceiling
- If HTTP 429 or Cloudflare challenge page detected → sleep 5–15 min, then retry

### 5.5 Session Rotation
- Maintain a pool of 1–3 Playwright browser contexts (not pages) to simulate different users
- Each context has its own cookie jar; rotate on 429 or CAPTCHA detection
- Optionally integrate proxy rotation via `BrowserNewContextOptions.Proxy`

### 5.6 CAPTCHA Handling
- Detect by checking if page URL contains `/challenge` or page title contains "Just a moment"
- On detection: pause, emit alert log event, optionally integrate 2captcha / CapSolver SDK
- Mark product as `Skipped` in `ProductScrapeState` and continue

---

## 6. Key Implementation Details

### 6.1 CategoryExtractor
```csharp
public async Task<List<Category>> ExtractAllAsync(IPage page)
{
    await page.GotoAsync("https://appsumo.com/software/", new() { WaitUntil = WaitUntilState.NetworkIdle });
    // Target sidebar ul > li > a links matching pattern /software/*
    var links = await page.Locator("nav li a[href^='/software/']").AllAsync();
    ...
}
```

### 6.2 Taco Filter Interaction
```csharp
// Click the "All tacos" dropdown button
await page.Locator("div.flex.items-center.justify-between:has-text('All tacos')").ClickAsync();

// Wait for dropdown options to appear
await page.WaitForSelectorAsync("[data-testid='taco-option']", new() { State = WaitForSelectorState.Visible });

// Click "3 Tacos" option
await page.Locator("[data-testid='taco-option']:has-text('3')").ClickAsync();

// Wait for reviews to refresh
await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
```

### 6.3 Review Pagination
```csharp
while (true)
{
    var reviews = await ExtractVisibleReviewsAsync(page, tacoRating);
    await repository.UpsertBatchAsync(productId, reviews);

    var nextBtn = page.Locator("button[aria-label='Next page']:not([disabled])");
    if (await nextBtn.CountAsync() == 0) break;

    await nextBtn.ClickAsync();
    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    await Task.Delay(Random.Shared.Next(600, 1500));
}
```

### 6.4 Resumability
On startup, `ScraperOrchestrator` queries `ProductScrapeState` for all products with `Status != Done` and processes only those — a crashed run resumes exactly where it left off.

---

## 7. Configuration (`appsettings.json`)

```json
{
  "Scraper": {
    "StartUrl": "https://appsumo.com/software/",
    "Headless": false,
    "MinDelayMs": 1800,
    "MaxDelayMs": 4500,
    "MaxProductsPerMinute": 30,
    "RetryCount": 3,
    "TacoRatings": [3, 2, 1]
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=AppSumoScraper;Trusted_Connection=True;"
  }
}
```

---

## 8. Integration with Altiora Market Intelligence

Once data is in SQL Server, it feeds directly into the existing Market Intelligence Engine:

| AppSumo Table | Altiora Usage |
|---|---|
| `AppSumoReviews` (1–3 tacos) | Source for `Opportunity` detection — complaints become market gaps |
| `AppSumoProducts` | Seed for `ProductSuggestion` — competitor product catalog |
| `AppSumoCategories` | Category taxonomy for opportunity clustering |

Suggested integration flow:
1. Nightly scrape run populates `AppSumoReviews`
2. Hangfire job calls existing LLM pipeline: `POST /api/opportunities/generate` with review text as context
3. Opportunities auto-linked to relevant `ProductSuggestion` if slug matches

---

## 9. Development Phases

| Phase | Deliverable | Effort |
|---|---|---|
| 1 | DB schema + EF Core models + repository | 0.5 day |
| 2 | CategoryExtractor + ProductListExtractor | 1 day |
| 3 | ReviewExtractor + Taco filter interaction | 1.5 days |
| 4 | Orchestrator + resumability + Polly policies | 1 day |
| 5 | Rate limiting + bot detection mitigations | 0.5 day |
| 6 | Logging, dry-run mode, CLI flags | 0.5 day |
| **Total** | | **~5 days** |

---

## 10. Running the Scraper

```bash
# Install Playwright browsers (once)
cd src/AppSumoScraper.Console
dotnet tool install --global Microsoft.Playwright.CLI
playwright install chromium

# Dry-run (no DB writes, just logs what would be scraped)
dotnet run -- --dry-run --max-products 10

# Full run
dotnet run -- --start-category /software/operations

# Resume interrupted run
dotnet run -- --resume
```
