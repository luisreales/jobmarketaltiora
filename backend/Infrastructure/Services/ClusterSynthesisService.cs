using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using backend.Application.Contracts;
using backend.Application.Interfaces;
using backend.Domain.Entities;
using backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace backend.Infrastructure.Services;

/// <summary>
/// Bloque D — LLM Synthesis.
/// Two modes:
///   - Batch (SynthesizePendingClustersAsync): worker picks up to 5 pending clusters.
///   - On-demand (SynthesizeClusterAsync): user triggers a single cluster from the UI.
/// </summary>
public sealed class ClusterSynthesisService(
    ApplicationDbContext dbContext,
    ISemanticKernelProvider kernelProvider,
    ILogger<ClusterSynthesisService> logger) : IClusterSynthesisService
{
    private const int MaxClustersPerRun = 5;
    private const int MaxJobDescriptionChars = 4_000;

    private const string SystemPromptTemplate =
        """
        Actúa como un Director de Estrategia B2B para una agencia de ingeniería de élite.
        Analiza este grupo de vacantes tecnológicas de empresas de la industria {industry} que usan {techTop3}.
        Tu objetivo es identificar el dolor de negocio común y empaquetar una solución (MVP/Auditoría) que podamos venderles hoy.
        Devuelve ÚNICAMENTE un objeto JSON válido con esta estructura exacta, sin texto adicional, sin markdown, sin bloques de código:
        {
          "pain": "Descripción clara de 2 líneas sobre el cuello de botella técnico y de negocio que sufren.",
          "mvp": "Nombre y descripción de 2 líneas de un paquete de servicios ágil (ej. Auditoría en 7 días, Migración de x a y) para resolverlo.",
          "leadMessage": "Un mensaje de cold email de 3 líneas, muy directo, ofreciendo este MVP al CTO."
        }
        """;

    // ── Batch mode ────────────────────────────────────────────────────────────

    public async Task SynthesizePendingClustersAsync(CancellationToken ct = default)
    {
        var kernel = kernelProvider.GetKernel();
        if (kernel is null)
        {
            logger.LogWarning("ClusterSynthesisService: Semantic Kernel not configured. Skipping batch synthesis.");
            return;
        }

        var pending = await dbContext.MarketClusters
            .Where(c => c.IsActionable && c.LlmStatus == "pending")
            .OrderByDescending(c => c.PriorityScore)
            .Take(MaxClustersPerRun)
            .ToListAsync(ct);

        if (pending.Count == 0)
        {
            logger.LogDebug("ClusterSynthesisService: no pending clusters.");
            return;
        }

        logger.LogInformation("ClusterSynthesisService: batch synthesizing {Count} cluster(s).", pending.Count);

        foreach (var cluster in pending)
        {
            await RunSynthesisAsync(kernel, cluster, ct);
        }
    }

    // ── On-demand mode ────────────────────────────────────────────────────────

    public async Task<MarketClusterDto?> SynthesizeClusterAsync(int clusterId, CancellationToken ct = default)
    {
        var cluster = await dbContext.MarketClusters
            .FirstOrDefaultAsync(c => c.Id == clusterId, ct);

        if (cluster is null) return null;

        // Cache hit: already synthesized, return immediately
        if (cluster.LlmStatus == "completed")
        {
            logger.LogInformation("ClusterSynthesisService: cluster {Id} already synthesized (cache hit).", clusterId);
            return ToDto(cluster);
        }

        var kernel = kernelProvider.GetKernel();
        if (kernel is null)
        {
            logger.LogWarning("ClusterSynthesisService: Semantic Kernel not configured. Cannot synthesize cluster {Id}.", clusterId);
            return ToDto(cluster);
        }

        await RunSynthesisAsync(kernel, cluster, ct);
        return ToDto(cluster);
    }

    // ── Core synthesis logic (shared) ─────────────────────────────────────────

    private async Task RunSynthesisAsync(Kernel kernel, MarketCluster cluster, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var promptText = await BuildPromptAsync(cluster, ct);
        var promptHash = ComputeHash(promptText);
        string responseText = string.Empty;

        try
        {
            var chat = kernel.GetRequiredService<IChatCompletionService>();
            var history = new ChatHistory();
            history.AddSystemMessage(BuildSystemPrompt(cluster));
            history.AddUserMessage(promptText);

            var result = await chat.GetChatMessageContentAsync(history, cancellationToken: ct);
            responseText = result.Content ?? string.Empty;
            sw.Stop();

            var parsed = ParseLlmResponse(responseText);

            cluster.SynthesizedPain        = parsed.Pain;
            cluster.SynthesizedMvp         = parsed.Mvp;
            cluster.SynthesizedLeadMessage  = parsed.LeadMessage;
            cluster.LlmStatus              = "completed";
            cluster.LastUpdatedAt          = DateTime.UtcNow;

            SavePromptLog(cluster.Id, promptText, promptHash, responseText,
                isSuccess: true, errorMessage: null, latencyMs: (int)sw.ElapsedMilliseconds);

            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation("ClusterSynthesisService: cluster {Id} synthesized in {Ms}ms.", cluster.Id, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            cluster.LlmStatus     = "failed";
            cluster.LastUpdatedAt = DateTime.UtcNow;

            logger.LogError(ex, "ClusterSynthesisService: cluster {Id} synthesis failed.", cluster.Id);

            try
            {
                SavePromptLog(cluster.Id, promptText, promptHash, responseText,
                    isSuccess: false, errorMessage: ex.Message, latencyMs: (int)sw.ElapsedMilliseconds);

                await dbContext.SaveChangesAsync(ct);
            }
            catch (Exception saveEx)
            {
                logger.LogError(saveEx, "ClusterSynthesisService: failed to persist error state for cluster {Id}.", cluster.Id);
            }
        }
    }

    // ── Prompt builders ───────────────────────────────────────────────────────

    private static string BuildSystemPrompt(MarketCluster cluster) =>
        SystemPromptTemplate
            .Replace("{industry}", cluster.Industry)
            .Replace("{techTop3}", cluster.TechKeyPart.Replace("|", ", "));

    private async Task<string> BuildPromptAsync(MarketCluster cluster, CancellationToken ct)
    {
        var jobDescriptions = await dbContext.JobInsights
            .AsNoTracking()
            .Where(i => i.ClusterId == cluster.Id)
            .OrderByDescending(i => i.LeadScore)
            .Take(5)
            .Select(i => i.Job!.Description)
            .ToListAsync(ct);

        if (jobDescriptions.Count == 0)
        {
            return $"Cluster: {cluster.Label}\nPainCategory: {cluster.PainCategory}\nJobCount: {cluster.JobCount}";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Cluster: {cluster.Label}");
        sb.AppendLine($"PainCategory: {cluster.PainCategory}");
        sb.AppendLine($"Industry: {cluster.Industry}");
        sb.AppendLine($"TechStack: {cluster.NormalizedTechStack}");
        sb.AppendLine($"JobCount: {cluster.JobCount}");
        sb.AppendLine();
        sb.AppendLine("--- SAMPLE JOB DESCRIPTIONS ---");

        foreach (var (desc, idx) in jobDescriptions.Select((d, i) => (d, i + 1)))
        {
            sb.AppendLine($"[JOB {idx}]");
            sb.AppendLine(CleanDescription(desc));
            sb.AppendLine();
        }

        var result = sb.ToString();
        return result.Length > MaxJobDescriptionChars ? result[..MaxJobDescriptionChars] : result;
    }

    // ── Text cleaning ─────────────────────────────────────────────────────────

    private static readonly string[] FluffMarkers =
    [
        "beneficios", "que ofrecemos", "qué ofrecemos", "acerca de nosotros",
        "about us", "somos una empresa", "horario", "ubicacion", "ubicación",
        "relocalizacion", "formacion", "formación", "equal opportunity"
    ];

    private static string CleanDescription(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var cleaned = new StringBuilder();
        foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var lower = line.ToLowerInvariant();
            if (FluffMarkers.Any(f => lower.Contains(f))) break;
            cleaned.AppendLine(line.Trim());
        }
        return cleaned.ToString().Trim();
    }

    // ── JSON parsing ──────────────────────────────────────────────────────────

    private record SynthesisResult(string Pain, string Mvp, string LeadMessage);

    private static SynthesisResult ParseLlmResponse(string raw)
    {
        var json = raw.Trim();
        if (json.StartsWith("```"))
        {
            var firstNewline = json.IndexOf('\n');
            var lastFence    = json.LastIndexOf("```");
            if (firstNewline > 0 && lastFence > firstNewline)
                json = json[(firstNewline + 1)..lastFence].Trim();
        }

        using var doc  = JsonDocument.Parse(json);
        var root       = doc.RootElement;
        var pain       = root.TryGetProperty("pain", out var p) ? p.GetString() ?? string.Empty : string.Empty;
        var mvp        = root.TryGetProperty("mvp", out var m) ? m.GetString() ?? string.Empty : string.Empty;
        var lead       = root.TryGetProperty("leadMessage", out var l) ? l.GetString() ?? string.Empty : string.Empty;

        if (string.IsNullOrWhiteSpace(pain) || string.IsNullOrWhiteSpace(mvp))
            throw new InvalidOperationException($"LLM response missing required fields. Raw: {raw[..Math.Min(200, raw.Length)]}");

        return new SynthesisResult(pain, mvp, lead);
    }

    // ── Audit log ─────────────────────────────────────────────────────────────

    private void SavePromptLog(int clusterId, string promptText, string promptHash,
        string responseText, bool isSuccess, string? errorMessage, int latencyMs)
    {
        dbContext.AiPromptLogs.Add(new AiPromptLog
        {
            ClusterId      = clusterId,
            Provider       = "SemanticKernel",
            ModelId        = "claude-4-6",
            PromptVersion  = "cluster-synthesis-v1",
            PromptHash     = promptHash,
            PromptText     = promptText,
            ResponseText   = responseText,
            IsSuccess      = isSuccess,
            Status         = isSuccess ? "success" : "failed",
            ErrorMessage   = errorMessage,
            LatencyMs      = latencyMs,
            CreatedAt      = DateTime.UtcNow
        });
    }

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..16];
    }

    // ── DTO projection ────────────────────────────────────────────────────────

    private static MarketClusterDto ToDto(MarketCluster c) => new(
        c.Id, c.Label, c.PainCategory, c.Industry, c.CompanyType, c.NormalizedTechStack,
        c.JobCount, c.DirectClientCount, c.DirectClientRatio,
        c.AvgOpportunityScore, c.AvgUrgencyScore, c.GrowthRate,
        c.BlueOceanScore, c.RoiRank,
        c.OpportunityType, c.IsActionable, c.RecommendedStrategy, c.PriorityScore,
        c.SynthesizedPain, c.SynthesizedMvp, c.SynthesizedLeadMessage,
        c.MvpType, c.EstimatedBuildDays, c.EstimatedDealSizeUsd,
        c.LlmStatus, c.LastUpdatedAt);
}
