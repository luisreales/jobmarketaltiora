using backend.Domain.Entities;
using backend.Infrastructure.Services;
using Xunit;

namespace backend.Tests;

public class RuleBasedAiEnrichmentServiceTests
{
    [Fact]
    public async Task AnalyzeJobAsync_ShouldDetectMigrationPainPoint()
    {
        var scorer = new OpportunityScorerService();
        var service = CreateService(scorer);
        var job = BuildJob("Senior Backend Engineer", "Need migration from monolith to modern services");

        var result = await service.AnalyzeJobAsync(job);

        Assert.Equal("Legacy Modernization", result.MainPainPoint);
        Assert.Equal("Migration", result.PainCategory);
        Assert.Equal("Rules", result.DecisionSource);
    }

    [Fact]
    public async Task AnalyzeJobAsync_ShouldDetectCloudModernizationAndUrgency()
    {
        var scorer = new OpportunityScorerService();
        var service = CreateService(scorer);
        var job = BuildJob(
            "Cloud Platform Engineer",
            "Urgent production issue in AWS Kubernetes platform, need scalable architecture immediately");

        var result = await service.AnalyzeJobAsync(job);

        Assert.Equal("Cloud Modernization", result.MainPainPoint);
        Assert.Equal("CloudModernization", result.PainCategory);
        Assert.True(result.UrgencyScore >= 6);
        Assert.InRange(result.OpportunityScore, 0, 100);
    }

    private static JobOffer BuildJob(string title, string description)
    {
        return new JobOffer
        {
            Id = 1,
            ExternalId = "ext-1",
            Source = "linkedin",
            SearchTerm = ".net",
            Title = title,
            Company = "Acme Corp",
            Location = "Remote",
            Description = description,
            Url = "https://example.com/job/1",
            CapturedAt = DateTime.UtcNow,
            IsConsultingCompany = false
        };
    }

    private static RuleBasedAiEnrichmentService CreateService(OpportunityScorerService scorer)
    {
        var preprocessor = new JobPreprocessorService(new TechCanonicalizer(), new IndustryClassifier());
        return new RuleBasedAiEnrichmentService(scorer, preprocessor);
    }
}
