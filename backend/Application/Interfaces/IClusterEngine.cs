namespace backend.Application.Interfaces;

public interface IClusterEngine
{
    /// <summary>
    /// Reads all processed JobInsights, groups them into MarketClusters
    /// by (PainCategory + TechTop3 + Industry + CompanyType), computes market signals
    /// and BlueOceanScore v2, and upserts the results in the MarketClusters table.
    /// Also updates ClusterId on all associated JobInsights.
    /// </summary>
    /// <returns>Number of clusters created or updated.</returns>
    Task<int> RebuildClustersAsync(CancellationToken cancellationToken = default);
}
