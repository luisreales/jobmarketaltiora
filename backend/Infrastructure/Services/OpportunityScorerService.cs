using backend.Application.Interfaces;

namespace backend.Infrastructure.Services;

public sealed class OpportunityScorerService : IOpportunityScorerService
{
    public int CalculateOpportunityScore(string normalizedText, int urgencyScore, bool isConsultingCompany)
    {
        var complexitySignals = new[]
        {
            "microservices", "distributed", "architecture", "scalable", "enterprise", "compliance"
        };

        var complexity = complexitySignals.Count(signal => normalizedText.Contains(signal, StringComparison.Ordinal));

        var baseScore = 45 + (complexity * 7) + (urgencyScore * 3);
        if (isConsultingCompany)
        {
            baseScore -= 12;
        }

        return Math.Clamp(baseScore, 0, 100);
    }
}
