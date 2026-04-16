using backend.Application.Contracts;

namespace backend.Application.Interfaces;

public interface IMarketIntelligenceService
{
    Task<int> ProcessPendingJobsAsync(CancellationToken cancellationToken = default, int? batchSize = null);

    /// <summary>
    /// Re-enriches JobInsights that are missing Fase 0 fields (Industry, NormalizedTechStack, LeadScore).
    /// Called automatically by Worker 1 each cycle after processing new jobs.
    /// </summary>
    Task<int> ReenrichStaleInsightsAsync(int? batchSize = null, CancellationToken cancellationToken = default);
    Task<PagedResultDto<MarketOpportunityDto>> GetOpportunitiesAsync(MarketOpportunityQuery query, CancellationToken cancellationToken = default);
    Task<PagedResultDto<MarketLeadDto>> GetLeadsAsync(MarketLeadsQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<MarketTrendDto>> GetTrendsAsync(MarketTrendsQuery query, CancellationToken cancellationToken = default);
}
