using backend.Application.Interfaces;
using backend.Domain.Entities;
using backend.Infrastructure.Data;
using backend.Infrastructure.Services;
using backend.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Xunit;

namespace backend.Tests;

public class HybridAiEnrichmentServiceTests
{
    [Fact]
    public async Task AnalyzeJobAsync_WhenSemanticKernelDisabled_ShouldReturnRuleBasedResult()
    {
        var service = CreateService(
            new MarketIntelligenceEnrichmentOptions { UseSemanticKernel = false, ShadowMode = true },
            new FakeSemanticKernelProvider(isConfigured: true));

        var result = await service.AnalyzeJobAsync(BuildJob());

        Assert.Equal("Rules", result.DecisionSource);
        Assert.Null(result.RawModelResponse);
    }

    [Fact]
    public async Task AnalyzeJobAsync_WhenSemanticKernelEnabledAndConfiguredButNoChatService_ShouldFallbackToRules()
    {
        var service = CreateService(
            new MarketIntelligenceEnrichmentOptions
            {
                UseSemanticKernel = true,
                ShadowMode = true,
                MinDescriptionLength = 10,
                SamplingRatePercent = 100,
                MaxCallsPerDay = 1000
            },
            new FakeSemanticKernelProvider(isConfigured: true));

        var result = await service.AnalyzeJobAsync(BuildJob());

        Assert.Equal("Rules", result.DecisionSource);
        Assert.Contains("SemanticKernelShadow=FallbackToRules", result.RawModelResponse);
    }

    [Fact]
    public async Task AnalyzeJobAsync_WhenSemanticKernelEnabledButNotConfigured_ShouldFallbackToRules()
    {
        var service = CreateService(
            new MarketIntelligenceEnrichmentOptions
            {
                UseSemanticKernel = true,
                ShadowMode = true,
                MinDescriptionLength = 10,
                SamplingRatePercent = 100,
                MaxCallsPerDay = 1000
            },
            new FakeSemanticKernelProvider(isConfigured: false));

        var result = await service.AnalyzeJobAsync(BuildJob());

        Assert.Equal("Rules", result.DecisionSource);
    }

    private static HybridAiEnrichmentService CreateService(
        MarketIntelligenceEnrichmentOptions options,
        ISemanticKernelProvider provider)
    {
        var scorer = new OpportunityScorerService();
        var preprocessor = new JobPreprocessorService(new TechCanonicalizer(), new IndustryClassifier());
        var rules = new RuleBasedAiEnrichmentService(scorer, preprocessor);
        ApplicationDbContext dbContext = TestApplicationDbContextFactory.Create();

        return new HybridAiEnrichmentService(
            rules,
            provider,
            Options.Create(options),
            Options.Create(new SemanticKernelOptions
            {
                Provider = "OpenAI",
                ModelId = "gpt-4o-mini"
            }),
            dbContext,
            NullLogger<HybridAiEnrichmentService>.Instance);
    }

    private static JobOffer BuildJob()
    {
        return new JobOffer
        {
            Id = 7,
            ExternalId = "job-77",
            Source = "linkedin",
            SearchTerm = ".net",
            Title = "Cloud Platform Engineer",
            Company = "Acme",
            Location = "Remote",
            Description = "Urgent migration and cloud scaling with microservices and enterprise architecture for a large platform",
            Url = "https://example.com/jobs/77",
            CapturedAt = DateTime.UtcNow,
            IsConsultingCompany = false
        };
    }

    private sealed class FakeSemanticKernelProvider(bool isConfigured) : ISemanticKernelProvider
    {
        public bool IsConfigured { get; } = isConfigured;

        public Kernel GetKernel()
        {
            return Kernel.CreateBuilder().Build();
        }
    }
}
