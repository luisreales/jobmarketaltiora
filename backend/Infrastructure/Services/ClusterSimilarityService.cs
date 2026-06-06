using System.Text.Json;

namespace backend.Infrastructure.Services;

/// <summary>
/// Pure utility for cosine similarity computations between float[] embedding vectors.
/// All methods are static and allocation-minimal.
/// </summary>
public static class ClusterSimilarityService
{
    /// <summary>
    /// Cosine similarity between two vectors. Returns 0 if either vector is zero-magnitude.
    /// Result is clamped to [-1, 1].
    /// </summary>
    public static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0)
            return 0.0;

        double dot = 0, magA = 0, magB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot  += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        if (magA == 0 || magB == 0)
            return 0.0;

        return Math.Clamp(dot / (Math.Sqrt(magA) * Math.Sqrt(magB)), -1.0, 1.0);
    }

    /// <summary>
    /// Computes the centroid (mean vector) of a collection of embedding vectors.
    /// Returns null if the list is empty or all vectors have different lengths.
    /// </summary>
    public static float[]? ComputeCentroid(IReadOnlyList<float[]> vectors)
    {
        if (vectors.Count == 0) return null;

        var dim = vectors[0].Length;
        if (dim == 0) return null;

        var centroid = new float[dim];
        var count    = 0;

        foreach (var v in vectors)
        {
            if (v.Length != dim) continue;
            for (var i = 0; i < dim; i++)
                centroid[i] += v[i];
            count++;
        }

        if (count == 0) return null;

        for (var i = 0; i < dim; i++)
            centroid[i] /= count;

        return centroid;
    }

    /// <summary>Deserializes a JSON float array. Returns null on parse error.</summary>
    public static float[]? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<float[]>(json);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Serializes a float[] to compact JSON.</summary>
    public static string Serialize(float[] vector)
        => JsonSerializer.Serialize(vector);
}
