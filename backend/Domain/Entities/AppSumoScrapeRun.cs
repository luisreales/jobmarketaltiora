namespace backend.Domain.Entities;

public class AppSumoScrapeRun
{
    public int Id { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }

    /// <summary>Running | Completed | Failed | Cancelled</summary>
    public string Status { get; set; } = "Running";

    public int ProductsScraped { get; set; }
    public int ReviewsSaved { get; set; }
    public int ErrorCount { get; set; }
    public string? Notes { get; set; }
}
