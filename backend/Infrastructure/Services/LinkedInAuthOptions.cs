namespace backend.Infrastructure.Services;

public sealed class LinkedInAuthOptions
{
    public bool LoginHeadless { get; set; } = false;
    public bool ReuseHeadless { get; set; } = true;
    public int NavigationTimeoutMs { get; set; } = 30000;
    public int ManualChallengeTimeoutSeconds { get; set; } = 300;
    public int SlowMoMs { get; set; } = 50;
    public int MaxRetryAttempts { get; set; } = 2;
    public int RetryDelayMs { get; set; } = 1500;
    public string SessionFilePath { get; set; } = "/app/data/linkedin-state.json";
    public string UserAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36";
}