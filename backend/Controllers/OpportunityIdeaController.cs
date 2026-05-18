using backend.Application.Contracts;
using backend.Domain.Entities;
using backend.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Route("api/opportunity-ideas")]
public class OpportunityIdeaController(
    ApplicationDbContext dbContext,
    ILogger<OpportunityIdeaController> logger) : ControllerBase
{
    // ── GET /api/opportunity-ideas ────────────────────────────────────────────

    [HttpGet]
    public async Task<ActionResult<List<OpportunityIdeaDto>>> GetAll(CancellationToken cancellationToken)
    {
        var ideas = await dbContext.OpportunityIdeas
            .AsNoTracking()
            .Include(i => i.Opportunity)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(ideas.Select(ToDto).ToList());
    }

    // ── PUT /api/opportunity-ideas/{id} ───────────────────────────────────────

    [HttpPut("{id}")]
    public async Task<ActionResult<OpportunityIdeaDto>> Update(
        string id,
        [FromBody] UpdateOpportunityIdeaRequest request,
        CancellationToken cancellationToken)
    {
        var idea = await dbContext.OpportunityIdeas
            .Include(i => i.Opportunity)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (idea is null)
            return NotFound(new { message = $"Idea '{id}' not found." });

        if (request.OpportunityId.HasValue)
        {
            var exists = await dbContext.Opportunities
                .AnyAsync(o => o.Id == request.OpportunityId.Value, cancellationToken);
            if (!exists)
                return BadRequest(new { message = $"Opportunity {request.OpportunityId} not found." });
        }

        idea.Name                  = request.Name.Trim();
        idea.BusinessJustification = request.BusinessJustification.Trim();
        idea.OpportunityId         = request.OpportunityId;

        if (!string.IsNullOrWhiteSpace(request.Source))
            idea.Source = request.Source.Trim();

        idea.Opportunity = idea.OpportunityId.HasValue
            ? await dbContext.Opportunities.AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == idea.OpportunityId.Value, cancellationToken)
            : null;

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("OpportunityIdeaController: idea '{Id}' updated.", id);
        return Ok(ToDto(idea));
    }

    // ── POST /api/opportunity-ideas/{id}/convert ──────────────────────────────

    [HttpPost("{id}/convert")]
    public async Task<ActionResult<OpportunityIdeaDto>> ConvertToOpportunity(
        string id,
        CancellationToken cancellationToken)
    {
        var idea = await dbContext.OpportunityIdeas
            .Include(i => i.Opportunity)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (idea is null)
            return NotFound(new { message = $"Idea '{id}' not found." });

        if (idea.OpportunityId.HasValue)
            return Conflict(new { message = "Idea is already linked to an Opportunity." });

        var opp = new Opportunity
        {
            JobId     = null,
            Company   = idea.Source == "AppSumo" ? "AppSumo" : "Manual",
            JobTitle  = idea.Name,
            LlmStatus = "pending",
            Status    = "active",
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Opportunities.Add(opp);
        await dbContext.SaveChangesAsync(cancellationToken);

        idea.OpportunityId = opp.Id;
        idea.Opportunity   = opp;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("OpportunityIdeaController: idea '{Id}' converted to opportunity #{OppId}.", id, opp.Id);
        return Ok(ToDto(idea));
    }

    // ── GET /api/opportunity-ideas/{id}/reviews ───────────────────────────────

    [HttpGet("{id}/reviews")]
    public async Task<ActionResult<List<AppSumoReviewForIdeaDto>>> GetReviews(
        string id,
        CancellationToken cancellationToken)
    {
        var idea = await dbContext.OpportunityIdeas
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (idea is null)
            return NotFound(new { message = $"Idea '{id}' not found." });

        if (idea.AppSumoProductId is null)
            return Ok(Array.Empty<AppSumoReviewForIdeaDto>());

        var reviews = await dbContext.AppSumoReviews
            .AsNoTracking()
            .Where(r => r.ProductId == idea.AppSumoProductId.Value)
            .OrderBy(r => r.TacoRating)
            .ThenByDescending(r => r.ReviewDate)
            .Select(r => new AppSumoReviewForIdeaDto(
                r.Id,
                r.TacoRating,
                r.ReviewerName,
                r.ReviewDate,
                r.ReviewText,
                r.FoundHelpful,
                r.IsVerified))
            .ToListAsync(cancellationToken);

        return Ok(reviews);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static OpportunityIdeaDto ToDto(OpportunityIdea i) => new(
        i.Id,
        i.Name,
        i.BusinessJustification,
        i.OpportunityId,
        i.Opportunity?.Company,
        i.Opportunity?.JobTitle,
        i.CreatedAt,
        i.Source,
        i.AppSumoProductId);
}
