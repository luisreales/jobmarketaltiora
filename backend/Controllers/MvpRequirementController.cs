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
[Route("api/mvp-requirements")]
public class MvpRequirementController(
    ApplicationDbContext dbContext,
    ISemanticKernelProvider kernelProvider,
    ILogger<MvpRequirementController> logger) : ControllerBase
{
    private const string SystemPrompt =
        """
        Act as a Lead Software Architect. Based on this product idea, generate the MVP Technical Requirements.
        Return STRICTLY a JSON object with no additional text, no markdown, no code blocks:
        {
          "architectureStrategy": "High-level architecture description (2-3 sentences).",
          "requiredTechStack": ["tech1", "tech2", "tech3"],
          "estimatedTimelines": "Sprint breakdown (e.g., Sprint 1: Auth & Core, Sprint 2: API, Sprint 3: UI — 6 weeks total).",
          "coreFeatures": ["Feature 1 — description", "Feature 2 — description", "Feature 3 — description"]
        }
        """;

    // ── GET /api/mvp-requirements ─────────────────────────────────────────────

    [HttpGet]
    public async Task<ActionResult<PagedResultDto<MvpRequirementDto>>> GetAll(
        [FromQuery] MvpRequirementQuery query,
        CancellationToken cancellationToken)
    {
        var page     = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 50);

        IQueryable<MvpRequirement> q = dbContext.MvpRequirements
            .AsNoTracking()
            .Include(m => m.Product);

        if (query.ProductId.HasValue)
            q = q.Where(m => m.ProductId == query.ProductId.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            q = q.Where(m =>
                m.ProductName.ToLower().Contains(term) ||
                m.CompanyContext.ToLower().Contains(term) ||
                m.ArchitectureStrategy.ToLower().Contains(term));
        }

        var totalCount = await q.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));

        var items = await q
            .OrderByDescending(m => m.GeneratedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Ok(new PagedResultDto<MvpRequirementDto>(
            items.Select(ToDto).ToList(),
            page, pageSize, totalCount, totalPages, "generatedAt", "desc"));
    }

    // ── GET /api/mvp-requirements/{id} ───────────────────────────────────────

    [HttpGet("{id:int}")]
    public async Task<ActionResult<MvpRequirementDto>> GetById(
        int id, CancellationToken cancellationToken)
    {
        var item = await dbContext.MvpRequirements
            .AsNoTracking()
            .Include(m => m.Product)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

        if (item is null)
            return NotFound(new { message = $"MVP requirement {id} not found." });

        return Ok(ToDto(item));
    }

    // ── POST /api/mvp-requirements/generate ──────────────────────────────────

    [HttpPost("generate")]
    public async Task<ActionResult<MvpRequirementDto>> Generate(
        [FromBody] GenerateMvpRequirementRequest request)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(310));
        var ct = cts.Token;

        if (string.IsNullOrWhiteSpace(request.Context))
            return BadRequest(new { message = "Context is required." });

        // Cache hit: if productId provided and already has an MVP requirement, return it
        if (request.ProductId.HasValue)
        {
            var cached = await dbContext.MvpRequirements
                .AsNoTracking()
                .Include(m => m.Product)
                .FirstOrDefaultAsync(m => m.ProductId == request.ProductId.Value, ct);

            if (cached is not null)
            {
                logger.LogInformation("MvpRequirementController: cache hit for product {Id}.", request.ProductId);
                return Ok(ToDto(cached));
            }
        }

        var kernel = kernelProvider.GetKernel();
        if (kernel is null)
            return Problem("Semantic Kernel is not available.", statusCode: StatusCodes.Status503ServiceUnavailable);

        ProductSuggestion? product = null;
        string productName = "Unknown";
        string promptText  = request.Context.Trim();

        if (request.ProductId.HasValue)
        {
            product = await dbContext.ProductSuggestions
                .AsNoTracking()
                .Include(p => p.Opportunity)
                .FirstOrDefaultAsync(p => p.Id == request.ProductId.Value, ct);

            if (product is not null)
            {
                productName = product.ProductName;
                var sb = new StringBuilder();
                sb.AppendLine($"Product/Service: {product.ProductName}");
                sb.AppendLine($"Description: {product.ProductDescription}");
                if (!string.IsNullOrWhiteSpace(product.TechFocus) && product.TechFocus != "To be defined")
                    sb.AppendLine($"Tech Focus: {product.TechFocus}");
                if (!string.IsNullOrWhiteSpace(request.Context))
                    sb.AppendLine($"Additional context: {request.Context}");
                if (product.Opportunity is not null)
                {
                    sb.AppendLine($"Target Company: {product.Opportunity.Company}");
                    sb.AppendLine($"Job: {product.Opportunity.JobTitle}");
                }
                promptText = sb.ToString();
            }
        }
        else
        {
            productName = ExtractProductNameFromContext(request.Context);
        }

        var sw = Stopwatch.StartNew();
        var responseText = string.Empty;

        try
        {
            var chat = kernel.GetRequiredService<IChatCompletionService>();
            var history = new ChatHistory();
            history.AddSystemMessage(SystemPrompt);
            history.AddUserMessage(promptText);

            var result = await chat.GetChatMessageContentAsync(history, cancellationToken: ct);
            responseText = result.Content ?? string.Empty;
            sw.Stop();

            var json = ParseAndValidateMvp(responseText);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var techStack = root.GetProperty("requiredTechStack").GetRawText();
            var features  = root.GetProperty("coreFeatures").GetRawText();

            var entity = new MvpRequirement
            {
                ProductId            = request.ProductId,
                ProductName          = productName,
                CompanyContext       = request.Context.Trim(),
                ArchitectureStrategy = root.GetProperty("architectureStrategy").GetString() ?? string.Empty,
                RequiredTechStackJson = techStack,
                EstimatedTimelines   = root.GetProperty("estimatedTimelines").GetString() ?? string.Empty,
                CoreFeaturesJson     = features,
                GeneratedAt          = DateTime.UtcNow
            };

            dbContext.MvpRequirements.Add(entity);

            // Also update product's TechnicalMvpJson for backward compat
            if (product is not null)
            {
                var tracked = await dbContext.ProductSuggestions
                    .FirstOrDefaultAsync(p => p.Id == product.Id, ct);
                if (tracked is not null)
                    tracked.TechnicalMvpJson = json;
            }

            LogPrompt(promptText, responseText, isSuccess: true, null, (int)sw.ElapsedMilliseconds);
            await dbContext.SaveChangesAsync(ct);

            await dbContext.Entry(entity).Reference(e => e.Product).LoadAsync(ct);

            logger.LogInformation("MvpRequirementController: requirement {Id} generated in {Ms}ms.", entity.Id, sw.ElapsedMilliseconds);
            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, ToDto(entity));
        }
        catch (OperationCanceledException)
        {
            return Problem("The AI analysis timed out. Please try again.", statusCode: StatusCodes.Status408RequestTimeout);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MvpRequirementController: generation failed.");
            LogPrompt(promptText, responseText, isSuccess: false, ex.Message, (int)sw.ElapsedMilliseconds);
            try { await dbContext.SaveChangesAsync(CancellationToken.None); } catch { /* best-effort */ }
            return Problem("The AI returned an unexpected response. Please try again.", statusCode: StatusCodes.Status502BadGateway);
        }
    }

    // ── PATCH /api/mvp-requirements/{id}/link ─────────────────────────────────

    [HttpPatch("{id:int}/link")]
    public async Task<ActionResult<MvpRequirementDto>> Link(
        int id,
        [FromBody] LinkMvpToProductRequest request,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.MvpRequirements
            .Include(m => m.Product)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

        if (item is null)
            return NotFound(new { message = $"MVP requirement {id} not found." });

        if (request.ProductId.HasValue)
        {
            var exists = await dbContext.ProductSuggestions
                .AnyAsync(p => p.Id == request.ProductId.Value, cancellationToken);
            if (!exists)
                return BadRequest(new { message = $"Product {request.ProductId} not found." });
        }

        item.ProductId = request.ProductId;
        item.Product   = request.ProductId.HasValue
            ? await dbContext.ProductSuggestions.FindAsync([request.ProductId.Value], cancellationToken: cancellationToken)
            : null;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("MvpRequirementController: requirement {Id} linked to product {ProductId}.", id, request.ProductId);
        return Ok(ToDto(item));
    }

    // ── DELETE /api/mvp-requirements/{id} ─────────────────────────────────────

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var item = await dbContext.MvpRequirements
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

        if (item is null)
            return NotFound(new { message = $"MVP requirement {id} not found." });

        dbContext.MvpRequirements.Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("MvpRequirementController: requirement {Id} deleted.", id);
        return NoContent();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static MvpRequirementDto ToDto(MvpRequirement m) => new(
        m.Id,
        m.ProductId,
        m.Product?.ProductName,
        m.ProductName,
        m.CompanyContext,
        m.ArchitectureStrategy,
        m.RequiredTechStackJson,
        m.EstimatedTimelines,
        m.CoreFeaturesJson,
        m.GeneratedAt);

    private static string ParseAndValidateMvp(string raw)
    {
        var json = StripMarkdownFences(raw);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        foreach (var field in new[] { "architectureStrategy", "requiredTechStack", "estimatedTimelines", "coreFeatures" })
        {
            if (!root.TryGetProperty(field, out _))
                throw new InvalidOperationException($"LLM response missing field '{field}'.");
        }
        return json;
    }

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

    private static string ExtractProductNameFromContext(string context)
    {
        var first = context.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? context;
        return first.Length > 100 ? first[..100] : first;
    }

    private void LogPrompt(string promptText, string responseText, bool isSuccess, string? errorMessage, int latencyMs)
    {
        using var sha = SHA256.Create();
        var hash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(promptText)))[..16].ToLower();
        dbContext.AiPromptLogs.Add(new AiPromptLog
        {
            Provider      = "SemanticKernel",
            ModelId       = "claude-4-6",
            PromptVersion = "mvp-requirement-v1",
            PromptHash    = hash,
            PromptText    = promptText,
            ResponseText  = responseText,
            IsSuccess     = isSuccess,
            Status        = isSuccess ? "success" : "failed",
            ErrorMessage  = errorMessage,
            LatencyMs     = latencyMs,
            CreatedAt     = DateTime.UtcNow
        });
    }
}
