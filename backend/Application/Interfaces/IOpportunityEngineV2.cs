namespace backend.Application.Interfaces;

/// <summary>
/// Enriches actionable MarketClusters with commercial intelligence signals:
/// TAM, BuyingIntent, HiringVelocity, SalesFriction, RevenuePotential,
/// SalesAngle, WhyNow, EstimatedCloseProbability, and PriorityScoreV2.
/// Runs after DecisionEngine in the clustering pipeline.
/// </summary>
public interface IOpportunityEngineV2
{
    Task<int> EnrichClustersAsync(CancellationToken ct = default);
}
