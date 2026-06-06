using backend.Application.Contracts;
using backend.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace backend.Controllers;

[ApiController]
[Route("api/revenue")]
public class RevenueController(
    ApplicationDbContext db,
    ILogger<RevenueController> logger) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<RevenueSummaryDto>> GetSummary(CancellationToken ct)
    {
        var actionable = await db.MarketClusters
            .AsNoTracking()
            .Where(c => c.IsActionable)
            .ToListAsync(ct);

        var totalPipeline = actionable
            .Where(c => c.EstimatedDealSizeUsd.HasValue)
            .Sum(c => c.EstimatedDealSizeUsd!.Value * (decimal)c.EstimatedCloseProbability);

        var avgCloseProb = actionable.Count > 0
            ? actionable.Average(c => c.EstimatedCloseProbability)
            : 0;

        var avgBoc = actionable.Count > 0
            ? actionable.Average(c => c.BlueOceanScore)
            : 0;

        // Funnel stats
        var totalJobs = await db.JobOffers.CountAsync(ct);
        var analyzedJobs = await db.JobInsights.CountAsync(ct);
        var clustered = await db.JobInsights.CountAsync(i => i.ClusterId != null, ct);
        var synthesized = actionable.Count(c => c.LlmStatus == "completed");
        var productsTotal = await db.ProductSuggestions.CountAsync(ct);
        var productsOpen = await db.ProductSuggestions.CountAsync(p => p.Status == "open", ct);

        var funnel = new FunnelStatsDto(
            totalJobs, analyzedJobs, clustered,
            actionable.Count, synthesized, productsTotal, productsOpen);

        // By service model
        var byServiceModel = actionable
            .Where(c => c.RecommendedServiceModel != null)
            .GroupBy(c => c.RecommendedServiceModel!)
            .Select(g => new ServiceModelRevenueDto(
                g.Key,
                g.Count(),
                g.Where(c => c.EstimatedDealSizeUsd.HasValue)
                    .Sum(c => c.EstimatedDealSizeUsd!.Value * (decimal)c.EstimatedCloseProbability),
                g.Average(c => c.EstimatedCloseProbability)))
            .OrderByDescending(x => x.WeightedValueUsd)
            .ToList();

        // By industry
        var byIndustry = actionable
            .GroupBy(c => c.Industry)
            .Select(g => new IndustryRevenueDto(
                g.Key,
                g.Max(c => c.EstimatedTam),
                g.Count(),
                g.Average(c => c.EstimatedCloseProbability),
                g.Where(c => c.EstimatedDealSizeUsd.HasValue)
                    .Sum(c => c.EstimatedDealSizeUsd!.Value * (decimal)c.EstimatedCloseProbability)))
            .OrderByDescending(x => x.EstimatedValueUsd)
            .ToList();

        // Determine which clusters already have a product
        var productClusterIds = await db.ProductSuggestions
            .AsNoTracking()
            .Select(p => p.ClusterIdsJson)
            .ToListAsync(ct);

        var coveredClusterIds = productClusterIds
            .SelectMany(j =>
            {
                try { return JsonSerializer.Deserialize<int[]>(j) ?? []; }
                catch { return []; }
            })
            .ToHashSet();

        // Top 20 by expected value
        var topOpportunities = actionable
            .Where(c => c.EstimatedDealSizeUsd.HasValue)
            .OrderByDescending(c => c.EstimatedDealSizeUsd!.Value * (decimal)c.EstimatedCloseProbability)
            .Take(20)
            .Select(c => new TopOpportunityDto(
                c.Id,
                c.Label,
                c.PainCategory,
                c.Industry,
                c.RecommendedServiceModel ?? "Unknown",
                c.EstimatedDealSizeUsd!.Value,
                c.EstimatedCloseProbability,
                c.EstimatedDealSizeUsd!.Value * (decimal)c.EstimatedCloseProbability,
                c.BlueOceanScore,
                c.BuyingIntentScore,
                c.JobCount,
                coveredClusterIds.Contains(c.Id)))
            .ToList();

        var summary = new RevenueSummaryDto(
            totalPipeline,
            totalPipeline,
            actionable.Count,
            productsTotal,
            productsOpen,
            avgCloseProb,
            avgBoc,
            funnel,
            byServiceModel,
            byIndustry,
            topOpportunities);

        return Ok(summary);
    }

    [HttpPatch("products/{id:int}/sales-status")]
    public async Task<IActionResult> UpdateSalesStatus(int id, [FromBody] SalesStatusUpdateDto dto, CancellationToken ct)
    {
        var product = await db.ProductSuggestions.FindAsync([id], ct);
        if (product is null) return NotFound();

        var valid = new[] { "new", "contacted", "qualified", "won", "lost" };
        if (!valid.Contains(dto.SalesStatus)) return BadRequest("Invalid SalesStatus.");

        product.SalesStatus = dto.SalesStatus;

        if (dto.SalesStatus == "won" && dto.WonDealSizeUsd.HasValue)
        {
            product.WonDealSizeUsd = dto.WonDealSizeUsd;
            product.ClosedAt = DateTime.UtcNow;
        }
        else if (dto.SalesStatus == "lost")
        {
            product.ClosedAt = DateTime.UtcNow;
        }
        else if (dto.SalesStatus == "contacted" && product.ContactedAt is null)
        {
            product.ContactedAt = DateTime.UtcNow;
        }

        if (dto.SalesNotes is not null)
            product.SalesNotes = dto.SalesNotes;

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Product {Id} SalesStatus updated to {Status}.", id, dto.SalesStatus);
        return NoContent();
    }
}
