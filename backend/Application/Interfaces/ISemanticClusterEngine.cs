namespace backend.Application.Interfaces;

/// <summary>
/// Semantic Cluster Engine — additive layer on top of the existing SHA256 clustering.
///
/// Step 1: GenerateEmbeddingsAsync — fills EmbeddingVectorJson on JobInsight records without it.
/// Step 2: AssignSemanticGroupsAsync — compares cluster centroids via cosine similarity
///         and sets SemanticGroupKey on MarketClusters that represent the same underlying problem.
///
/// Never breaks existing clusters. Falls back gracefully when SK is not configured.
/// </summary>
public interface ISemanticClusterEngine
{
    /// <summary>
    /// Generates embeddings for JobInsight records that don't have one yet.
    /// Returns the number of insights embedded.
    /// </summary>
    Task<int> GenerateEmbeddingsAsync(CancellationToken ct = default);

    /// <summary>
    /// Compares cluster centroids and assigns the same SemanticGroupKey to clusters
    /// whose centroid similarity exceeds <paramref name="similarityThreshold"/>.
    /// Returns the number of clusters that received a semantic group assignment.
    /// </summary>
    Task<int> AssignSemanticGroupsAsync(double similarityThreshold = 0.82, CancellationToken ct = default);
}
