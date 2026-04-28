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
    // Returns all ideas (including orphaned ones), with linked opportunity info.

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
    // Update Name, BusinessJustification, and re-link to an Opportunity.

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

        // Validate target opportunity exists (when provided)
        if (request.OpportunityId.HasValue)
        {
            var exists = await dbContext.Opportunities
                .AnyAsync(o => o.Id == request.OpportunityId.Value, cancellationToken);
            if (!exists)
                return BadRequest(new { message = $"Opportunity {request.OpportunityId} not found." });
        }

        idea.Name                 = request.Name.Trim();
        idea.BusinessJustification = request.BusinessJustification.Trim();
        idea.OpportunityId        = request.OpportunityId;

        // Reload linked opportunity for the DTO
        if (idea.OpportunityId.HasValue)
        {
            idea.Opportunity = await dbContext.Opportunities
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == idea.OpportunityId.Value, cancellationToken);
        }
        else
        {
            idea.Opportunity = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("OpportunityIdeaController: idea '{Id}' updated.", id);
        return Ok(ToDto(idea));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static OpportunityIdeaDto ToDto(OpportunityIdea i) => new(
        i.Id,
        i.Name,
        i.BusinessJustification,
        i.OpportunityId,
        i.Opportunity?.Company,
        i.Opportunity?.JobTitle,
        i.CreatedAt);
}
