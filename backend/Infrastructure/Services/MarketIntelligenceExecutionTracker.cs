namespace backend.Infrastructure.Services;

public sealed class MarketIntelligenceExecutionTracker
{
    private readonly object gate = new();

    private bool isRunning;
    private DateTime? lastStartedAt;
    private DateTime? lastCompletedAt;
    private int lastProcessedJobs;
    private string lastOutcome = "never";
    private string? lastError;
    private string lastTrigger = "none";

    public void MarkStarted(string trigger)
    {
        lock (gate)
        {
            isRunning = true;
            lastStartedAt = DateTime.UtcNow;
            lastTrigger = string.IsNullOrWhiteSpace(trigger) ? "unknown" : trigger;
            lastError = null;
        }
    }

    public void MarkCompleted(int processedJobs)
    {
        lock (gate)
        {
            isRunning = false;
            lastCompletedAt = DateTime.UtcNow;
            lastProcessedJobs = Math.Max(0, processedJobs);
            lastOutcome = "success";
            lastError = null;
        }
    }

    public void MarkFailed(string? error)
    {
        lock (gate)
        {
            isRunning = false;
            lastCompletedAt = DateTime.UtcNow;
            lastProcessedJobs = 0;
            lastOutcome = "failed";
            lastError = string.IsNullOrWhiteSpace(error) ? "Unknown error" : error;
        }
    }

    public MarketIntelligenceExecutionSnapshot GetSnapshot()
    {
        lock (gate)
        {
            return new MarketIntelligenceExecutionSnapshot(
                isRunning,
                lastStartedAt,
                lastCompletedAt,
                lastProcessedJobs,
                lastOutcome,
                lastError,
                lastTrigger);
        }
    }
}

public sealed record MarketIntelligenceExecutionSnapshot(
    bool IsRunning,
    DateTime? LastStartedAt,
    DateTime? LastCompletedAt,
    int LastProcessedJobs,
    string LastOutcome,
    string? LastError,
    string LastTrigger);