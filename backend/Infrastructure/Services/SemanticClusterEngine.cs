#pragma warning disable SKEXP0001 // ITextEmbeddingGenerationService is experimental in SK 1.x

using System.Security.Cryptography;
using System.Text;
using backend.Application.Interfaces;
using backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel.Embeddings;

namespace backend.Infrastructure.Services;

/// <summary>
/// Semantic Cluster Engine — additive enrichment layer.
///
/// Step 1 (GenerateEmbeddingsAsync):
///   Embeds MainPainPoint + PainCategory + SuggestedSolution for JobInsight records
///   that don't yet have an EmbeddingVectorJson. Processed in batches of 20.
///
/// Step 2 (AssignSemanticGroupsAsync):
///   Computes per-cluster centroid from member insight embeddings.
///   Clusters whose centroids exceed the similarity threshold share the same SemanticGroupKey,
///   surfacing them as related without merging or breaking existing SHA256 keys.
///
/// Both steps degrade gracefully when the embedding model is not configured.
/// </summary>
public sealed class SemanticClusterEngine(
    ApplicationDbContext dbContext,
    ISemanticKernelProvider kernelProvider,
    ILogger<SemanticClusterEngine> logger) : ISemanticClusterEngine
{
    private const int EmbeddingBatchSize = 20;

    // ── Step 1: Generate embeddings ──────────────────────────────────────────────

    public async Task<int> GenerateEmbeddingsAsync(CancellationToken ct = default)
    {
        if (!kernelProvider.IsEmbeddingConfigured)
        {
            logger.LogDebug("SemanticClusterEngine: embedding model not configured — skipping.");
            return 0;
        }

        var embeddingService = TryGetEmbeddingService();
        if (embeddingService is null)
        {
            logger.LogWarning("SemanticClusterEngine: ITextEmbeddingGenerationService not available in kernel.");
            return 0;
        }

        // Load only processed insights without embeddings (cap at 200 per cycle)
        var insights = await dbContext.JobInsights
            .Where(i => i.IsProcessed && i.EmbeddingVectorJson == null)
            .OrderByDescending(i => i.ProcessedAt)
            .Take(200)
            .ToListAsync(ct);

        if (insights.Count == 0)
        {
            logger.LogDebug("SemanticClusterEngine: all insights already have embeddings.");
            return 0;
        }

        logger.LogInformation("SemanticClusterEngine: generating embeddings for {Count} insights.", insights.Count);

        var embedded = 0;

        for (var offset = 0; offset < insights.Count; offset += EmbeddingBatchSize)
        {
            ct.ThrowIfCancellationRequested();

            var batch = insights.Skip(offset).Take(EmbeddingBatchSize).ToList();
            var texts = batch
                .Select(i => BuildEmbeddingText(i.MainPainPoint, i.PainCategory, i.SuggestedSolution))
                .ToList();

            try
            {
                var embeddings = await embeddingService.GenerateEmbeddingsAsync(texts, cancellationToken: ct);

                for (var j = 0; j < batch.Count; j++)
                {
                    var vector = embeddings[j].ToArray();
                    batch[j].EmbeddingVectorJson = ClusterSimilarityService.Serialize(vector);
                    batch[j].EmbeddedAt          = DateTime.UtcNow;
                    embedded++;
                }

                await dbContext.SaveChangesAsync(ct);

                logger.LogDebug(
                    "SemanticClusterEngine: embedded batch {Offset}–{End}.",
                    offset, offset + batch.Count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "SemanticClusterEngine: embedding batch {Offset}–{End} failed — skipping.",
                    offset, offset + batch.Count);
            }
        }

        logger.LogInformation("SemanticClusterEngine: {Embedded} insights embedded.", embedded);
        return embedded;
    }

    // ── Step 2: Assign semantic group keys ───────────────────────────────────────

    public async Task<int> AssignSemanticGroupsAsync(
        double similarityThreshold = 0.82,
        CancellationToken ct = default)
    {
        if (!kernelProvider.IsEmbeddingConfigured)
        {
            logger.LogDebug("SemanticClusterEngine: embedding model not configured — skipping group assignment.");
            return 0;
        }

        // Load clusters with at least one embedded insight
        var clusters = await dbContext.MarketClusters
            .Where(c => c.IsActionable)
            .ToListAsync(ct);

        if (clusters.Count < 2)
        {
            logger.LogDebug("SemanticClusterEngine: fewer than 2 actionable clusters — no groups to assign.");
            return 0;
        }

        // Load centroids: for each cluster, average its insights' embeddings
        var centroids = await BuildCentroidsAsync(clusters.Select(c => c.Id).ToList(), ct);

        if (centroids.Count < 2)
        {
            logger.LogDebug("SemanticClusterEngine: not enough clusters with embeddings for group assignment.");
            return 0;
        }

        // Union-Find grouping by cosine similarity
        var clusterIds = centroids.Keys.ToList();
        var parent = clusterIds.ToDictionary(id => id, id => id);

        int Find(int x) => parent[x] == x ? x : parent[x] = Find(parent[x]);
        void Union(int a, int b) { parent[Find(a)] = Find(b); }

        for (var i = 0; i < clusterIds.Count; i++)
        {
            for (var j = i + 1; j < clusterIds.Count; j++)
            {
                var idA = clusterIds[i];
                var idB = clusterIds[j];

                var sim = ClusterSimilarityService.CosineSimilarity(centroids[idA], centroids[idB]);
                if (sim >= similarityThreshold)
                    Union(idA, idB);
            }
        }

        // Assign deterministic SemanticGroupKey per Union-Find root
        var rootToKey = new Dictionary<int, string>();
        string KeyForRoot(int root)
        {
            if (!rootToKey.TryGetValue(root, out var key))
            {
                var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"semantic-group-{root}"));
                key = Convert.ToHexString(bytes).ToLowerInvariant()[..12];
                rootToKey[root] = key;
            }
            return key;
        }

        var assigned = 0;
        foreach (var cluster in clusters)
        {
            if (!centroids.ContainsKey(cluster.Id)) continue;

            var root = Find(cluster.Id);

            // Only assign a group key when the cluster actually has siblings
            var hasSiblings = clusterIds.Any(other => other != cluster.Id && Find(other) == root);
            var newKey = hasSiblings ? KeyForRoot(root) : null;

            if (cluster.SemanticGroupKey != newKey)
            {
                cluster.SemanticGroupKey = newKey;
                assigned++;
            }
        }

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "SemanticClusterEngine: {Assigned} clusters updated with semantic group keys. Groups={Groups}",
            assigned, rootToKey.Count);

        return assigned;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private async Task<Dictionary<int, float[]>> BuildCentroidsAsync(
        List<int> clusterIds, CancellationToken ct)
    {
        var insightVectors = await dbContext.JobInsights
            .Where(i => i.ClusterId != null
                        && clusterIds.Contains(i.ClusterId!.Value)
                        && i.EmbeddingVectorJson != null)
            .Select(i => new { i.ClusterId, i.EmbeddingVectorJson })
            .ToListAsync(ct);

        var grouped = insightVectors
            .GroupBy(x => x.ClusterId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => ClusterSimilarityService.Deserialize(x.EmbeddingVectorJson))
                       .OfType<float[]>()
                       .ToList());

        var centroids = new Dictionary<int, float[]>();
        foreach (var (clusterId, vectors) in grouped)
        {
            var centroid = ClusterSimilarityService.ComputeCentroid(vectors);
            if (centroid is not null)
                centroids[clusterId] = centroid;
        }

        return centroids;
    }

    private static string BuildEmbeddingText(string painPoint, string painCategory, string solution)
    {
        var parts = new[] { painPoint, painCategory, solution }
            .Where(s => !string.IsNullOrWhiteSpace(s));
        return string.Join(". ", parts);
    }

    private ITextEmbeddingGenerationService? TryGetEmbeddingService()
    {
        try
        {
            var kernel = kernelProvider.GetKernel();
            return kernel.Services.GetService<ITextEmbeddingGenerationService>();
        }
        catch
        {
            return null;
        }
    }
}
