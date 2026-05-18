namespace backend.Domain.Entities;

public class AppSumoReview
{
    public long Id { get; set; }
    public int ProductId { get; set; }
    public AppSumoProduct Product { get; set; } = null!;

    public string? AppSumoReviewId { get; set; }
    public byte TacoRating { get; set; }  // 1, 2, or 3 only
    public string? ReviewerName { get; set; }
    public DateOnly? ReviewDate { get; set; }
    public string ReviewText { get; set; } = string.Empty;
    public int? FoundHelpful { get; set; }
    public bool IsVerified { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
