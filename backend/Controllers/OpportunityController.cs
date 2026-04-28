using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using backend.Application.Contracts;
using backend.Application.Interfaces;
using backend.Domain.Entities;
using backend.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace backend.Controllers;

[ApiController]
[Route("api/opportunities")]
public class OpportunityController(
    ApplicationDbContext dbContext,
    ISemanticKernelProvider kernelProvider,
    ILogger<OpportunityController> logger) : ControllerBase
{
    private const string TechnicalIdeationSystemPrompt =
        """
        Act as a CEO and B2B Deal Maker. Analyze this job opportunity.
        Focus on the MONEY and the BUSINESS GAP.
        What are 3 high-ticket MVP products or services we can build to solve their problem and generate revenue?
        Return STRICTLY a JSON object with no additional text, no markdown, no code blocks.
        Use this exact structure:
        {
          "productIdeas": [
            { "id": "url-friendly-slug-based-on-name", "name": "Short product name", "businessJustification": "1-2 sentence business case — what problem it solves and the revenue/cost impact." },
            { "id": "...", "name": "...", "businessJustification": "..." },
            { "id": "...", "name": "...", "businessJustification": "..." }
          ]
        }
        The "id" field must be a unique URL-friendly slug derived from the name (lowercase, hyphens, no spaces or special chars).
        """;

    // ── GET /api/opportunities ────────────────────────────────────────────────

    [HttpGet]
    public async Task<ActionResult<PagedResultDto<OpportunityDto>>> GetOpportunities(
        [FromQuery] OpportunityQuery query,
        CancellationToken cancellationToken)
    {
        var page     = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 50);

        IQueryable<Opportunity> q = dbContext.Opportunities.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.LlmStatus))
            q = q.Where(o => o.LlmStatus == query.LlmStatus);

        var totalCount = await q.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));

        var items = await q
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(o => o.Products)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(ToDto).ToList();

        return Ok(new PagedResultDto<OpportunityDto>(dtos, page, pageSize, totalCount, totalPages, "createdAt", "desc"));
    }

    // ── GET /api/opportunities/{id} ───────────────────────────────────────────

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OpportunityDto>> GetOpportunity(int id, CancellationToken cancellationToken)
    {
        var opportunity = await dbContext.Opportunities
            .AsNoTracking()
            .Include(o => o.Products)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (opportunity is null)
            return NotFound(new { message = $"Opportunity {id} not found." });

        return Ok(ToDto(opportunity));
    }

    // ── POST /api/opportunities/{id}/synthesize-ideas ─────────────────────────

    [HttpPost("{id:int}/synthesize-ideas")]
    public async Task<ActionResult<OpportunityDto>> SynthesizeIdeas(int id)
    {
        // Independent CancellationToken — LLM calls can exceed ASP.NET default request timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(310));
        var ct = cts.Token;

        var opportunity = await dbContext.Opportunities
            .Include(o => o.Products)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        if (opportunity is null)
            return NotFound(new { message = $"Opportunity {id} not found." });

        // Cache hit — already analyzed
        if (opportunity.LlmStatus == "completed")
        {
            logger.LogInformation("OpportunityController: opportunity {Id} already synthesized (cache hit).", id);
            return Ok(ToDto(opportunity));
        }

        var kernel = kernelProvider.GetKernel();
        if (kernel is null)
        {
            return Problem(
                title: "AI service not configured",
                detail: "Semantic Kernel is not available. Check SemanticKernel configuration.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var sw = Stopwatch.StartNew();
        var promptText = BuildJobPrompt(opportunity);
        var promptHash = ComputeHash(promptText);
        var responseText = string.Empty;

        try
        {
            var chat = kernel.GetRequiredService<IChatCompletionService>();
            var history = new ChatHistory();
            history.AddSystemMessage(TechnicalIdeationSystemPrompt);
            history.AddUserMessage(promptText);

            var result = await chat.GetChatMessageContentAsync(history, cancellationToken: ct);
            responseText = result.Content ?? string.Empty;
            sw.Stop();

            var parsedIdeas = ParseAndValidateProductIdeas(responseText);

            opportunity.LlmStatus     = "completed";
            opportunity.SynthesizedAt = DateTime.UtcNow;

            // ── Persist ideas to Idea Vault with globally unique IDs ──────────
            var persistedIdeas = new List<ParsedIdea>();
            var reservedIdeaIds = new HashSet<string>(
                await dbContext.OpportunityIdeas
                    .AsNoTracking()
                    .Select(i => i.Id)
                    .ToListAsync(ct),
                StringComparer.OrdinalIgnoreCase);

            foreach (var idea in parsedIdeas)
            {
                var existingIdea = await dbContext.OpportunityIdeas
                    .FirstOrDefaultAsync(i => i.Id == idea.Id, ct);

                if (existingIdea is not null)
                {
                    if (existingIdea.OpportunityId == opportunity.Id)
                    {
                        existingIdea.Name = idea.Name;
                        existingIdea.BusinessJustification = idea.BusinessJustification;
                        persistedIdeas.Add(new ParsedIdea(existingIdea.Id, existingIdea.Name, existingIdea.BusinessJustification));
                        continue;
                    }

                    var uniqueId = GenerateAvailableIdeaId(idea.Id, reservedIdeaIds);
                    dbContext.OpportunityIdeas.Add(new backend.Domain.Entities.OpportunityIdea
                    {
                        Id = uniqueId,
                        Name = idea.Name,
                        BusinessJustification = idea.BusinessJustification,
                        OpportunityId = opportunity.Id,
                        CreatedAt = DateTime.UtcNow
                    });
                    persistedIdeas.Add(new ParsedIdea(uniqueId, idea.Name, idea.BusinessJustification));
                    continue;
                }

                reservedIdeaIds.Add(idea.Id);
                dbContext.OpportunityIdeas.Add(new backend.Domain.Entities.OpportunityIdea
                {
                    Id = idea.Id,
                    Name = idea.Name,
                    BusinessJustification = idea.BusinessJustification,
                    OpportunityId = opportunity.Id,
                    CreatedAt = DateTime.UtcNow
                });
                persistedIdeas.Add(idea);
            }

            opportunity.ProductIdeasJson = JsonSerializer.Serialize(
                persistedIdeas.Select(i => new
                {
                    id = i.Id,
                    name = i.Name,
                    businessJustification = i.BusinessJustification
                }));

            SavePromptLog(opportunity.JobId, promptText, promptHash, responseText,
                isSuccess: true, errorMessage: null, latencyMs: (int)sw.ElapsedMilliseconds);

            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation("OpportunityController: opportunity {Id} synthesized in {Ms}ms.", id, sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            opportunity.LlmStatus = "failed";
            await TrySaveAsync(opportunity, ct);
            return Problem(
                title: "AI synthesis timeout",
                detail: "The AI analysis took too long. Please try again.",
                statusCode: StatusCodes.Status408RequestTimeout);
        }
        catch (Exception ex)
        {
            sw.Stop();
            opportunity.LlmStatus = "failed";
            logger.LogError(ex, "OpportunityController: synthesis failed for opportunity {Id}.", id);

            SavePromptLog(opportunity.JobId, promptText, promptHash, responseText,
                isSuccess: false, errorMessage: ex.Message, latencyMs: (int)sw.ElapsedMilliseconds);

            await TrySaveAsync(opportunity, ct);

            return Problem(
                title: "AI synthesis failed",
                detail: "The AI returned an unexpected response. Please try again.",
                statusCode: StatusCodes.Status502BadGateway);
        }

        return Ok(ToDto(opportunity));
    }

    // ── DELETE /api/opportunities/{id} ───────────────────────────────────────

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteOpportunity(int id, CancellationToken cancellationToken)
    {
        var opportunity = await dbContext.Opportunities
            .Include(o => o.Products)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (opportunity is null)
            return NotFound(new { message = $"Opportunity {id} not found." });

        dbContext.Opportunities.Remove(opportunity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string BuildJobPrompt(Opportunity opportunity)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Company: {opportunity.Company}");
        sb.AppendLine($"Job Title: {opportunity.JobTitle}");
        if (!string.IsNullOrWhiteSpace(opportunity.TechStack))
            sb.AppendLine($"Tech Stack: {opportunity.TechStack}");
        sb.AppendLine();
        sb.AppendLine("--- JOB DESCRIPTION ---");
        var description = opportunity.JobDescription ?? "No description available.";
        sb.AppendLine(description.Length > 4000 ? description[..4000] : description);
        return sb.ToString();
    }

    private record ParsedIdea(string Id, string Name, string BusinessJustification);

    private static List<ParsedIdea> ParseAndValidateProductIdeas(string raw)
    {
        var json = StripMarkdownFences(raw);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("productIdeas", out var ideas) || ideas.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException($"LLM response missing 'productIdeas' array. Raw: {raw[..Math.Min(200, raw.Length)]}");

        var parsedList = new List<ParsedIdea>();
        var usedSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var el in ideas.EnumerateArray())
        {
            var name = el.TryGetProperty("name", out var nameProp)
                ? (nameProp.GetString() ?? string.Empty).Trim()
                : string.Empty;

            if (string.IsNullOrWhiteSpace(name))
                continue;

            var requestedSlug = el.TryGetProperty("id", out var idProp)
                ? idProp.GetString() ?? string.Empty
                : string.Empty;

            var businessJustification = el.TryGetProperty("businessJustification", out var bjProp)
                ? bjProp.GetString() ?? string.Empty
                : el.TryGetProperty("shortTechnicalDescription", out var descProp)
                    ? descProp.GetString() ?? string.Empty
                    : string.Empty;

            var slug = MakeUniqueSlug(string.IsNullOrWhiteSpace(requestedSlug) ? name : requestedSlug, usedSlugs);
            parsedList.Add(new ParsedIdea(slug, name, businessJustification.Trim()));
        }

        if (parsedList.Count == 0)
            throw new InvalidOperationException("LLM response did not contain any valid product ideas.");

        return parsedList;
    }

    private static string GenerateAvailableIdeaId(string rawBaseId, ISet<string> reservedIds)
    {
        var baseId = NormalizeSlug(rawBaseId);
        var candidate = baseId;
        var suffix = 2;

        while (!reservedIds.Add(candidate))
        {
            candidate = $"{baseId}-{suffix}";
            suffix++;
        }

        return candidate;
    }

    private static string MakeUniqueSlug(string rawValue, ISet<string> usedSlugs)
    {
        var baseSlug = NormalizeSlug(rawValue);
        var candidate = baseSlug;
        var suffix = 2;

        while (!usedSlugs.Add(candidate))
        {
            candidate = $"{baseSlug}-{suffix}";
            suffix++;
        }

        return candidate;
    }

    private static string NormalizeSlug(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return "idea";

        var sb = new StringBuilder();
        var lastWasDash = false;

        foreach (var ch in rawValue.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
                lastWasDash = false;
                continue;
            }

            if (!lastWasDash)
            {
                sb.Append('-');
                lastWasDash = true;
            }
        }

        var normalized = sb.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? "idea" : normalized;
    }

    // ── Static projection ─────────────────────────────────────────────────────

    private static string StripMarkdownFences(string raw)
    {
        var json = raw.Trim();
        if (json.StartsWith("```"))
        {
            var firstNewline = json.IndexOf('\n');
            var lastFence    = json.LastIndexOf("```");
            if (firstNewline > 0 && lastFence > firstNewline)
                json = json[(firstNewline + 1)..lastFence].Trim();
        }
        return json;
    }

    private void SavePromptLog(int jobId, string promptText, string promptHash,
        string responseText, bool isSuccess, string? errorMessage, int latencyMs)
    {
        dbContext.AiPromptLogs.Add(new AiPromptLog
        {
            JobId         = jobId,
            Provider      = "SemanticKernel",
            ModelId       = "claude-4-6",
            PromptVersion = "opportunity-ideas-v1",
            PromptHash    = promptHash,
            PromptText    = promptText,
            ResponseText  = responseText,
            IsSuccess     = isSuccess,
            Status        = isSuccess ? "success" : "failed",
            ErrorMessage  = errorMessage,
            LatencyMs     = latencyMs,
            CreatedAt     = DateTime.UtcNow
        });
    }

    private async Task TrySaveAsync(Opportunity opportunity, CancellationToken ct)
    {
        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "OpportunityController: failed to persist error state for opportunity {Id}.", opportunity.Id);
        }
    }

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..16];
    }

    private static OpportunityDto ToDto(Opportunity o) => new(
        o.Id,
        o.JobId,
        o.Company,
        o.JobTitle,
        o.JobDescription,
        o.TechStack,
        o.ProductIdeasJson,
        o.LlmStatus,
        o.Status,
        o.CreatedAt,
        o.SynthesizedAt,
        o.Products
            .Select(p => p.SourceIdeaId)
            .Where(id => id is not null)
            .Select(id => id!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList());
}
