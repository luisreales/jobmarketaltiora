using System.Text.RegularExpressions;
using backend.Domain.Entities;

namespace backend.Infrastructure.Services;

/// <summary>
/// Cleans and structures raw job data before enrichment.
/// Extracted from HybridAiEnrichmentService — this is the canonical preprocessing step
/// for all pipeline workers (Worker 1 insight enrichment, Worker 2 clustering).
/// </summary>
public sealed class JobPreprocessorService(
    TechCanonicalizer techCanonicalizer,
    IndustryClassifier industryClassifier)
{
    // Section headers that mark the start of fluff content (benefits, about us, etc.)
    private static readonly string[] DefaultFluffMarkers =
    [
        "qué ofrecemos", "que ofrecemos", "lo que ofrecemos", "what we offer",
        "beneficios", "benefits", "perks",
        "acerca de nosotros", "sobre nosotros", "about us", "who we are", "who are we",
        "somos una empresa", "we are a company",
        "nuestra cultura", "our culture",
        "equal opportunity",
        "horario", "schedule", "working hours",
        "ubicacion", "ubicación", "location requirements",
        "relocalizacion", "relocalización", "relocation",
        "formacion", "formación", "training provided",
        "we look forward to",
        "apply now", "send your cv",
    ];

    // Consulting company signals — these are indirect clients for AltioraTech.
    private static readonly string[] ConsultingSignals =
    [
        "consultora", "consulting", "consultancy", "staffing", "outsourcing",
        "body shop", "nearshore", "offshore", "contracting firm", "recruitment agency",
        "headhunter", "talent acquisition firm", "staff augmentation",
        "we place you", "on behalf of", "our client", "nuestro cliente",
        "it services company", "global delivery", "digital factory",
        "accenture", "deloitte", "ibm consulting", "capgemini", "globant",
        "softserve", "epam", "toptal", "lemontech", "everis", "ntt data",
    ];

    // Emoji / noise that inflates token counts and confuses classifiers.
    private static readonly string[] NoiseEmojis =
    [
        "✅", "📍", "🤖", "🚀", "🕝", "📚", "🗣️", "💡", "🌍", "🌎", "🌏",
        "🔥", "⭐", "🎯", "📈", "💼", "🏢", "👥", "💰", "🛠️", "⚡",
    ];

    /// <summary>
    /// Builds a structured context object from a raw job offer.
    /// This is the single pre-enrichment step for all downstream services.
    /// </summary>
    public JobContext BuildContext(JobOffer job)
    {
        var rawDescription = job.Description ?? string.Empty;
        var combinedText = $"{job.Title} {job.Company} {rawDescription}";

        var cleanDescription = RemoveFluffTail(rawDescription, DefaultFluffMarkers);
        cleanDescription = StripNoise(cleanDescription);

        var industry = industryClassifier.Classify(combinedText);
        var techTokens = techCanonicalizer.ExtractTokens($"{job.Title} {cleanDescription}");
        var normalizedTechStack = techCanonicalizer.Normalize($"{job.Title} {cleanDescription}");

        var isConsulting = job.IsConsultingCompany
            || DetectConsultingSignals($"{job.Company} {rawDescription}");

        return new JobContext(
            CleanDescription: cleanDescription,
            Industry: industry,
            IndustryLabel: IndustryClassifier.Label(industry),
            TechTokens: techTokens,
            NormalizedTechStack: normalizedTechStack,
            IsConsulting: isConsulting);
    }

    /// <summary>
    /// Detects consulting company signals in text, supplementing the flag already set by the scraper.
    /// </summary>
    public bool DetectConsultingSignals(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var lower = text.ToLowerInvariant();
        return ConsultingSignals.Any(signal => lower.Contains(signal, StringComparison.Ordinal));
    }

    private static string RemoveFluffTail(string description, string[] cutMarkers)
    {
        var lower = description.ToLowerInvariant();
        var cutIndex = -1;

        foreach (var marker in cutMarkers)
        {
            if (string.IsNullOrWhiteSpace(marker))
            {
                continue;
            }

            var idx = lower.IndexOf(marker.ToLowerInvariant(), StringComparison.Ordinal);
            if (idx >= 0 && (cutIndex < 0 || idx < cutIndex))
            {
                cutIndex = idx;
            }
        }

        var core = cutIndex > 0 ? description[..cutIndex] : description;
        return Truncate(core, 2000);
    }

    private static string StripNoise(string text)
    {
        var result = text;
        foreach (var emoji in NoiseEmojis)
        {
            result = result.Replace(emoji, " ", StringComparison.Ordinal);
        }

        // Collapse multiple whitespace/newlines to single space
        result = Regex.Replace(result, @"\s+", " ");
        return result.Trim();
    }

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..maxLength];
}

/// <summary>
/// Structured output from <see cref="JobPreprocessorService.BuildContext"/>.
/// Consumed by RuleBasedAiEnrichmentService, HybridAiEnrichmentService, and ClusterEngine.
/// </summary>
public sealed record JobContext(
    string CleanDescription,
    IndustryType Industry,
    string IndustryLabel,
    IReadOnlyList<string> TechTokens,
    string NormalizedTechStack,
    bool IsConsulting);
