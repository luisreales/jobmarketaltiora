namespace backend.Application.Contracts;

public class AiPromptLogsQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string SortBy { get; set; } = "createdAt";
    public string SortDirection { get; set; } = "desc";
    public string? Provider { get; set; }
    public string? ModelId { get; set; }
    public string? Status { get; set; }
    public int? ClusterId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public record AiPromptLogDto(
    long Id,
    int? JobId,
    int? ClusterId,
    string Provider,
    string ModelId,
    string PromptVersion,
    string PromptHash,
    string PromptText,
    string ResponseText,
    bool CacheHit,
    bool IsSuccess,
    string Status,
    string? ErrorMessage,
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    int LatencyMs,
    DateTime CreatedAt);

public record AiUsageSummaryDto(
    DateTime FromDate,
    DateTime ToDate,
    int TotalCalls,
    int SuccessCalls,
    int FailedCalls,
    int CacheHits,
    int TotalTokens,
    double AverageLatencyMs);

public record AiPromptTemplateDto(
    string Key,
    string Template,
    string Version,
    bool IsActive,
    string Source,
    DateTime? UpdatedAt,
    string? UpdatedBy);

public class UpdateAiPromptTemplateRequest
{
    public string Template { get; set; } = string.Empty;
    public string? Version { get; set; }
    public bool IsActive { get; set; } = true;
    public string? UpdatedBy { get; set; }
}

public class CreateAiPromptTemplateRequest
{
    public string Key { get; set; } = string.Empty;
    public string Template { get; set; } = string.Empty;
    public string? Version { get; set; }
    public bool IsActive { get; set; } = true;
    public string? UpdatedBy { get; set; }
}

public record AiWorkerStatusDto(
    bool IsRunning,
    DateTime? LastStartedAt,
    DateTime? LastCompletedAt,
    int LastProcessedJobs,
    string LastOutcome,
    string? LastError,
    int IntervalSeconds,
    int BatchSize,
    string LastTrigger);

public record AiWorkerRunNowResultDto(
    int ProcessedJobs,
    DateTime StartedAt,
    DateTime CompletedAt,
    string Trigger);

public record LlmHealthDto(
    bool IsConfigured,
    bool IsHealthy,
    string State,
    string ModelId,
    DateTime? LastSuccessAt,
    DateTime? LastFailureAt,
    string Message);
