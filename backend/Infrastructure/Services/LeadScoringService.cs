using backend.Domain.Entities;

namespace backend.Infrastructure.Services;

/// <summary>
/// Computes a composite LeadScore (0–100) per JobInsight.
/// Formula:
///   LeadScore = OpportunityScore * 0.40
///             + UrgencyScore * 5 * 0.20      (UrgencyScore is 1–10, scaled to 0–50 then weighted)
///             + DirectClientBonus             (20 if IsDirectClient, else 0)
///             + RecencyBoost * 0.20           (0–100 based on age of the job posting)
///
/// This score is used to rank leads within a cluster (not to rank clusters themselves —
/// BlueOceanScore handles that in Fase 2).
/// </summary>
public sealed class LeadScoringService
{
    // Recency buckets (days since CapturedAt → max recency points)
    private const int MaxRecencyScore = 100;
    private const int FreshDays = 7;      // 0–7 days: 100 points
    private const int RecentDays = 30;    // 8–30 days: linear decay 100→40
    private const int StaleDays = 90;     // 31–90 days: linear decay 40→5
    // Older than StaleDays: 0 recency points

    public int Calculate(JobInsight insight, DateTime? capturedAt = null)
    {
        var opportunityComponent = insight.OpportunityScore * 0.40;
        var urgencyComponent = Math.Clamp(insight.UrgencyScore, 1, 10) * 5.0 * 0.20;
        var directClientBonus = insight.IsDirectClient ? 20.0 : 0.0;
        var recencyBoost = ComputeRecencyBoost(capturedAt) * 0.20;

        var raw = opportunityComponent + urgencyComponent + directClientBonus + recencyBoost;
        return (int)Math.Clamp(Math.Round(raw), 0, 100);
    }

    private static double ComputeRecencyBoost(DateTime? capturedAt)
    {
        if (capturedAt is null)
        {
            return 0;
        }

        var ageDays = (DateTime.UtcNow - capturedAt.Value).TotalDays;

        if (ageDays <= FreshDays)
        {
            return MaxRecencyScore;
        }

        if (ageDays <= RecentDays)
        {
            // Linear decay: 100 → 40 over [FreshDays, RecentDays]
            var t = (ageDays - FreshDays) / (RecentDays - FreshDays);
            return 100 - t * (100 - 40);
        }

        if (ageDays <= StaleDays)
        {
            // Linear decay: 40 → 5 over [RecentDays, StaleDays]
            var t = (ageDays - RecentDays) / (StaleDays - RecentDays);
            return 40 - t * (40 - 5);
        }

        return 0;
    }
}
