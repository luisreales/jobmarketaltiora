using backend.Application.Contracts;
using backend.Application.Interfaces;
using backend.Domain.Entities;
using backend.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Route("api/products")]
public class ProductController(
    ApplicationDbContext dbContext,
    IProductGeneratorService productGenerator,
    IProductSynthesisService productSynthesis,
    ILogger<ProductController> logger) : ControllerBase
{
    /// <summary>
    /// Returns paginated product suggestions ordered by PriorityScore (highest first).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResultDto<ProductDto>>> GetProducts(
        [FromQuery] ProductQuery query,
        CancellationToken cancellationToken)
    {
        var page     = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 50);

        IQueryable<ProductSuggestion> q = dbContext.ProductSuggestions.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.OpportunityType))
            q = q.Where(p => p.OpportunityType == query.OpportunityType);

        if (!string.IsNullOrWhiteSpace(query.Industry))
            q = q.Where(p => p.Industry == query.Industry);

        var totalCount = await q.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));

        var items = await q
            .OrderByDescending(p => p.PriorityScore)
            .ThenByDescending(p => p.TopBlueOceanScore)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => ToDto(p))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResultDto<ProductDto>(items, page, pageSize, totalCount, totalPages, "priorityScore", "desc"));
    }

    /// <summary>
    /// Returns a single product suggestion by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductDto>> GetProduct(int id, CancellationToken cancellationToken)
    {
        var product = await dbContext.ProductSuggestions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
            return NotFound(new { message = $"Product {id} not found." });

        return Ok(ToDto(product));
    }

    /// <summary>
    /// Manually triggers product generation for all actionable clusters.
    /// Safe to call multiple times — upserts existing products.
    /// </summary>
    [HttpPost("generate")]
    public async Task<ActionResult<ProductGenerateResultDto>> GenerateProducts(CancellationToken cancellationToken)
    {
        logger.LogInformation("ProductController: manual product generation triggered.");

        var productsGenerated = await productGenerator.GenerateProductsAsync(cancellationToken);
        var actionableClusters = await dbContext.MarketClusters
            .AsNoTracking()
            .CountAsync(c => c.IsActionable, cancellationToken);

        return Ok(new ProductGenerateResultDto(
            ProductsGenerated: productsGenerated,
            ActionableClusters: actionableClusters,
            RanAt: DateTime.UtcNow));
    }

    /// <summary>
    /// On-demand LLM synthesis for a specific product.
    /// Returns cached result immediately if LlmStatus == "completed".
    /// </summary>
    [HttpPost("{id:int}/synthesize")]
    public async Task<ActionResult<ProductDto>> SynthesizeProduct(int id)
    {
        // Independent CancellationToken — LLM calls can exceed the default ASP.NET
        // request timeout. Gives 310 s, slightly above the SK HttpClient timeout (300 s).
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(310));

        var result = await productSynthesis.SynthesizeProductAsync(id, cts.Token);
        if (result is null)
            return NotFound(new { message = $"Product {id} not found." });

        return Ok(ToDto(result));
    }

    // ── Projection ────────────────────────────────────────────────────────────────

    private static ProductDto ToDto(ProductSuggestion p) => new(
        p.Id,
        p.ProductName,
        p.ProductDescription,
        p.WhyNow,
        p.Offer,
        p.ActionToday,
        p.TechFocus,
        p.EstimatedBuildDays,
        p.MinDealSizeUsd,
        p.MaxDealSizeUsd,
        p.TotalJobCount,
        p.AvgDirectClientRatio,
        p.AvgUrgencyScore,
        p.TopBlueOceanScore,
        p.ClusterCount,
        p.PriorityScore,
        p.OpportunityType,
        p.Industry,
        p.SynthesisDetailJson,
        p.LlmStatus,
        p.GeneratedAt);
}
