using backend.Application.Contracts;
using backend.Application.Interfaces;
using backend.Infrastructure.Data;
using backend.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace backend.Controllers;

[ApiController]
[Route("api/ai")]
public class AiObservabilityController(
    ApplicationDbContext dbContext,
    IServiceScopeFactory scopeFactory,
    MarketIntelligenceExecutionTracker executionTracker,
    IConfiguration configuration,
    IOptions<MarketIntelligenceEnrichmentOptions> enrichmentOptions,
    IOptions<SemanticKernelOptions> semanticKernelOptions) : ControllerBase
{
    [HttpGet("logs")]
    public async Task<ActionResult<PagedResultDto<AiPromptLogDto>>> GetLogs([FromQuery] AiPromptLogsQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var logsQuery = dbContext.AiPromptLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Provider))
        {
            var provider = query.Provider.Trim();
            logsQuery = logsQuery.Where(x => EF.Functions.ILike(x.Provider, $"%{provider}%"));
        }

        if (!string.IsNullOrWhiteSpace(query.ModelId))
        {
            var modelId = query.ModelId.Trim();
            logsQuery = logsQuery.Where(x => EF.Functions.ILike(x.ModelId, $"%{modelId}%"));
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim();
            logsQuery = logsQuery.Where(x => EF.Functions.ILike(x.Status, $"%{status}%"));
        }

        if (query.ClusterId.HasValue)
        {
            logsQuery = logsQuery.Where(x => x.ClusterId == query.ClusterId.Value);
        }

        if (query.FromDate.HasValue)
        {
            logsQuery = logsQuery.Where(x => x.CreatedAt >= query.FromDate.Value);
        }

        if (query.ToDate.HasValue)
        {
            logsQuery = logsQuery.Where(x => x.CreatedAt <= query.ToDate.Value);
        }

        var totalCount = await logsQuery.CountAsync(cancellationToken);

        var sortBy = NormalizeSortBy(query.SortBy);
        var sortDirection = NormalizeSortDirection(query.SortDirection);

        logsQuery = ApplySorting(logsQuery, sortBy, sortDirection);

        var items = await logsQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AiPromptLogDto(
                x.Id,
                x.JobId,
                x.ClusterId,
                x.Provider,
                x.ModelId,
                x.PromptVersion,
                x.PromptHash,
                x.PromptText,
                x.ResponseText,
                x.CacheHit,
                x.IsSuccess,
                x.Status,
                x.ErrorMessage,
                x.PromptTokens,
                x.CompletionTokens,
                x.TotalTokens,
                x.LatencyMs,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));

        return Ok(new PagedResultDto<AiPromptLogDto>(items, page, pageSize, totalCount, totalPages, sortBy, sortDirection));
    }

    [HttpGet("llm-health")]
    public async Task<ActionResult<LlmHealthDto>> GetLlmHealth(CancellationToken cancellationToken = default)
    {
        var cfg = semanticKernelOptions.Value;
        var isConfigured = cfg.Enabled
            && !string.IsNullOrWhiteSpace(cfg.Provider)
            && !string.IsNullOrWhiteSpace(cfg.ModelId)
            && (!string.IsNullOrWhiteSpace(cfg.ApiKey)
                || !string.IsNullOrWhiteSpace(cfg.ApiKeyEnvVar)
                || !string.IsNullOrWhiteSpace(cfg.ApiKeys.OpenAI)
                || !string.IsNullOrWhiteSpace(cfg.ApiKeys.Gemini)
                || !string.IsNullOrWhiteSpace(cfg.ApiKeys.Copilot)
                || !string.IsNullOrWhiteSpace(cfg.ApiKeys.Perplexity));

        var lastSuccessAt = await dbContext.AiPromptLogs
            .AsNoTracking()
            .Where(x => x.IsSuccess)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => (DateTime?)x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var lastFailureAt = await dbContext.AiPromptLogs
            .AsNoTracking()
            .Where(x => !x.IsSuccess)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => (DateTime?)x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var isHealthy = isConfigured
            && lastSuccessAt.HasValue
            && (!lastFailureAt.HasValue || lastSuccessAt.Value >= lastFailureAt.Value)
            && lastSuccessAt.Value >= DateTime.UtcNow.AddMinutes(-30);

        var state = isHealthy ? "healthy" : (isConfigured ? "degraded" : "not-configured");
        var message = state switch
        {
            "healthy" => "LLM configurado y respondiendo correctamente.",
            "degraded" => "LLM configurado pero con errores recientes o sin exito reciente.",
            _ => "LLM no configurado. Revisa Provider, ModelId y API key."
        };

        return Ok(new LlmHealthDto(
            isConfigured,
            isHealthy,
            state,
            cfg.ModelId,
            lastSuccessAt,
            lastFailureAt,
            message));
    }

    [HttpGet("summary")]
    public async Task<ActionResult<AiUsageSummaryDto>> GetSummary([FromQuery] int windowDays = 7, CancellationToken cancellationToken = default)
    {
        var days = Math.Clamp(windowDays, 1, 90);
        var toDate = DateTime.UtcNow;
        var fromDate = toDate.AddDays(-days);

        var logs = dbContext.AiPromptLogs
            .AsNoTracking()
            .Where(x => x.CreatedAt >= fromDate && x.CreatedAt <= toDate);

        var totalCalls = await logs.CountAsync(cancellationToken);
        var successCalls = await logs.CountAsync(x => x.IsSuccess, cancellationToken);
        var failedCalls = await logs.CountAsync(x => !x.IsSuccess, cancellationToken);
        var cacheHits = await logs.CountAsync(x => x.CacheHit, cancellationToken);
        var totalTokens = await logs.SumAsync(x => x.TotalTokens, cancellationToken);
        var averageLatency = totalCalls == 0
            ? 0
            : await logs.AverageAsync(x => (double)x.LatencyMs, cancellationToken);

        return Ok(new AiUsageSummaryDto(fromDate, toDate, totalCalls, successCalls, failedCalls, cacheHits, totalTokens, Math.Round(averageLatency, 2)));
    }

    [HttpGet("prompts/{key}")]
    public async Task<ActionResult<AiPromptTemplateDto>> GetPromptTemplate([FromRoute] string key, CancellationToken cancellationToken)
    {
        var normalizedKey = NormalizeKey(key);
        if (string.IsNullOrWhiteSpace(normalizedKey))
        {
            return BadRequest(new { message = "Prompt key is required." });
        }

        var template = await dbContext.AiPromptTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == normalizedKey, cancellationToken);

        if (template is not null)
        {
            return Ok(new AiPromptTemplateDto(
                template.Key,
                template.Template,
                template.Version,
                template.IsActive,
                "database",
                template.UpdatedAt,
                template.UpdatedBy));
        }

        var fallbackTemplate = ResolveFallbackTemplate(normalizedKey);

        return Ok(new AiPromptTemplateDto(
            normalizedKey,
            fallbackTemplate.Template,
            fallbackTemplate.Version,
            true,
            "config-default",
            null,
            null));
    }

    [HttpPut("prompts/{key}")]
    public async Task<ActionResult<AiPromptTemplateDto>> UpsertPromptTemplate(
        [FromRoute] string key,
        [FromBody] UpdateAiPromptTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedKey = NormalizeKey(key);
        if (string.IsNullOrWhiteSpace(normalizedKey))
        {
            return BadRequest(new { message = "Prompt key is required." });
        }

        if (string.IsNullOrWhiteSpace(request.Template))
        {
            return BadRequest(new { message = "Template content is required." });
        }

        var entity = await dbContext.AiPromptTemplates
            .FirstOrDefaultAsync(x => x.Key == normalizedKey, cancellationToken);

        if (entity is null)
        {
            entity = new Domain.Entities.AiPromptTemplate
            {
                Key = normalizedKey
            };

            dbContext.AiPromptTemplates.Add(entity);
        }

        entity.Template = request.Template.Trim();
        entity.Version = string.IsNullOrWhiteSpace(request.Version)
            ? enrichmentOptions.Value.PromptVersion
            : request.Version.Trim();
        entity.IsActive = request.IsActive;
        entity.UpdatedBy = string.IsNullOrWhiteSpace(request.UpdatedBy) ? "ui-admin" : request.UpdatedBy.Trim();
        entity.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new AiPromptTemplateDto(
            entity.Key,
            entity.Template,
            entity.Version,
            entity.IsActive,
            "database",
            entity.UpdatedAt,
            entity.UpdatedBy));
    }

    [HttpGet("worker-status")]
    public ActionResult<AiWorkerStatusDto> GetWorkerStatus()
    {
        var intervalSeconds = Math.Clamp(configuration.GetValue<int?>("Jobs:MarketIntelligence:IntervalSeconds") ?? 60, 10, 600);
        var batchSize = Math.Clamp(configuration.GetValue<int?>("Jobs:MarketIntelligence:BatchSize") ?? 50, 5, 200);
        var snapshot = executionTracker.GetSnapshot();

        return Ok(new AiWorkerStatusDto(
            snapshot.IsRunning,
            snapshot.LastStartedAt,
            snapshot.LastCompletedAt,
            snapshot.LastProcessedJobs,
            snapshot.LastOutcome,
            snapshot.LastError,
            intervalSeconds,
            batchSize,
            snapshot.LastTrigger));
    }

    [HttpPost("worker/run-now")]
    public async Task<ActionResult<AiWorkerRunNowResultDto>> RunWorkerNow(CancellationToken cancellationToken)
    {
        var startedAt = DateTime.UtcNow;
        executionTracker.MarkStarted("manual");

        try
        {
            var configuredBatchSize = Math.Clamp(configuration.GetValue<int?>("Jobs:MarketIntelligence:BatchSize") ?? 50, 5, 200);

            using var scope = scopeFactory.CreateScope();
            var marketService = scope.ServiceProvider.GetRequiredService<IMarketIntelligenceService>();
            var processed = await marketService.ProcessPendingJobsAsync(cancellationToken, configuredBatchSize);

            executionTracker.MarkCompleted(processed);
            var completedAt = DateTime.UtcNow;

            return Ok(new AiWorkerRunNowResultDto(processed, startedAt, completedAt, "manual"));
        }
        catch (Exception ex)
        {
            executionTracker.MarkFailed(ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Manual worker run failed.",
                detail = ex.Message
            });
        }
    }

    private static string NormalizeKey(string? key)
    {
        return key?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    private (string Template, string Version) ResolveFallbackTemplate(string key)
    {
        return key switch
        {
            AiPromptTemplateKeys.MarketJobAnalysis => (
                enrichmentOptions.Value.PromptTemplate,
                enrichmentOptions.Value.PromptVersion),
            _ => (
                enrichmentOptions.Value.PromptTemplate,
                enrichmentOptions.Value.PromptVersion)
        };
    }

    private static string NormalizeSortBy(string? sortBy)
    {
        var normalized = sortBy?.Trim().ToLowerInvariant();

        return normalized switch
        {
            "createdat" => "createdAt",
            "provider" => "provider",
            "modelid" => "modelId",
            "clusterid" => "clusterId",
            "status" => "status",
            "totaltokens" => "totalTokens",
            "latencyms" => "latencyMs",
            _ => "createdAt"
        };
    }

    private static string NormalizeSortDirection(string? sortDirection)
    {
        var normalized = sortDirection?.Trim().ToLowerInvariant();
        return normalized == "asc" ? "asc" : "desc";
    }

    private static IQueryable<Domain.Entities.AiPromptLog> ApplySorting(
        IQueryable<Domain.Entities.AiPromptLog> query,
        string sortBy,
        string sortDirection)
    {
        var asc = sortDirection == "asc";

        return sortBy switch
        {
            "provider" => asc
                ? query.OrderBy(x => x.Provider).ThenByDescending(x => x.CreatedAt)
                : query.OrderByDescending(x => x.Provider).ThenByDescending(x => x.CreatedAt),
            "modelId" => asc
                ? query.OrderBy(x => x.ModelId).ThenByDescending(x => x.CreatedAt)
                : query.OrderByDescending(x => x.ModelId).ThenByDescending(x => x.CreatedAt),
            "clusterId" => asc
                ? query.OrderBy(x => x.ClusterId).ThenByDescending(x => x.CreatedAt)
                : query.OrderByDescending(x => x.ClusterId).ThenByDescending(x => x.CreatedAt),
            "status" => asc
                ? query.OrderBy(x => x.Status).ThenByDescending(x => x.CreatedAt)
                : query.OrderByDescending(x => x.Status).ThenByDescending(x => x.CreatedAt),
            "totalTokens" => asc
                ? query.OrderBy(x => x.TotalTokens).ThenByDescending(x => x.CreatedAt)
                : query.OrderByDescending(x => x.TotalTokens).ThenByDescending(x => x.CreatedAt),
            "latencyMs" => asc
                ? query.OrderBy(x => x.LatencyMs).ThenByDescending(x => x.CreatedAt)
                : query.OrderByDescending(x => x.LatencyMs).ThenByDescending(x => x.CreatedAt),
            _ => asc
                ? query.OrderBy(x => x.CreatedAt)
                : query.OrderByDescending(x => x.CreatedAt)
        };
    }
}
