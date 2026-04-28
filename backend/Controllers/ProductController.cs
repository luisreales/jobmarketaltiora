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
[Route("api/products")]
public class ProductController(
    ApplicationDbContext dbContext,
    IProductGeneratorService productGenerator,
    IProductSynthesisService productSynthesis,
    ISemanticKernelProvider kernelProvider,
    ILogger<ProductController> logger) : ControllerBase
{
    private const string CommercialStrategySystemPromptTemplate =
        """
        Act as an elite B2B Technical Sales Strategist. We are going to sell the product/service '{ProductName}'
        to the company that posted this job. Focus strictly on MONEY and BUSINESS, not code.
        Return STRICTLY a JSON object with no additional text, no markdown, no code blocks:
        {
          "realBusinessProblem": "What is the actual business pain behind this job? (1 sentence, business terms)",
          "financialImpact": "Why this saves or makes them money (2 lines).",
          "mvpDefinition": "Scope of the Minimum Viable Product we will sell them first to enter quickly.",
          "targetBuyer": "Exact job title to contact (e.g., CTO) and Go-To-Market hook strategy.",
          "pricingStrategy": "Rough pricing model and estimate (e.g., Fixed $5k-$10k sprint).",
          "outreachMessage": "Short, aggressive, highly relevant cold email to the Target Buyer to close a discovery call."
        }
        """;
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

        IQueryable<ProductSuggestion> q = ApplyProductFilters(
            dbContext.ProductSuggestions
                .AsNoTracking()
                .Where(p => p.OpportunityId != null),
            query);

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

    // ── POST /api/products/from-cluster/{id} ─────────────────────────────────

    /// <summary>
    /// Shortcut: generates a ProductSuggestion directly from an actionable cluster,
    /// bypassing the Jobs → Opportunity funnel. Uses the existing rule-based generator.
    /// Returns 409 if a product for this cluster already exists.
    /// </summary>
    [HttpPost("from-cluster/{id:int}")]
    public async Task<ActionResult<ProductDto>> CreateFromCluster(int id, CancellationToken cancellationToken)
    {
        var cluster = await dbContext.MarketClusters
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (cluster is null)
            return NotFound(new { message = $"Cluster {id} not found." });

        if (!cluster.IsActionable)
            return Problem(
                title: "Cluster not actionable",
                detail: "Only actionable clusters can generate products directly.",
                statusCode: StatusCodes.Status422UnprocessableEntity);

        var product = await productGenerator.GenerateForClusterAsync(id, cancellationToken);

        if (product is null)
            return Problem(
                title: "No product generated",
                detail: "No catalog rule matched this cluster. Try Synthesize first.",
                statusCode: StatusCodes.Status422UnprocessableEntity);

        logger.LogInformation("ProductController: product '{Name}' created from cluster {ClusterId}.", product.ProductName, id);

        return CreatedAtAction(
            actionName: nameof(GetProduct),
            routeValues: new { id = product.Id },
            value: ToDto(product));
    }

    // ── POST /api/products/from-opportunity ──────────────────────────────────

    /// <summary>
    /// Creates a manual ProductSuggestion from a specific product idea generated by AI
    /// in the Opportunity detail page. Links the product to the Opportunity.
    /// </summary>
    [HttpPost("from-opportunity")]
    public async Task<ActionResult<ProductDto>> CreateFromOpportunity(
        [FromBody] CreateProductFromOpportunityRequest request,
        CancellationToken cancellationToken)
    {
        var opportunity = await dbContext.Opportunities
            .FirstOrDefaultAsync(o => o.Id == request.OpportunityId, cancellationToken);

        if (opportunity is null)
            return NotFound(new { message = $"Opportunity {request.OpportunityId} not found." });

        if (!string.IsNullOrWhiteSpace(request.SourceIdeaId))
        {
            var alreadyExists = await dbContext.ProductSuggestions
                .AsNoTracking()
                .AnyAsync(p => p.SourceIdeaId == request.SourceIdeaId, cancellationToken);

            if (alreadyExists)
            {
                return Conflict(new
                {
                    message = $"A product has already been created from idea '{request.SourceIdeaId}'."
                });
            }
        }

        var product = new ProductSuggestion
        {
            ProductName        = request.Name,
            ProductDescription = request.ShortTechnicalDescription,
            OpportunityId      = request.OpportunityId,
            SourceIdeaId       = request.SourceIdeaId,
            ClusterIdsJson     = "[]",
            WhyNow             = $"Created from {opportunity.Company} job — {opportunity.JobTitle}",
            Offer              = "Custom quote — define with client",
            ActionToday        = "Run Commercial Strategy AI to generate outreach message",
            TechFocus          = opportunity.TechStack ?? "To be defined",
            EstimatedBuildDays = 0,
            MinDealSizeUsd     = 0,
            MaxDealSizeUsd     = 0,
            TotalJobCount      = 1,
            AvgDirectClientRatio = 1.0,
            AvgUrgencyScore    = 0,
            TopBlueOceanScore  = 0,
            ClusterCount       = 0,
            OpportunityType    = "Manual",
            Industry           = "Unknown",
            PriorityScore      = 100,
            LlmStatus          = "pending",
            Status             = "open",
            GeneratedAt        = DateTime.UtcNow
        };

        dbContext.ProductSuggestions.Add(product);

        // Mark the opportunity as converted
        opportunity.Status = "converted";

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("ProductController: manual product '{Name}' created from opportunity {OppId}.",
            request.Name, request.OpportunityId);

        return CreatedAtAction(
            actionName: nameof(GetProduct),
            routeValues: new { id = product.Id },
            value: ToDto(product));
    }

    // ── POST /api/products/{id}/synthesize-strategy ───────────────────────────

    /// <summary>
    /// On-demand Commercial Strategy synthesis for a product.
    /// Generates: realBusinessProblem, financialImpact, mvpDefinition,
    ///            targetBuyer, pricingStrategy, outreachMessage.
    /// Returns cached result immediately if LlmStatus == "completed".
    /// </summary>
    [HttpPost("{id:int}/synthesize-strategy")]
    public async Task<ActionResult<ProductDto>> SynthesizeStrategy(int id)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(310));
        var ct = cts.Token;

        var product = await dbContext.ProductSuggestions
            .Include(p => p.Opportunity)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (product is null)
            return NotFound(new { message = $"Product {id} not found." });

        // Cache hit — already has commercial strategy
        if (product.LlmStatus == "completed" && product.SynthesisDetailJson is not null)
        {
            logger.LogInformation("ProductController: product {Id} strategy already synthesized (cache hit).", id);
            return Ok(ToDto(product));
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
        var systemPrompt = CommercialStrategySystemPromptTemplate.Replace("{ProductName}", product.ProductName);
        var promptText   = BuildStrategyPrompt(product);
        var promptHash   = ComputeHash(promptText);
        var responseText = string.Empty;

        try
        {
            var chat = kernel.GetRequiredService<IChatCompletionService>();
            var history = new ChatHistory();
            history.AddSystemMessage(systemPrompt);
            history.AddUserMessage(promptText);

            var result = await chat.GetChatMessageContentAsync(history, cancellationToken: ct);
            responseText = result.Content ?? string.Empty;
            sw.Stop();

            var strategyJson = ParseAndValidateStrategy(responseText);

            product.SynthesisDetailJson = strategyJson;
            product.LlmStatus           = "completed";

            SaveStrategyPromptLog(product.Id, promptText, promptHash, responseText,
                isSuccess: true, errorMessage: null, latencyMs: (int)sw.ElapsedMilliseconds);

            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation("ProductController: product {Id} strategy synthesized in {Ms}ms.", id, sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            product.LlmStatus = "failed";
            await TrySaveAsync(product, ct);
            return Problem(
                title: "AI synthesis timeout",
                detail: "The AI analysis took too long. Please try again.",
                statusCode: StatusCodes.Status408RequestTimeout);
        }
        catch (Exception ex)
        {
            sw.Stop();
            product.LlmStatus = "failed";
            logger.LogError(ex, "ProductController: strategy synthesis failed for product {Id}.", id);

            SaveStrategyPromptLog(product.Id, promptText, promptHash, responseText,
                isSuccess: false, errorMessage: ex.Message, latencyMs: (int)sw.ElapsedMilliseconds);

            await TrySaveAsync(product, ct);

            return Problem(
                title: "AI synthesis failed",
                detail: "The AI returned an unexpected response. Please try again.",
                statusCode: StatusCodes.Status502BadGateway);
        }

        return Ok(ToDto(product));
    }

    // ── POST /api/products/{id}/synthesize-technical-mvp ─────────────────────────

    private const string TechnicalMvpSystemPrompt =
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

    /// <summary>
    /// Generates MVP Technical Requirements for a product using AI.
    /// Returns cached result if already generated.
    /// </summary>
    [HttpPost("{id:int}/synthesize-technical-mvp")]
    public async Task<ActionResult<ProductDto>> SynthesizeTechnicalMvp(int id)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(310));
        var ct = cts.Token;

        var product = await dbContext.ProductSuggestions
            .Include(p => p.Opportunity)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (product is null)
            return NotFound(new { message = $"Product {id} not found." });

        // Cache hit
        if (product.TechnicalMvpJson is not null)
        {
            logger.LogInformation("ProductController: product {Id} technical MVP already generated (cache hit).", id);
            return Ok(ToDto(product));
        }

        var kernel = kernelProvider.GetKernel();
        if (kernel is null)
        {
            return Problem(
                title: "AI service not configured",
                detail: "Semantic Kernel is not available.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var sw = Stopwatch.StartNew();
        var promptText = BuildTechnicalMvpPrompt(product);
        var responseText = string.Empty;

        try
        {
            var chat = kernel.GetRequiredService<IChatCompletionService>();
            var history = new ChatHistory();
            history.AddSystemMessage(TechnicalMvpSystemPrompt);
            history.AddUserMessage(promptText);

            var result = await chat.GetChatMessageContentAsync(history, cancellationToken: ct);
            responseText = result.Content ?? string.Empty;
            sw.Stop();

            var technicalJson = ParseAndValidateTechnicalMvp(responseText);
            product.TechnicalMvpJson = technicalJson;

            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation("ProductController: product {Id} technical MVP generated in {Ms}ms.", id, sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            return Problem(
                title: "AI synthesis timeout",
                detail: "The AI analysis took too long. Please try again.",
                statusCode: StatusCodes.Status408RequestTimeout);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ProductController: technical MVP synthesis failed for product {Id}.", id);
            return Problem(
                title: "AI synthesis failed",
                detail: "The AI returned an unexpected response. Please try again.",
                statusCode: StatusCodes.Status502BadGateway);
        }

        return Ok(ToDto(product));
    }

    // ── PUT /api/products/{id} ───────────────────────────────────────────────

    /// <summary>
    /// Updates editable product information from the products dashboard.
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ProductDto>> UpdateProduct(
        int id,
        [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        var product = await dbContext.ProductSuggestions
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
            return NotFound(new { message = $"Product {id} not found." });

        var normalizedName = request.ProductName.Trim();
        var duplicateNameExists = await dbContext.ProductSuggestions
            .AsNoTracking()
            .AnyAsync(p => p.Id != id && p.ProductName == normalizedName, cancellationToken);

        if (duplicateNameExists)
            return Conflict(new { message = $"Another product already uses the name '{normalizedName}'." });

        product.ProductName = normalizedName;
        product.ProductDescription = request.ProductDescription.Trim();
        product.WhyNow = request.WhyNow.Trim();
        product.Offer = request.Offer.Trim();
        product.ActionToday = request.ActionToday.Trim();
        product.TechFocus = request.TechFocus.Trim();
        product.EstimatedBuildDays = Math.Max(0, request.EstimatedBuildDays);
        product.MinDealSizeUsd = Math.Max(0, request.MinDealSizeUsd);
        product.MaxDealSizeUsd = Math.Max(product.MinDealSizeUsd, request.MaxDealSizeUsd);
        product.OpportunityType = string.IsNullOrWhiteSpace(request.OpportunityType) ? product.OpportunityType : request.OpportunityType.Trim();
        product.Industry = string.IsNullOrWhiteSpace(request.Industry) ? "Unknown" : request.Industry.Trim();
        product.ImageUrl = NormalizeStoredImageUrl(request.ImageUrl);
        product.Status = string.Equals(request.Status, "closed", StringComparison.OrdinalIgnoreCase) ? "closed" : "open";

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(product));
    }

    // ── POST /api/products/{id}/image ───────────────────────────────────────

    /// <summary>
    /// Uploads a product image and stores the resulting public URL on the product.
    /// </summary>
    [HttpPost("{id:int}/image")]
    public async Task<ActionResult<ProductDto>> UploadProductImage(
        int id,
        IFormFile image,
        CancellationToken cancellationToken)
    {
        var product = await dbContext.ProductSuggestions
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
            return NotFound(new { message = $"Product {id} not found." });

        if (image is null || image.Length == 0)
            return BadRequest(new { message = "Please select an image file." });

        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".webp", ".gif"
        };

        var extension = Path.GetExtension(image.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !allowedExtensions.Contains(extension))
            return BadRequest(new { message = "Unsupported image format. Use png, jpg, jpeg, webp, or gif." });

        var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "products");
        Directory.CreateDirectory(uploadsDir);

        var safeName = $"product-{id}-{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var filePath = Path.Combine(uploadsDir, safeName);

        await using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await image.CopyToAsync(stream, cancellationToken);
        }

        var relativeUrl = $"/uploads/products/{safeName}";
        product.ImageUrl = relativeUrl;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(product));
    }

    // ── GET /api/products/export ─────────────────────────────────────────────

    /// <summary>
    /// Exports the current product list as an Excel-compatible CSV file.
    /// </summary>
    [HttpGet("export")]
    public async Task<IActionResult> ExportProductsCsv([FromQuery] ProductQuery query, CancellationToken cancellationToken)
    {
        var products = await ApplyProductFilters(
                dbContext.ProductSuggestions.AsNoTracking(),
                query)
            .OrderByDescending(p => p.PriorityScore)
            .ThenByDescending(p => p.GeneratedAt)
            .ToListAsync(cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("Id,ProductName,ProductDescription,WhyNow,Offer,ActionToday,TechFocus,EstimatedBuildDays,MinDealSizeUsd,MaxDealSizeUsd,TotalJobCount,AvgDirectClientRatio,AvgUrgencyScore,TopBlueOceanScore,ClusterCount,PriorityScore,OpportunityType,Industry,LlmStatus,GeneratedAt,OpportunityId,SourceIdeaId,ImageUrl,Status");

        foreach (var product in products)
        {
            sb.AppendLine(string.Join(",", new[]
            {
                EscapeCsv(product.Id.ToString()),
                EscapeCsv(product.ProductName),
                EscapeCsv(product.ProductDescription),
                EscapeCsv(product.WhyNow),
                EscapeCsv(product.Offer),
                EscapeCsv(product.ActionToday),
                EscapeCsv(product.TechFocus),
                EscapeCsv(product.EstimatedBuildDays.ToString()),
                EscapeCsv(product.MinDealSizeUsd.ToString("0.##")),
                EscapeCsv(product.MaxDealSizeUsd.ToString("0.##")),
                EscapeCsv(product.TotalJobCount.ToString()),
                EscapeCsv(product.AvgDirectClientRatio.ToString("0.####")),
                EscapeCsv(product.AvgUrgencyScore.ToString("0.##")),
                EscapeCsv(product.TopBlueOceanScore.ToString("0.##")),
                EscapeCsv(product.ClusterCount.ToString()),
                EscapeCsv(product.PriorityScore.ToString()),
                EscapeCsv(product.OpportunityType),
                EscapeCsv(product.Industry),
                EscapeCsv(product.LlmStatus),
                EscapeCsv(product.GeneratedAt.ToString("O")),
                EscapeCsv(product.OpportunityId?.ToString() ?? string.Empty),
                EscapeCsv(product.SourceIdeaId ?? string.Empty),
                EscapeCsv(product.ImageUrl ?? string.Empty),
                EscapeCsv(product.Status)
            }));
        }

        return File(
            Encoding.UTF8.GetBytes(sb.ToString()),
            "text/csv",
            $"products-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
    }

    // ── PATCH /api/products/{id}/close ────────────────────────────────────────────

    /// <summary>
    /// Marks a product as Closed (analysis done, ready for development team).
    /// </summary>
    [HttpPatch("{id:int}/close")]
    public async Task<ActionResult<ProductDto>> CloseProduct(int id, CancellationToken cancellationToken)
    {
        var product = await dbContext.ProductSuggestions
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
            return NotFound(new { message = $"Product {id} not found." });

        product.Status = "closed";
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("ProductController: product {Id} marked as closed.", id);
        return Ok(ToDto(product));
    }

    // ── DELETE /api/products/{id} ─────────────────────────────────────────────────

    /// <summary>
    /// Permanently deletes a product suggestion by ID.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteProduct(int id, CancellationToken cancellationToken)
    {
        var product = await dbContext.ProductSuggestions
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
            return NotFound(new { message = $"Product {id} not found." });

        if (product.OpportunityId.HasValue)
        {
            var hasRemainingProducts = await dbContext.ProductSuggestions
                .AsNoTracking()
                .AnyAsync(p => p.OpportunityId == product.OpportunityId.Value && p.Id != id, cancellationToken);

            if (!hasRemainingProducts)
            {
                var opportunity = await dbContext.Opportunities
                    .FirstOrDefaultAsync(o => o.Id == product.OpportunityId.Value, cancellationToken);

                if (opportunity is not null)
                    opportunity.Status = "active";
            }
        }

        dbContext.ProductSuggestions.Remove(product);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("ProductController: product {Id} deleted.", id);
        return NoContent();
    }

    // ── Projection ────────────────────────────────────────────────────────────────

    private static IQueryable<ProductSuggestion> ApplyProductFilters(
        IQueryable<ProductSuggestion> queryable,
        ProductQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.OpportunityType))
            queryable = queryable.Where(p => p.OpportunityType == query.OpportunityType);

        if (!string.IsNullOrWhiteSpace(query.Industry))
            queryable = queryable.Where(p => p.Industry == query.Industry);

        return queryable;
    }

    private static string? NormalizeStoredImageUrl(string? rawImageUrl)
    {
        if (string.IsNullOrWhiteSpace(rawImageUrl))
            return null;

        var normalized = rawImageUrl.Trim();

        if (Uri.TryCreate(normalized, UriKind.Absolute, out var absoluteUri) &&
            absoluteUri.AbsolutePath.StartsWith("/uploads/products/", StringComparison.OrdinalIgnoreCase))
        {
            return absoluteUri.AbsolutePath;
        }

        return normalized;
    }

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
        p.TechnicalMvpJson,
        p.LlmStatus,
        p.GeneratedAt,
        p.OpportunityId,
        p.SourceIdeaId,
        p.ImageUrl,
        p.Status);

    // ── Strategy helpers ──────────────────────────────────────────────────────

    private static string BuildStrategyPrompt(ProductSuggestion product)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Product/Service to sell: {product.ProductName}");
        sb.AppendLine($"Description: {product.ProductDescription}");

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
        else
        {
            sb.AppendLine($"Industry: {product.Industry}");
            sb.AppendLine($"Tech Focus: {product.TechFocus}");
            sb.AppendLine($"Why Now: {product.WhyNow}");
        }

        return sb.ToString();
    }

    private static string BuildTechnicalMvpPrompt(ProductSuggestion product)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Product/Service: {product.ProductName}");
        sb.AppendLine($"Description: {product.ProductDescription}");
        if (!string.IsNullOrWhiteSpace(product.TechFocus) && product.TechFocus != "To be defined")
            sb.AppendLine($"Tech Focus: {product.TechFocus}");
        if (product.Opportunity is not null)
        {
            sb.AppendLine($"Target Company: {product.Opportunity.Company}");
            sb.AppendLine($"Job: {product.Opportunity.JobTitle}");
        }
        return sb.ToString();
    }

    private static string ParseAndValidateTechnicalMvp(string raw)
    {
        var json = StripMarkdownFences(raw);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var required = new[] { "architectureStrategy", "requiredTechStack", "estimatedTimelines", "coreFeatures" };
        foreach (var field in required)
        {
            if (!root.TryGetProperty(field, out _))
                throw new InvalidOperationException($"LLM response missing field '{field}'. Raw: {raw[..Math.Min(200, raw.Length)]}");
        }

        return json;
    }

    private static string ParseAndValidateStrategy(string raw)
    {
        var json = StripMarkdownFences(raw);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var required = new[] { "realBusinessProblem", "financialImpact", "mvpDefinition",
                                "targetBuyer", "pricingStrategy", "outreachMessage" };

        foreach (var field in required)
        {
            if (!root.TryGetProperty(field, out _))
                throw new InvalidOperationException($"LLM response missing field '{field}'. Raw: {raw[..Math.Min(200, raw.Length)]}");
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

    private void SaveStrategyPromptLog(int productId, string promptText, string promptHash,
        string responseText, bool isSuccess, string? errorMessage, int latencyMs)
    {
        dbContext.AiPromptLogs.Add(new AiPromptLog
        {
            Provider      = "SemanticKernel",
            ModelId       = "claude-4-6",
            PromptVersion = $"product-strategy-v1-{productId}",
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

    private async Task TrySaveAsync(ProductSuggestion product, CancellationToken ct)
    {
        try { await dbContext.SaveChangesAsync(ct); }
        catch (Exception ex) { logger.LogError(ex, "ProductController: failed to persist error state for product {Id}.", product.Id); }
    }

    private static string EscapeCsv(string value)
    {
        var escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..16];
    }
}
