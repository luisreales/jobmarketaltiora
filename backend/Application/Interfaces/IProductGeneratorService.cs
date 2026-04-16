using backend.Domain.Entities;

namespace backend.Application.Interfaces;

public interface IProductGeneratorService
{
    /// <summary>
    /// Generates (or regenerates) ProductSuggestions for all actionable clusters.
    /// Deletes orphaned suggestions for clusters that are no longer actionable.
    /// Returns count of products upserted.
    /// </summary>
    Task<int> GenerateProductsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a single product suggestion for a specific cluster.
    /// Returns null if the cluster is not actionable or no catalog rule matches.
    /// </summary>
    Task<ProductSuggestion?> GenerateForClusterAsync(int clusterId, CancellationToken cancellationToken = default);
}
