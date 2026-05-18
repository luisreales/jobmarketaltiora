namespace backend.Domain.Entities;

public class ProductScrapeState
{
    public int ProductId { get; set; }
    public AppSumoProduct Product { get; set; } = null!;

    public int LastRunId { get; set; }
    public AppSumoScrapeRun LastRun { get; set; } = null!;

    /// <summary>Pending | Done | Failed | Skipped</summary>
    public string Status { get; set; } = "Pending";

    public byte AttemptCount { get; set; }
    public string? LastError { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
