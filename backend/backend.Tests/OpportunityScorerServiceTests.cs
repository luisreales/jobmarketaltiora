using backend.Infrastructure.Services;
using Xunit;

namespace backend.Tests;

public class OpportunityScorerServiceTests
{
    [Fact]
    public void CalculateOpportunityScore_ShouldIncreaseWithUrgencyAndComplexity()
    {
        var service = new OpportunityScorerService();

        var low = service.CalculateOpportunityScore("small fix", urgencyScore: 2, isConsultingCompany: false);
        var high = service.CalculateOpportunityScore("microservices scalable enterprise architecture", urgencyScore: 8, isConsultingCompany: false);

        Assert.True(high > low);
    }

    [Fact]
    public void CalculateOpportunityScore_ShouldPenalizeConsultingCompany()
    {
        var service = new OpportunityScorerService();

        var directClient = service.CalculateOpportunityScore("microservices scalable enterprise", urgencyScore: 6, isConsultingCompany: false);
        var consulting = service.CalculateOpportunityScore("microservices scalable enterprise", urgencyScore: 6, isConsultingCompany: true);

        Assert.True(directClient > consulting);
    }
}
