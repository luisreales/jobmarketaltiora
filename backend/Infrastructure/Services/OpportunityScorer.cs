using backend.Application.Interfaces;
using backend.Domain.Entities;

namespace backend.Infrastructure.Services;

public sealed class OpportunityScorer : IOpportunityScorer
{
    public int Score(JobOffer job)
    {
        var score = 0;
        var text = $"{job.Title} {job.Description}".ToLowerInvariant();

        if (job.CompanyType.Equals("DirectClient", StringComparison.OrdinalIgnoreCase))
        {
            score += 30;
        }

        if (job.IsConsultingCompany)
        {
            score -= 20;
        }

        if (ContainsAny(text, ".net", "c#", "asp.net"))
        {
            score += 20;
        }

        if (ContainsAny(text, "microservices", "scalability", "high traffic"))
        {
            score += 15;
        }

        if (ContainsAny(text, "azure", "aws", "cloud"))
        {
            score += 10;
        }

        if (ContainsAny(text, "api", "distributed systems"))
        {
            score += 10;
        }

        return Math.Clamp(score, 0, 100);
    }

    private static bool ContainsAny(string text, params string[] tokens)
    {
        return tokens.Any(token => text.Contains(token, StringComparison.Ordinal));
    }
}