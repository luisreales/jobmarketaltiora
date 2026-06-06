using System.Text;
using System.Text.RegularExpressions;
using backend.Application.Interfaces;

namespace backend.Infrastructure.Services;

/// <summary>
/// Transforms raw job descriptions into compact technical signals for LLM synthesis.
/// Pipeline: RemoveCorporateFluff → ExtractTechnicalSections → NormalizeWhitespace → TrimToTokenBudget
/// </summary>
public sealed partial class JobDescriptionCleanerService(
    ILogger<JobDescriptionCleanerService> logger) : IJobDescriptionCleanerService
{
    private const int MaxCleanChars = 4_000;

    // Section markers that signal the start of non-technical content.
    // Matched case-insensitively; first match triggers truncation.
    private static readonly string[] FluffCutMarkers =
    [
        // English
        "about us", "about the company", "about our company",
        "who we are", "who are we",
        "why join", "why work with us", "why work here",
        "what we offer", "what we provide",
        "our values", "our culture", "company culture",
        "we believe", "our mission",
        "benefits", "perks", "compensation",
        "competitive salary", "salary range", "stock options",
        "healthcare", "dental", "vision plan", "401k", "pto", "paid time off",
        "vacation days", "remote work policy", "flexible schedule",
        "diversity", "inclusion", "equal opportunity", "eeo",
        "we are an equal", "we are committed to diversity",
        "apply now", "send your cv", "send your resume",
        "we look forward to", "join our team",
        // Spanish
        "acerca de nosotros", "sobre nosotros", "quiénes somos", "quienes somos",
        "por qué trabajar", "por que trabajar",
        "nuestros valores", "nuestra cultura", "nuestra misión",
        "lo que ofrecemos", "qué ofrecemos", "que ofrecemos",
        "beneficios", "prestaciones",
        "horario", "horario flexible",
        "salario competitivo", "rango salarial",
        "somos una empresa", "we are a company",
        "igual oportunidad",
    ];

    // Keywords that indicate a line contains technical signal worth keeping.
    private static readonly string[] TechSignals =
    [
        // Architecture & patterns
        "microservic", "monolith", "ddd", "domain driven", "event-driven", "event driven",
        "cqrs", "hexagonal", "clean architecture", "repository pattern",
        "api gateway", "service mesh", "saga", "choreography",
        // Cloud & infra
        "kubernetes", "k8s", "docker", "container", "helm", "terraform",
        "aws", "azure", "gcp", "cloud", "serverless", "lambda",
        "ci/cd", "pipeline", "devops", "gitops", "argocd", "jenkins",
        // Data & streaming
        "kafka", "rabbitmq", "event bus", "message queue", "pubsub",
        "redis", "cache", "elasticsearch", "opensearch",
        "sql", "postgres", "mongodb", "cosmos", "cassandra", "dynamodb",
        "data lake", "data warehouse", "dbt", "airflow", "spark",
        // Languages & frameworks
        ".net", "c#", "java", "spring", "go", "golang", "rust", "python",
        "node.js", "nodejs", "react", "angular", "vue", "typescript",
        // Pain signals (migration, scaling, legacy)
        "legacy", "migration", "moderniz", "refactor", "rewrite", "scalab",
        "performance", "latency", "throughput", "bottleneck", "technical debt",
        "integration", "third-party", "external api", "webhook", "oauth",
        "security", "authentication", "authorization", "sso", "saml",
        // Roles & responsibilities that reveal the real work
        "design", "architect", "implement", "develop", "build", "own",
        "lead", "define", "maintain", "optimize", "deploy", "automate",
    ];

    // Consulting/staffing blacklist — these are indirect clients for AltioraTech.
    private static readonly string[] ConsultingBlacklist =
    [
        // Already in JobPreprocessorService
        "consultora", "consulting", "consultancy", "staffing", "outsourcing",
        "body shop", "nearshore", "offshore", "contracting firm", "recruitment agency",
        "headhunter", "talent acquisition firm", "staff augmentation",
        "we place you", "on behalf of", "our client", "nuestro cliente",
        "it services company", "global delivery", "digital factory",
        "accenture", "deloitte", "ibm consulting", "capgemini", "globant",
        "softserve", "epam", "toptal", "lemontech", "everis", "ntt data",
        // Additional
        "cognizant", "infosys", "wipro", "hcl", "tech mahindra",
        "mphasis", "hexaware", "mindtree", "l&t technology",
        "thoughtworks", "slalom", "booz allen", "kpmg", "pwc",
        "ey technology", "ernst & young", "mckinsey", "bcg",
        "tcs", "tata consultancy",
    ];

    public CleanedJobDescription Clean(string rawDescription, string companyName = "")
    {
        if (string.IsNullOrWhiteSpace(rawDescription))
            return new CleanedJobDescription(string.Empty, false, 0, 0);

        var original = rawDescription.Length;
        var isConsulting = DetectConsulting(companyName, rawDescription);

        var text = RemoveCorporateFluff(rawDescription);
        text = ExtractTechnicalSections(text);
        text = NormalizeWhitespace(text);
        text = TrimToTokenBudget(text);

        logger.LogDebug(
            "JobDescriptionCleaner: {Original}→{Cleaned} chars, consulting={Consulting}",
            original, text.Length, isConsulting);

        return new CleanedJobDescription(text, isConsulting, original, text.Length);
    }

    // ── Stage 1: cut everything after the first fluff marker ────────────────────

    private static string RemoveCorporateFluff(string text)
    {
        var lower = text.ToLowerInvariant();
        var cutAt = int.MaxValue;

        foreach (var marker in FluffCutMarkers)
        {
            var idx = lower.IndexOf(marker, StringComparison.Ordinal);
            if (idx >= 0 && idx < cutAt)
                cutAt = idx;
        }

        return cutAt == int.MaxValue ? text : text[..cutAt];
    }

    // ── Stage 2: keep only lines with technical signals ──────────────────────────

    private static string ExtractTechnicalSections(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // If >60% of lines have tech signals, the whole text is technical — skip filtering.
        var techCount = lines.Count(HasTechSignal);
        if (lines.Length > 0 && (double)techCount / lines.Length >= 0.60)
            return text;

        // Otherwise keep only lines with signals, plus one line of context before each block.
        var result = new StringBuilder();
        for (var i = 0; i < lines.Length; i++)
        {
            if (HasTechSignal(lines[i]))
            {
                // Include the preceding line as context (e.g., a section header)
                if (i > 0 && result.Length > 0 && !HasTechSignal(lines[i - 1]))
                    result.AppendLine(lines[i - 1].Trim());

                result.AppendLine(lines[i].Trim());
            }
        }

        var filtered = result.ToString().Trim();
        // Fallback: if filtering removed too much (less than 100 chars), return original
        return filtered.Length >= 100 ? filtered : text;
    }

    private static bool HasTechSignal(string line)
    {
        var lower = line.ToLowerInvariant();
        return TechSignals.Any(s => lower.Contains(s, StringComparison.Ordinal));
    }

    // ── Stage 3: collapse whitespace ─────────────────────────────────────────────

    private static string NormalizeWhitespace(string text)
    {
        // Collapse runs of 3+ newlines to double newline
        text = MultiNewlineRegex().Replace(text, "\n\n");
        // Collapse inline spaces
        text = MultiSpaceRegex().Replace(text, " ");
        return text.Trim();
    }

    // ── Stage 4: hard token budget ───────────────────────────────────────────────

    private static string TrimToTokenBudget(string text, int maxChars = MaxCleanChars)
        => text.Length <= maxChars ? text : text[..maxChars];

    // ── Consulting detection ──────────────────────────────────────────────────────

    private static bool DetectConsulting(string companyName, string description)
    {
        var combined = $"{companyName} {description}".ToLowerInvariant();
        return ConsultingBlacklist.Any(signal => combined.Contains(signal, StringComparison.Ordinal));
    }

    [GeneratedRegex(@"\n{3,}", RegexOptions.Compiled)]
    private static partial Regex MultiNewlineRegex();

    [GeneratedRegex(@"[ \t]{2,}", RegexOptions.Compiled)]
    private static partial Regex MultiSpaceRegex();
}
