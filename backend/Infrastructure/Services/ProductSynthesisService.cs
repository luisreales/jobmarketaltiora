using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using backend.Application.Contracts;
using backend.Application.Interfaces;
using backend.Domain.Entities;
using backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Diagnostics;

namespace backend.Infrastructure.Services;

/// <summary>
/// TAREA 2 — On-demand tactical LLM synthesis for a ProductSuggestion.
/// Calls SK once per product and caches result in SynthesisDetailJson.
/// JSON schema: { implementacion, requerimientos, tiempo_y_tecnologias, empresas_objetivo }
/// </summary>
public sealed class ProductSynthesisService(
    ApplicationDbContext dbContext,
    ISemanticKernelProvider kernelProvider,
    IOptions<MarketIntelligenceEnrichmentOptions> enrichmentOptions,
    IOptions<SemanticKernelOptions> semanticKernelOptions,
    ILogger<ProductSynthesisService> logger) : IProductSynthesisService
{
    private const int MaxDescriptionChars = 5_500;
    private const int MaxTemplateJobs = 5;
    private const int MaxDescriptionSnippetChars = 260;
    private const string DefaultPromptVersion = "product-synthesis-v2";
    private const string FallbackModelId = "bedrock/anthropic.claude-4-5-haiku";
    private const string StrictJsonRetryInstruction =
        "Devuelve SOLO un objeto JSON valido en una sola linea, sin markdown y sin texto extra. " +
        "Usa exactamente estas claves: implementacion, requerimientos, tiempo_y_tecnologias, empresas_objetivo. " +
        "Mantén cada valor breve (maximo 350 caracteres).";

    private static readonly string[] HighSignalKeywords =
    {
        ".net", "api", "integration", "microservice", "sql", "postgres", "kafka", "redis",
        "azure", "aws", "docker", "kubernetes", "observability", "opentelemetry", "ci/cd",
        "test", "testing", "automation", "architecture", "scalable", "performance", "security"
    };

    private static readonly string[] BoilerplateMarkers =
    {
        "about the job", "we've been leading", "top 1% of tech talent", "career development",
        "setting you on a path", "our diverse", "when you apply", "silicon valley"
    };

    private const string SystemPrompt =
        """
        Actúa como un Director de Estrategia B2B para una agencia de ingeniería de élite.
        Recibirás la descripción de un producto/servicio tecnológico y las empresas objetivo que lo necesitan.
        Tu objetivo es generar un plan de ataque táctico y accionable para vender este producto HOY.
        Devuelve ÚNICAMENTE un objeto JSON válido con esta estructura exacta, sin texto adicional, sin markdown, sin bloques de código:
        {
          "implementacion": "Pasos detallados de implementación del servicio, numerados, con duración estimada por paso.",
          "requerimientos": "Requisitos técnicos y de negocio necesarios del cliente para ejecutar este sprint.",
          "tiempo_y_tecnologias": "Desglose de tiempos por fase y stack tecnológico recomendado con justificación.",
          "empresas_objetivo": "Lista de 3-5 empresas concretas del contexto con un mensaje personalizado de apertura para cada una."
        }
        """;

    private const string PromptTemplateFallback =
        """
        Titulo: {{Title}}
        Empresa: {{Company}}
        Descripcion: {{Description}}
        """;

    public async Task<ProductSuggestion?> SynthesizeProductAsync(int productId, CancellationToken ct = default)
    {
        var product = await dbContext.ProductSuggestions
            .FirstOrDefaultAsync(p => p.Id == productId, ct);

        if (product is null) return null;

        // Cache hit — return immediately
        if (product.LlmStatus == "completed" && product.SynthesisDetailJson is not null)
        {
            logger.LogInformation("ProductSynthesisService: product {Id} already synthesized (cache hit).", productId);
            return product;
        }

        var kernel = kernelProvider.GetKernel();
        if (kernel is null)
        {
            logger.LogWarning("ProductSynthesisService: Semantic Kernel not configured. Cannot synthesize product {Id}.", productId);
            return product;
        }

        await RunSynthesisAsync(kernel, product, ct);
        return product;
    }

    // ── Core ──────────────────────────────────────────────────────────────────────

    private async Task RunSynthesisAsync(Kernel kernel, ProductSuggestion product, CancellationToken ct)
    {
        var promptContext = await BuildPromptContextAsync(product, ct);
        var promptText   = promptContext.PromptText;
        var promptHash   = ComputeHash(promptText);
        var responseText = string.Empty;
        var modelUsed = semanticKernelOptions.Value.ModelId;
        var timer = Stopwatch.StartNew();

        try
        {
            var chat    = kernel.GetRequiredService<IChatCompletionService>();
            var history = new ChatHistory();
            history.AddSystemMessage(SystemPrompt);
            history.AddUserMessage(promptText);

            var (content, selectedModel, plan) = await GetSynthesisWithFallbackAsync(chat, history, ct);
            responseText = content;
            modelUsed = selectedModel;

            product.SynthesisDetailJson = content;
            product.LlmStatus           = "completed";

            await UpdateRelatedClustersAsync(promptContext.ClusterIds, plan, ct);

            SavePromptLogs(
                promptContext,
                promptHash,
                responseText,
                isSuccess: true,
                errorMessage: null,
                latencyMs: (int)timer.ElapsedMilliseconds,
                modelId: modelUsed);
            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation(
                "ProductSynthesisService: product {Id} synthesized OK with model {ModelId} in {LatencyMs} ms.",
                product.Id,
                modelUsed,
                timer.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            product.LlmStatus = "failed";
            logger.LogError(ex, "ProductSynthesisService: product {Id} synthesis failed.", product.Id);

            try
            {
                SavePromptLogs(
                    promptContext,
                    promptHash,
                    responseText,
                    isSuccess: false,
                    errorMessage: ex.Message,
                    latencyMs: (int)timer.ElapsedMilliseconds,
                    modelId: modelUsed);
                await dbContext.SaveChangesAsync(ct);
            }
            catch (Exception saveEx)
            {
                logger.LogError(saveEx, "ProductSynthesisService: failed to persist error state for product {Id}.", product.Id);
            }
        }
    }

    // ── Prompt builder ────────────────────────────────────────────────────────────

    private async Task<PromptBuildContext> BuildPromptContextAsync(ProductSuggestion product, CancellationToken ct)
    {
        var clusterIds = ParseClusterIds(product.ClusterIdsJson);
        var templateConfig = await ResolvePromptTemplateAsync(ct);

        var jobs = clusterIds.Count > 0
            ? await dbContext.JobInsights
                .AsNoTracking()
                .Where(i => i.ClusterId != null && clusterIds.Contains(i.ClusterId.Value) && i.Job != null)
                .Include(i => i.Job)
                .OrderByDescending(i => i.LeadScore)
                .Take(MaxTemplateJobs)
                .Select(i => new PromptJobContext(
                    i.ClusterId!.Value,
                    i.Job!.Title,
                    i.Job.Company,
                    i.Job.Description))
                .ToListAsync(ct)
            : [];

        var companies = jobs
            .Select(j => j.Company)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();

        var renderedTemplateBlocks = jobs
            .Select(j => RenderTemplate(templateConfig.Template, j))
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("OBJETIVO: Generar plan de ataque B2B accionable para vender este producto hoy.");
        sb.AppendLine();
        sb.AppendLine($"Producto: {product.ProductName}");
        sb.AppendLine($"Descripción: {product.ProductDescription}");
        sb.AppendLine($"Oferta actual: {product.Offer}");
        sb.AppendLine($"Stack tecnológico: {product.TechFocus}");
        sb.AppendLine($"Industria dominante: {product.Industry}");
        sb.AppendLine($"Señal de mercado: {product.WhyNow}");
        sb.AppendLine($"Clusters cubiertos: {product.ClusterCount}");
        sb.AppendLine($"Días estimados de entrega: {product.EstimatedBuildDays}");

        if (renderedTemplateBlocks.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("--- CONTEXTO MARKET-JOB-ANALYSIS (template renderizado) ---");
            for (var i = 0; i < renderedTemplateBlocks.Count; i++)
            {
                sb.AppendLine($"[INPUT {i + 1}]");
                sb.AppendLine(renderedTemplateBlocks[i]);
                sb.AppendLine();
            }
        }

        if (companies.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("--- EMPRESAS OBJETIVO ---");
            foreach (var company in companies)
                sb.AppendLine($"- {company}");
        }

        var result = sb.ToString();
        var prompt = result.Length > MaxDescriptionChars ? result[..MaxDescriptionChars] : result;

        return new PromptBuildContext(
            PromptText: prompt,
            PromptVersion: templateConfig.Version,
            ClusterIds: clusterIds,
            PrimaryClusterId: clusterIds.FirstOrDefault());
    }

    private async Task<(string Template, string Version)> ResolvePromptTemplateAsync(CancellationToken ct)
    {
        var configuredTemplate = enrichmentOptions.Value.PromptTemplate;
        var configuredVersion = enrichmentOptions.Value.PromptVersion;

        var dbTemplate = await dbContext.AiPromptTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Key == AiPromptTemplateKeys.MarketJobAnalysis && x.IsActive,
                ct);

        var template = IsUsableMarketTemplate(dbTemplate?.Template)
            ? dbTemplate!.Template
            : configuredTemplate;

        var version = string.IsNullOrWhiteSpace(dbTemplate?.Version)
            ? configuredVersion
            : dbTemplate.Version;

        if (!IsUsableMarketTemplate(template))
        {
            template = PromptTemplateFallback;
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            version = DefaultPromptVersion;
        }

        return (template, version);
    }

    private static bool IsUsableMarketTemplate(string? template)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return false;
        }

        var trimmed = template.Trim();
        if (trimmed.Length < 30)
        {
            return false;
        }

        var lower = trimmed.ToLowerInvariant();
        if (lower is "hello" or "hola" or "test" or "testing")
        {
            return false;
        }

        // The market-job-analysis template must use at least one contextual placeholder.
        return trimmed.Contains("{{Description}}", StringComparison.Ordinal)
            || trimmed.Contains("{{TechnicalContext}}", StringComparison.Ordinal)
            || trimmed.Contains("{{Title}}", StringComparison.Ordinal)
            || trimmed.Contains("{{Company}}", StringComparison.Ordinal);
    }

    private static List<int> ParseClusterIds(string clusterIdsJson)
    {
        try
        {
            return JsonSerializer.Deserialize<List<int>>(clusterIdsJson) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string RenderTemplate(string template, PromptJobContext job)
    {
        var normalizedDescription = NormalizeDescription(job.Description);

        return template
            .Replace("{{Title}}", job.Title)
            .Replace("{{Company}}", job.Company)
            .Replace("{{Description}}", normalizedDescription)
            .Replace("{{TechnicalContext}}", normalizedDescription);
    }

    private static string NormalizeDescription(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "N/A";
        }

        var normalized = raw
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("\t", " ")
            .Trim();

        if (normalized.Length == 0)
        {
            return "N/A";
        }

        var fragments = normalized
            .Split(['.', ';', '!', '?'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.Length > 18)
            .Where(x => !ContainsAny(x, BoilerplateMarkers))
            .ToList();

        if (fragments.Count == 0)
        {
            return TruncateText(normalized, MaxDescriptionSnippetChars);
        }

        var highSignal = fragments
            .Where(x => ContainsAny(x, HighSignalKeywords))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        var selected = highSignal.Count > 0
            ? highSignal
            : fragments
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(2)
                .ToList();

        var compressed = string.Join(". ", selected);
        if (!compressed.EndsWith(".", StringComparison.Ordinal))
        {
            compressed += ".";
        }

        return TruncateText(compressed, MaxDescriptionSnippetChars);
    }

    private static bool ContainsAny(string value, IEnumerable<string> markers)
    {
        foreach (var marker in markers)
        {
            if (value.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string TruncateText(string value, int maxChars)
    {
        if (value.Length <= maxChars)
        {
            return value;
        }

        return value[..maxChars].TrimEnd() + "...";
    }

    private async Task UpdateRelatedClustersAsync(IReadOnlyCollection<int> clusterIds, TacticalPlan plan, CancellationToken ct)
    {
        if (clusterIds.Count == 0)
        {
            return;
        }

        var relatedClusters = await dbContext.MarketClusters
            .Where(c => clusterIds.Contains(c.Id))
            .ToListAsync(ct);

        if (relatedClusters.Count == 0)
        {
            return;
        }

        foreach (var cluster in relatedClusters)
        {
            cluster.SynthesizedPain = plan.Requerimientos;
            cluster.SynthesizedMvp = $"{plan.Implementacion}\n\n{plan.TiempoYTecnologias}";
            cluster.SynthesizedLeadMessage = plan.EmpresasObjetivo;
            cluster.LlmStatus = "completed";
            cluster.LastUpdatedAt = DateTime.UtcNow;
        }
    }

    // ── JSON helpers ──────────────────────────────────────────────────────────────

    private static string StripFences(string raw)
    {
        var json = raw.Trim();
        if (!json.StartsWith("```")) return json;
        var firstNewline = json.IndexOf('\n');
        var lastFence    = json.LastIndexOf("```");
        if (firstNewline > 0 && lastFence > firstNewline)
            return json[(firstNewline + 1)..lastFence].Trim();
        return json;
    }

    private static string ExtractJsonObject(string raw)
    {
        var content = StripFences(raw).Trim();
        var start = content.IndexOf('{');
        if (start < 0)
        {
            return content;
        }

        var depth = 0;
        var inString = false;
        var isEscaped = false;

        for (var index = start; index < content.Length; index++)
        {
            var current = content[index];

            if (isEscaped)
            {
                isEscaped = false;
                continue;
            }

            if (current == '\\' && inString)
            {
                isEscaped = true;
                continue;
            }

            if (current == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
            {
                continue;
            }

            if (current == '{')
            {
                depth++;
            }
            else if (current == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return content[start..(index + 1)];
                }
            }
        }

        return content[start..];
    }

    private static TacticalPlan ParsePlan(string raw)
    {
        if (!TryParsePlan(raw, out var plan, out _, out var errorMessage))
        {
            throw new InvalidOperationException(errorMessage);
        }

        return plan;
    }

    private static bool TryParsePlan(
        string raw,
        out TacticalPlan plan,
        out string normalizedJson,
        out string errorMessage)
    {
        var json = ExtractJsonObject(raw);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var implementacion = root.TryGetProperty("implementacion", out var implProp) ? implProp.GetString() : null;
            var requerimientos = root.TryGetProperty("requerimientos", out var reqProp) ? reqProp.GetString() : null;
            var tiempoTecnologias = root.TryGetProperty("tiempo_y_tecnologias", out var timeProp) ? timeProp.GetString() : null;
            var empresasObjetivo = root.TryGetProperty("empresas_objetivo", out var targetProp) ? targetProp.GetString() : null;

            if (string.IsNullOrWhiteSpace(implementacion))
            {
                plan = default!;
                normalizedJson = string.Empty;
                errorMessage = $"LLM response missing 'implementacion'. Raw: {raw[..Math.Min(200, raw.Length)]}";
                return false;
            }

            plan = new TacticalPlan(
                implementacion,
                requerimientos ?? string.Empty,
                tiempoTecnologias ?? string.Empty,
                empresasObjetivo ?? string.Empty);

            normalizedJson = JsonSerializer.Serialize(new
            {
                implementacion = plan.Implementacion,
                requerimientos = plan.Requerimientos,
                tiempo_y_tecnologias = plan.TiempoYTecnologias,
                empresas_objetivo = plan.EmpresasObjetivo
            });
            errorMessage = string.Empty;
            return true;
        }
        catch (JsonException ex)
        {
            plan = default!;
            normalizedJson = string.Empty;
            errorMessage = $"LLM response was not valid JSON. {ex.Message}. Raw: {raw[..Math.Min(200, raw.Length)]}";
            return false;
        }
    }

    // ── Audit log ─────────────────────────────────────────────────────────────────

    private void SavePromptLogs(
        PromptBuildContext promptContext,
        string promptHash,
        string responseText,
        bool isSuccess,
        string? errorMessage,
        int latencyMs,
        string modelId)
    {
        var clusterIds = promptContext.ClusterIds.Count == 0
            ? [promptContext.PrimaryClusterId]
            : promptContext.ClusterIds;

        foreach (var clusterId in clusterIds.Where(id => id > 0).Distinct())
        {
            dbContext.AiPromptLogs.Add(new AiPromptLog
            {
                ClusterId = clusterId,
                Provider = "SemanticKernel",
                ModelId = modelId,
                PromptVersion = promptContext.PromptVersion,
                PromptHash = promptHash,
                PromptText = promptContext.PromptText,
                ResponseText = responseText,
                IsSuccess = isSuccess,
                Status = isSuccess ? "success" : "failed",
                ErrorMessage = errorMessage,
                LatencyMs = Math.Max(0, latencyMs),
                CreatedAt = DateTime.UtcNow
            });
        }
    }

    private async Task<(string Content, string ModelId, TacticalPlan Plan)> GetSynthesisWithFallbackAsync(
        IChatCompletionService chat,
        ChatHistory history,
        CancellationToken ct)
    {
        var configuredModel = semanticKernelOptions.Value.ModelId;
        var model = string.IsNullOrWhiteSpace(configuredModel) ? FallbackModelId : configuredModel;
        var configuredTimeoutSeconds = semanticKernelOptions.Value.TimeoutSeconds > 0
            ? semanticKernelOptions.Value.TimeoutSeconds
            : 300;
        var primaryTimeoutSeconds = Math.Clamp(configuredTimeoutSeconds - 20, 95, configuredTimeoutSeconds);
        var fallbackTimeoutSeconds = Math.Clamp(configuredTimeoutSeconds - 40, 70, configuredTimeoutSeconds);

        try
        {
            var primary = await ExecuteChatAsync(chat, history, model, timeoutSeconds: primaryTimeoutSeconds, ct);
            if (TryParsePlan(primary, out var primaryPlan, out var normalizedPrimary, out var primaryError))
            {
                return (normalizedPrimary, model, primaryPlan);
            }

            // Retry once with a stricter JSON-only instruction when the first response is malformed/truncated.
            var primaryRetryHistory = CloneHistoryWithExtraUserInstruction(history, StrictJsonRetryInstruction);
            var primaryRetry = await ExecuteChatAsync(chat, primaryRetryHistory, model, timeoutSeconds: primaryTimeoutSeconds, ct);
            if (TryParsePlan(primaryRetry, out var primaryRetryPlan, out var normalizedPrimaryRetry, out _))
            {
                return (normalizedPrimaryRetry, model, primaryRetryPlan);
            }

            logger.LogWarning(
                "ProductSynthesisService: invalid or empty response with model {ModelId}; trying fallback. Error={Error}",
                model,
                primaryError);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException ex)
        {
            logger.LogWarning(
                ex,
                "ProductSynthesisService: primary model {ModelId} timed out after {TimeoutSeconds}s; trying fallback model.",
                model,
                primaryTimeoutSeconds);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "ProductSynthesisService: primary model {ModelId} failed; trying fallback model.",
                model);
        }

        if (model.Equals(FallbackModelId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("LLM response was invalid or empty and fallback model is already in use.");
        }

        var fallback = await ExecuteChatAsync(chat, history, FallbackModelId, timeoutSeconds: fallbackTimeoutSeconds, ct);
        if (!TryParsePlan(fallback, out var fallbackPlan, out var normalizedFallback, out var fallbackError))
        {
            var fallbackRetryHistory = CloneHistoryWithExtraUserInstruction(history, StrictJsonRetryInstruction);
            var fallbackRetry = await ExecuteChatAsync(chat, fallbackRetryHistory, FallbackModelId, timeoutSeconds: fallbackTimeoutSeconds, ct);
            if (TryParsePlan(fallbackRetry, out var fallbackRetryPlan, out var normalizedFallbackRetry, out _))
            {
                return (normalizedFallbackRetry, FallbackModelId, fallbackRetryPlan);
            }

            throw new InvalidOperationException($"Fallback LLM response was invalid. {fallbackError}");
        }

        return (normalizedFallback, FallbackModelId, fallbackPlan);
    }

    private static ChatHistory CloneHistoryWithExtraUserInstruction(ChatHistory source, string instruction)
    {
        var clone = new ChatHistory();
        foreach (var message in source)
        {
            clone.AddMessage(message.Role, message.Content ?? string.Empty);
        }

        clone.AddUserMessage(instruction);
        return clone;
    }

    private static async Task<string> ExecuteChatAsync(
        IChatCompletionService chat,
        ChatHistory history,
        string modelId,
        int timeoutSeconds,
        CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var effectiveTimeoutSeconds = Math.Max(20, timeoutSeconds);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(effectiveTimeoutSeconds));

        var settings = new OpenAIPromptExecutionSettings
        {
            ModelId = modelId,
            MaxTokens = 1600,
            Temperature = 0.2
        };

        try
        {
            var result = await chat.GetChatMessageContentAsync(history, settings, null, timeoutCts.Token);
            return result.Content?.Trim() ?? string.Empty;
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"LLM request timed out after {effectiveTimeoutSeconds}s for model '{modelId}'.",
                ex);
        }
    }

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..16];
    }

    private sealed record PromptBuildContext(
        string PromptText,
        string PromptVersion,
        IReadOnlyCollection<int> ClusterIds,
        int PrimaryClusterId);

    private sealed record PromptJobContext(
        int ClusterId,
        string Title,
        string Company,
        string? Description);

    private sealed record TacticalPlan(
        string Implementacion,
        string Requerimientos,
        string TiempoYTecnologias,
        string EmpresasObjetivo);
}
