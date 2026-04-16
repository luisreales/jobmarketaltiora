namespace backend.Infrastructure.Services;

/// <summary>
/// Maps raw technology tokens from job descriptions to canonical labels.
/// Prevents cluster fragmentation caused by synonyms (e.g. ".NET Core" and "ASP.NET" → "NET").
/// </summary>
public sealed class TechCanonicalizer
{
    // Maps lowercase token (substring match) → canonical display label.
    // Order matters: more specific tokens should come before general ones.
    private static readonly (string Token, string Canonical)[] TokenMap =
    [
        // .NET ecosystem
        (".net core",       "NET"),
        ("asp.net",         "NET"),
        ("dotnet",          "NET"),
        (".net",            "NET"),
        ("c#",              "CSHARP"),
        ("entity framework","EF"),
        ("ef core",         "EF"),

        // JVM
        ("spring boot",     "SPRING"),
        ("spring",          "SPRING"),
        ("java",            "JAVA"),
        ("kotlin",          "KOTLIN"),
        ("scala",           "SCALA"),

        // Node / JS
        ("node.js",         "NODE"),
        ("node",            "NODE"),
        ("typescript",      "TYPESCRIPT"),
        ("javascript",      "JAVASCRIPT"),
        ("react",           "REACT"),
        ("angular",         "ANGULAR"),
        ("vue",             "VUE"),
        ("next.js",         "NEXTJS"),
        ("nextjs",          "NEXTJS"),

        // Python
        ("fastapi",         "FASTAPI"),
        ("django",          "DJANGO"),
        ("flask",           "FLASK"),
        ("python",          "PYTHON"),

        // Go / Rust
        ("golang",          "GO"),
        (" go ",            "GO"),
        ("rust",            "RUST"),

        // Databases — SQL
        ("sql server",      "SQL"),
        ("ms sql",          "SQL"),
        ("mssql",           "SQL"),
        ("postgresql",      "SQL"),
        ("postgres",        "SQL"),
        ("mysql",           "SQL"),
        ("oracle db",       "SQL"),
        ("oracle",          "SQL"),
        (" sql ",           "SQL"),

        // Databases — NoSQL
        ("mongodb",         "MONGODB"),
        ("couchbase",       "COUCHBASE"),
        ("redis",           "REDIS"),
        ("elasticsearch",   "ELASTICSEARCH"),
        ("cassandra",       "CASSANDRA"),
        ("dynamodb",        "DYNAMODB"),

        // Messaging
        ("kafka",           "KAFKA"),
        ("rabbitmq",        "RABBITMQ"),
        ("service bus",     "SERVICEBUS"),
        ("pubsub",          "PUBSUB"),

        // Cloud — must come before generic "azure"/"aws"/"gcp" so full names match first
        ("azure devops",    "AZURE"),
        ("azure functions", "AZURE"),
        ("azure",           "AZURE"),
        ("aws lambda",      "AWS"),
        ("aws",             "AWS"),
        ("gcp",             "GCP"),
        ("google cloud",    "GCP"),

        // Containers / orchestration
        ("kubernetes",      "KUBERNETES"),
        (" k8s",            "KUBERNETES"),
        ("docker",          "DOCKER"),
        ("helm",            "HELM"),
        ("terraform",       "TERRAFORM"),

        // Architecture patterns
        ("microservices",   "MICROSERVICES"),
        ("micro services",  "MICROSERVICES"),
        ("hexagonal",       "HEXAGONAL"),
        ("ddd",             "DDD"),
        ("event-driven",    "EVENTDRIVEN"),
        ("event driven",    "EVENTDRIVEN"),
        ("cqrs",            "CQRS"),
        ("graphql",         "GRAPHQL"),
        ("grpc",            "GRPC"),
        ("openapi",         "OPENAPI"),
        ("swagger",         "OPENAPI"),

        // Observability
        ("datadog",         "DATADOG"),
        ("prometheus",      "PROMETHEUS"),
        ("grafana",         "GRAFANA"),
        ("opentelemetry",   "OPENTELEMETRY"),
        ("otel",            "OPENTELEMETRY"),
        ("splunk",          "SPLUNK"),
        ("new relic",       "NEWRELIC"),
        ("sentry",          "SENTRY"),

        // Security
        ("oauth",           "OAUTH"),
        ("jwt",             "JWT"),
        ("keycloak",        "KEYCLOAK"),
    ];

    /// <summary>
    /// Extracts canonical tech tokens from raw text (title + description).
    /// Returns a deduplicated, ordered list of canonical labels.
    /// </summary>
    public IReadOnlyList<string> ExtractTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        var lower = text.ToLowerInvariant();
        var found = new List<string>();

        foreach (var (token, canonical) in TokenMap)
        {
            if (!found.Contains(canonical) && lower.Contains(token, StringComparison.Ordinal))
            {
                found.Add(canonical);
            }
        }

        return found;
    }

    /// <summary>
    /// Returns a comma-separated string of the top N canonical tokens for display/storage.
    /// </summary>
    public string Normalize(string text, int maxTokens = 6)
    {
        var tokens = ExtractTokens(text);
        if (tokens.Count == 0)
        {
            return "Unknown";
        }

        return string.Join(", ", tokens.Take(maxTokens));
    }
}
