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
[Route("api/commercial-strategies")]
public class CommercialStrategyController(
    ApplicationDbContext dbContext,
    ISemanticKernelProvider kernelProvider,
    ILogger<CommercialStrategyController> logger) : ControllerBase
{
    private const string SystemPrompt =
        """
        Act as an elite B2B Technical Sales Strategist. We are going to sell the described product/service
        to the company or context provided. Focus strictly on MONEY and BUSINESS, not code.
        Return STRICTLY a JSON object with no additional text, no markdown, no code blocks:
        {
          "realBusinessProblem": "What is the actual business pain? (1 sentence, business terms)",
          "financialImpact": "Why this saves or makes them money (2 lines).",
          "mvpDefinition": "Scope of the Minimum Viable Product we will sell them first to enter quickly.",
          "targetBuyer": "Exact job title to contact (e.g., CTO) and Go-To-Market hook strategy.",
          "pricingStrategy": "Rough pricing model and estimate (e.g., Fixed $5k-$10k sprint).",
          "outreachMessage": "Short, aggressive, highly relevant cold email to the Target Buyer to close a discovery call."
        }
        """;

    // ── GET /api/commercial-strategies ───────────────────────────────────────

    [HttpGet]
    public async Task<ActionResult<PagedResultDto<CommercialStrategyDto>>> GetAll(
        [FromQuery] CommercialStrategyQuery query,
        CancellationToken cancellationToken)
    {
        var page     = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 50);

        IQueryable<CommercialStrategy> q = dbContext.CommercialStrategies
            .AsNoTracking()
            .Include(s => s.Product);

        if (query.ProductId.HasValue)
            q = q.Where(s => s.ProductId == query.ProductId.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            q = q.Where(s =>
                s.ProductName.ToLower().Contains(term) ||
                s.CompanyContext.ToLower().Contains(term) ||
                s.RealBusinessProblem.ToLower().Contains(term));
        }

        var totalCount = await q.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));

        var items = await q
            .OrderByDescending(s => s.GeneratedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Ok(new PagedResultDto<CommercialStrategyDto>(
            items.Select(ToDto).ToList(),
            page, pageSize, totalCount, totalPages, "generatedAt", "desc"));
    }

    // ── GET /api/commercial-strategies/{id} ──────────────────────────────────

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CommercialStrategyDto>> GetById(
        int id, CancellationToken cancellationToken)
    {
        var item = await dbContext.CommercialStrategies
            .AsNoTracking()
            .Include(s => s.Product)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (item is null)
            return NotFound(new { message = $"Commercial strategy {id} not found." });

        return Ok(ToDto(item));
    }

    // ── POST /api/commercial-strategies/generate ─────────────────────────────

    [HttpPost("generate")]
    public async Task<ActionResult<CommercialStrategyDto>> Generate(
        [FromBody] GenerateCommercialStrategyRequest request)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(310));
        var ct = cts.Token;

        if (string.IsNullOrWhiteSpace(request.Context))
            return BadRequest(new { message = "Context is required." });

        // Cache hit: if productId provided and already has a strategy, return it
        if (request.ProductId.HasValue)
        {
            var cached = await dbContext.CommercialStrategies
                .AsNoTracking()
                .Include(s => s.Product)
                .FirstOrDefaultAsync(s => s.ProductId == request.ProductId.Value, ct);

            if (cached is not null)
            {
                logger.LogInformation("CommercialStrategyController: cache hit for product {Id}.", request.ProductId);
                return Ok(ToDto(cached));
            }
        }

        var kernel = kernelProvider.GetKernel();
        if (kernel is null)
            return Problem("Semantic Kernel is not available.", statusCode: StatusCodes.Status503ServiceUnavailable);

        // Optionally enrich context from linked product
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
                if (!string.IsNullOrWhiteSpace(request.Context))
                    sb.AppendLine($"Additional context: {request.Context}");
                if (product.Opportunity is not null)
                {
                    sb.AppendLine($"Target Company: {product.Opportunity.Company}");
                    sb.AppendLine($"Job Posted: {product.Opportunity.JobTitle}");
                    if (!string.IsNullOrWhiteSpace(product.Opportunity.TechStack))
                        sb.AppendLine($"Tech Stack: {product.Opportunity.TechStack}");
                    if (!string.IsNullOrWhiteSpace(product.Opportunity.JobDescription))
                    {
                        sb.AppendLine();
                        sb.AppendLine("--- ORIGINAL JOB DESCRIPTION ---");
                        var desc = product.Opportunity.JobDescription;
                        sb.AppendLine(desc.Length > 3000 ? desc[..3000] : desc);
                    }
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

            var json = ParseAndValidateStrategy(responseText);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var entity = new CommercialStrategy
            {
                ProductId            = request.ProductId,
                ProductName          = productName,
                CompanyContext       = request.Context.Trim(),
                RealBusinessProblem  = root.GetProperty("realBusinessProblem").GetString() ?? string.Empty,
                FinancialImpact      = root.GetProperty("financialImpact").GetString() ?? string.Empty,
                MvpDefinition        = root.GetProperty("mvpDefinition").GetString() ?? string.Empty,
                TargetBuyer          = root.GetProperty("targetBuyer").GetString() ?? string.Empty,
                PricingStrategy      = root.GetProperty("pricingStrategy").GetString() ?? string.Empty,
                OutreachMessage      = root.GetProperty("outreachMessage").GetString() ?? string.Empty,
                GeneratedAt          = DateTime.UtcNow
            };

            dbContext.CommercialStrategies.Add(entity);

            // Also update product's SynthesisDetailJson for backward compat
            if (product is not null)
            {
                var tracked = await dbContext.ProductSuggestions
                    .FirstOrDefaultAsync(p => p.Id == product.Id, ct);
                if (tracked is not null)
                {
                    tracked.SynthesisDetailJson = json;
                    tracked.LlmStatus = "completed";
                }
            }

            LogPrompt(promptText, responseText, isSuccess: true, null, (int)sw.ElapsedMilliseconds);
            await dbContext.SaveChangesAsync(ct);

            // Reload nav property for DTO
            await dbContext.Entry(entity).Reference(e => e.Product).LoadAsync(ct);

            logger.LogInformation("CommercialStrategyController: strategy {Id} generated in {Ms}ms.", entity.Id, sw.ElapsedMilliseconds);
            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, ToDto(entity));
        }
        catch (OperationCanceledException)
        {
            return Problem("The AI analysis timed out. Please try again.", statusCode: StatusCodes.Status408RequestTimeout);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CommercialStrategyController: generation failed.");
            LogPrompt(promptText, responseText, isSuccess: false, ex.Message, (int)sw.ElapsedMilliseconds);
            try { await dbContext.SaveChangesAsync(CancellationToken.None); } catch { /* best-effort log save */ }
            return Problem("The AI returned an unexpected response. Please try again.", statusCode: StatusCodes.Status502BadGateway);
        }
    }

    // ── PATCH /api/commercial-strategies/{id}/link ────────────────────────────

    [HttpPatch("{id:int}/link")]
    public async Task<ActionResult<CommercialStrategyDto>> Link(
        int id,
        [FromBody] LinkStrategyToProductRequest request,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.CommercialStrategies
            .Include(s => s.Product)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (item is null)
            return NotFound(new { message = $"Commercial strategy {id} not found." });

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

        logger.LogInformation("CommercialStrategyController: strategy {Id} linked to product {ProductId}.", id, request.ProductId);
        return Ok(ToDto(item));
    }

    // ── DELETE /api/commercial-strategies/{id} ────────────────────────────────

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var item = await dbContext.CommercialStrategies
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (item is null)
            return NotFound(new { message = $"Commercial strategy {id} not found." });

        dbContext.CommercialStrategies.Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("CommercialStrategyController: strategy {Id} deleted.", id);
        return NoContent();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static CommercialStrategyDto ToDto(CommercialStrategy s) => new(
        s.Id,
        s.ProductId,
        s.Product?.ProductName,
        s.ProductName,
        s.CompanyContext,
        s.RealBusinessProblem,
        s.FinancialImpact,
        s.MvpDefinition,
        s.TargetBuyer,
        s.PricingStrategy,
        s.OutreachMessage,
        s.GeneratedAt);

    private static string ParseAndValidateStrategy(string raw)
    {
        var json = StripMarkdownFences(raw);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        foreach (var field in new[] { "realBusinessProblem", "financialImpact", "mvpDefinition",
                                      "targetBuyer", "pricingStrategy", "outreachMessage" })
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
            PromptVersion = "commercial-strategy-v1",
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
