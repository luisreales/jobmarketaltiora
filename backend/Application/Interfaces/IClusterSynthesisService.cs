using backend.Application.Contracts;

namespace backend.Application.Interfaces;

public interface IClusterSynthesisService
{
    /// <summary>
    /// Batch: picks up to 5 actionable clusters with LlmStatus="pending" and synthesizes them.
    /// Each cluster is processed independently — failure on one does not abort the rest.
    /// </summary>
    Task SynthesizePendingClustersAsync(CancellationToken ct = default);

    /// <summary>
    /// On-demand: synthesizes a single cluster by ID.
    /// If the cluster already has LlmStatus="completed", returns the cached result immediately.
    /// Returns null if the cluster does not exist.
    /// </summary>
    Task<MarketClusterDto?> SynthesizeClusterAsync(int clusterId, CancellationToken ct = default);
}
