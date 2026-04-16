namespace backend.Application.Interfaces;

public interface IDecisionEngine
{
    /// <summary>
    /// Reads all MarketClusters with a computed BlueOceanScore and evaluates each one:
    /// - Classifies OpportunityType (MVPProduct | QuickWin | Consulting | Ignore)
    /// - Sets IsActionable flag
    /// - Assigns RecommendedStrategy
    /// - Calculates PriorityScore (0–100)
    /// </summary>
    /// <returns>Number of clusters evaluated.</returns>
    Task<int> EvaluateClustersAsync(CancellationToken cancellationToken = default);
}
