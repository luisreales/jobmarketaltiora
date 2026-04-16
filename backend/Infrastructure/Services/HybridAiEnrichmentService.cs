using backend.Application.Contracts;
using backend.Application.Interfaces;
using backend.Domain.Entities;
using backend.Infrastructure.Data;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace backend.Infrastructure.Services;

public sealed class HybridAiEnrichmentService(
    RuleBasedAiEnrichmentService ruleBasedService,
    ISemanticKernelProvider semanticKernelProvider,
    IOptions<MarketIntelligenceEnrichmentOptions> options,
    IOptions<SemanticKernelOptions> semanticKernelOptions,
    ApplicationDbContext dbContext,
    ILogger<HybridAiEnrichmentService> logger) : IAiEnrichmentService
{
    private static readonly string[] TaskKeywords =
    {
        "diseno", "diseño", "desarrollo", "develop", "build", "implement", "arquitect", "architecture",
        "api", "integration", "migrat", "refactor", "testing", "test", "sprint", "clean code", "optimiz"
    };

    private static readonly string[] FluffKeywords =
    {
        "beneficios", "que ofrecemos", "qué ofrecemos", "acerca de nosotros", "about us", "somos",
        "horario", "ubicacion", "ubicación", "relocalizacion", "relocalización", "formacion", "formación"
    };

    private static readonly (string Token, string Label)[] KnownTechTokens =
    {
        ("java", "Java"),
        ("spring", "Spring"),
        ("kafka", "Kafka"),
        ("react", "React"),
        ("typescript", "TypeScript"),
        ("javascript", "JavaScript"),
        (".net", ".NET"),
        ("c#", "C#"),
        ("postgres", "PostgreSQL"),
        ("mongodb", "MongoDB"),
        ("couchbase", "Couchbase"),
        ("redis", "Redis"),
        ("docker", "Docker"),
        ("kubernetes", "Kubernetes"),
        ("azure", "Azure"),
        ("aws", "AWS"),
        ("gcp", "GCP"),
        ("ddd", "DDD"),
        ("hexagonal", "Hexagonal"),
        ("openapi", "OpenAPI")
    };

    public async Task<JobInsightAnalysisResult> AnalyzeJobAsync(JobOffer job, CancellationToken cancellationToken = default)
    {
        var ruleResult = await ruleBasedService.AnalyzeJobAsync(job, cancellationToken);

        if (!options.Value.UseSemanticKernel)
        {
            return ruleResult;
        }

        if (options.Value.SkipConsultingCompaniesForLlm
            && ShouldSkipCompany(job, options.Value.ConsultingCompanyBlacklist))
        {
            return ruleResult with
            {
                RawModelResponse = MergeRawResponse(ruleResult.RawModelResponse, "SemanticKernelShadow=SkippedByCompanyPolicy")
            };
        }

        var promptContext = BuildPromptContext(job, options.Value.FluffCutMarkers);

        if (promptContext.TechnicalContext.Length < Math.Max(1, options.Value.MinDescriptionLength))
        {
            return ruleResult with
            {
                RawModelResponse = MergeRawResponse(ruleResult.RawModelResponse, "SemanticKernelShadow=SkippedByMinLength")
            };
        }

        if (!ShouldSample(job.Id, options.Value.SamplingRatePercent))
        {
            return ruleResult with
            {
                RawModelResponse = MergeRawResponse(ruleResult.RawModelResponse, "SemanticKernelShadow=SkippedBySampling")
            };
        }

        var dayStart = DateTime.UtcNow.Date;
        var callsToday = await dbContext.AiPromptLogs
            .AsNoTracking()
            .CountAsync(x => x.CreatedAt >= dayStart && !x.CacheHit, cancellationToken);
        if (callsToday >= Math.Max(1, options.Value.MaxCallsPerDay))
        {
            return ruleResult with
            {
                RawModelResponse = MergeRawResponse(ruleResult.RawModelResponse, "SemanticKernelShadow=SkippedByDailyBudget")
            };
        }

        var (prompt, promptVersion) = await BuildPromptAsync(job, promptContext, cancellationToken);
        var promptHash = ComputeSha256(prompt);

        var cacheSince = DateTime.UtcNow.AddHours(-Math.Max(1, options.Value.CacheHours));
        var cachedLog = await dbContext.AiPromptLogs
            .AsNoTracking()
            .Where(x => x.PromptHash == promptHash && x.IsSuccess && x.CreatedAt >= cacheSince)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (cachedLog is not null)
        {
            await SavePromptLogAsync(
                job.Id,
                promptVersion,
                promptHash,
                prompt,
                cachedLog.ResponseText,
                cacheHit: true,
                isSuccess: true,
                status: "cached",
                errorMessage: null,
                latencyMs: 0,
                cancellationToken: cancellationToken);

            return ruleResult with
            {
                RawModelResponse = MergeRawResponse(ruleResult.RawModelResponse, "SemanticKernelShadow=CacheHit")
            };
        }

        var timer = Stopwatch.StartNew();

        try
        {
            if (!semanticKernelProvider.IsConfigured)
            {
                logger.LogWarning(
                    "AI enrichment stage={Stage} jobId={JobId} fallback={Fallback} reason={Reason}",
                    "semantic-kernel",
                    job.Id,
                    true,
                    "NotConfigured");

                await SavePromptLogAsync(
                    job.Id,
                    promptVersion,
                    promptHash,
                    prompt,
                    string.Empty,
                    cacheHit: false,
                    isSuccess: false,
                    status: "not_configured",
                    errorMessage: "SemanticKernel provider is not configured",
                    latencyMs: (int)timer.ElapsedMilliseconds,
                    cancellationToken: cancellationToken);

                return ruleResult;
            }

            var kernel = semanticKernelProvider.GetKernel();
            var chatService = kernel.GetRequiredService<IChatCompletionService>();

            var history = new ChatHistory();
            history.AddSystemMessage("You are a concise analysis assistant. Respond in one line only.");
            history.AddUserMessage(prompt);
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                // Keep responses bounded so enrichment does not stall batches on long reasoning.
                MaxTokens = 220
            };

            var responseMessages = await chatService.GetChatMessageContentsAsync(history, executionSettings, kernel, cancellationToken);
            var modelResponse = responseMessages.LastOrDefault()?.Content ?? string.Empty;

            await SavePromptLogAsync(
                job.Id,
                promptVersion,
                promptHash,
                prompt,
                modelResponse,
                cacheHit: false,
                isSuccess: true,
                status: "success",
                errorMessage: null,
                latencyMs: (int)timer.ElapsedMilliseconds,
                cancellationToken: cancellationToken);

            logger.LogInformation(
                "AI enrichment stage={Stage} jobId={JobId} shadowMode={ShadowMode} latencyMs={LatencyMs} decisionSource={DecisionSource}",
                "semantic-kernel",
                job.Id,
                options.Value.ShadowMode,
                timer.ElapsedMilliseconds,
                ruleResult.DecisionSource);

            // Shadow mode: keep rules as source of truth until SK prompts/contracts are finalized.
            return ruleResult with
            {
                RawModelResponse = MergeRawResponse(ruleResult.RawModelResponse, $"SemanticKernelShadow=Connected;Model={semanticKernelOptions.Value.ModelId}")
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "AI enrichment stage={Stage} jobId={JobId} fallback={Fallback} latencyMs={LatencyMs}",
                "semantic-kernel",
                job.Id,
                true,
                timer.ElapsedMilliseconds);

            await SavePromptLogAsync(
                job.Id,
                promptVersion,
                promptHash,
                prompt,
                string.Empty,
                cacheHit: false,
                isSuccess: false,
                status: "failed",
                errorMessage: ex.ToString(),
                latencyMs: (int)timer.ElapsedMilliseconds,
                cancellationToken: cancellationToken);

            return ruleResult with
            {
                RawModelResponse = MergeRawResponse(ruleResult.RawModelResponse, "SemanticKernelShadow=FallbackToRules")
            };
        }
    }

    private async Task SavePromptLogAsync(
        int? jobId,
        string promptVersion,
        string promptHash,
        string promptText,
        string responseText,
        bool cacheHit,
        bool isSuccess,
        string status,
        string? errorMessage,
        int latencyMs,
        CancellationToken cancellationToken)
    {
        var provider = semanticKernelOptions.Value.Provider;
        var modelId = semanticKernelOptions.Value.ModelId;

        var promptTokens = ApproximateTokens(promptText);
        var completionTokens = ApproximateTokens(responseText);

        dbContext.AiPromptLogs.Add(new AiPromptLog
        {
            JobId = jobId,
            Provider = provider,
            ModelId = modelId,
            PromptVersion = promptVersion,
            PromptHash = promptHash,
            PromptText = promptText,
            ResponseText = responseText,
            CacheHit = cacheHit,
            IsSuccess = isSuccess,
            Status = status,
            ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? null : errorMessage,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            TotalTokens = promptTokens + completionTokens,
            LatencyMs = Math.Max(0, latencyMs),
            CreatedAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static bool ShouldSample(int jobId, int samplingRatePercent)
    {
        var rate = Math.Clamp(samplingRatePercent, 1, 100);
        return (Math.Abs(jobId) % 100) < rate;
    }

    private async Task<(string Prompt, string PromptVersion)> BuildPromptAsync(JobOffer job, PromptContext promptContext, CancellationToken cancellationToken)
    {
        var configuredTemplate = options.Value.PromptTemplate;
        var configuredVersion = options.Value.PromptVersion;

        var dbTemplate = await dbContext.AiPromptTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Key == AiPromptTemplateKeys.MarketJobAnalysis && x.IsActive,
                cancellationToken);

        var template = string.IsNullOrWhiteSpace(dbTemplate?.Template)
            ? configuredTemplate
            : dbTemplate.Template;
        var promptVersion = string.IsNullOrWhiteSpace(dbTemplate?.Version)
            ? configuredVersion
            : dbTemplate.Version;

        if (string.IsNullOrWhiteSpace(template))
        {
            template = "Analyze this technical context and return ONLY JSON with keys pain_point, business_opportunity, altioratech_offer, confidence. Context={{TechnicalContext}}";
        }

        var renderedPrompt = template
            .Replace("{{Title}}", job.Title)
            .Replace("{{Company}}", job.Company)
            .Replace("{{Description}}", promptContext.TechnicalContext)
            .Replace("{{TechnicalContext}}", promptContext.TechnicalContext)
            .Replace("{{Tech}}", promptContext.Tech)
            .Replace("{{Tasks}}", promptContext.Tasks);

        return (renderedPrompt, promptVersion);
    }

    private static bool ShouldSkipCompany(JobOffer job, string[] blacklist)
    {
        if (job.IsConsultingCompany)
        {
            return true;
        }

        var company = job.Company?.Trim();
        if (string.IsNullOrWhiteSpace(company))
        {
            return false;
        }

        return blacklist.Any(blocked =>
            !string.IsNullOrWhiteSpace(blocked)
            && company.Contains(blocked, StringComparison.OrdinalIgnoreCase));
    }

    private static PromptContext BuildPromptContext(JobOffer job, string[] configuredCutMarkers)
    {
        var description = job.Description ?? string.Empty;
        var trimmed = RemoveFluffTail(description, configuredCutMarkers);

        var techParts = new List<string>();
        var taskParts = new List<string>();

        foreach (var line in trimmed.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var normalized = NormalizeText(line);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            var lower = normalized.ToLowerInvariant();
            if (ContainsAny(lower, FluffKeywords))
            {
                continue;
            }

            if (KnownTechTokens.Any(token => lower.Contains(token.Token, StringComparison.Ordinal)))
            {
                techParts.Add(normalized);
            }

            if (ContainsAny(lower, TaskKeywords))
            {
                taskParts.Add(normalized);
            }
        }

        var tech = techParts.Count > 0
            ? string.Join(" | ", techParts.Distinct(StringComparer.OrdinalIgnoreCase).Take(4))
            : InferTechStack(trimmed);

        var tasks = taskParts.Count > 0
            ? string.Join(" | ", taskParts.Distinct(StringComparer.OrdinalIgnoreCase).Take(4))
            : Truncate(NormalizeText(trimmed), 420);

        if (string.IsNullOrWhiteSpace(tech))
        {
            tech = "Unknown";
        }

        if (string.IsNullOrWhiteSpace(tasks))
        {
            tasks = "Not explicitly listed";
        }

        var technicalContext = $"Title: {job.Title}\nCompany: {job.Company}\nTech: {tech}\nTasks: {tasks}";
        return new PromptContext(Truncate(technicalContext, 1600), tech, tasks);
    }

    private static string RemoveFluffTail(string description, string[] configuredCutMarkers)
    {
        var markers = configuredCutMarkers is { Length: > 0 }
            ? configuredCutMarkers
            : FluffKeywords;

        var lower = description.ToLowerInvariant();
        var cutIndex = -1;

        foreach (var marker in markers)
        {
            if (string.IsNullOrWhiteSpace(marker))
            {
                continue;
            }

            var index = lower.IndexOf(marker.ToLowerInvariant(), StringComparison.Ordinal);
            if (index >= 0 && (cutIndex < 0 || index < cutIndex))
            {
                cutIndex = index;
            }
        }

        var core = cutIndex > 0 ? description[..cutIndex] : description;
        return Truncate(core, 1800);
    }

    private static string InferTechStack(string text)
    {
        var lower = text.ToLowerInvariant();
        var inferred = KnownTechTokens
            .Where(token => lower.Contains(token.Token, StringComparison.Ordinal))
            .Select(token => token.Label)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();

        return inferred.Count == 0 ? string.Empty : string.Join(", ", inferred);
    }

    private static bool ContainsAny(string text, string[] keywords)
    {
        return keywords.Any(keyword => text.Contains(keyword, StringComparison.Ordinal));
    }

    private static string NormalizeText(string text)
    {
        var normalized = text
            .Replace("✅", " ")
            .Replace("📍", " ")
            .Replace("🤖", " ")
            .Replace("🚀", " ")
            .Replace("🕝", " ")
            .Replace("📚", " ")
            .Replace("🗣️", " ");

        normalized = Regex.Replace(normalized, @"\s+", " ");
        return normalized.Trim(' ', '-', ':', ';', ',', '.', '|');
    }

    private sealed record PromptContext(string TechnicalContext, string Tech, string Tasks);

    private static string ComputeSha256(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static int ApproximateTokens(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        return Math.Max(1, text.Length / 4);
    }

    private static string Truncate(string? text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return text.Length <= maxLength ? text : text[..maxLength];
    }

    private static string MergeRawResponse(string? existing, string marker)
    {
        return string.IsNullOrWhiteSpace(existing)
            ? marker
            : $"{existing};{marker}";
    }
}
