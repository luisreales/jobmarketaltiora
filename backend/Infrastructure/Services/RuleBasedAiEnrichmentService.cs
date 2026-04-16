using System.Text.Json;
using System.Text.RegularExpressions;
using backend.Application.Contracts;
using backend.Application.Interfaces;
using backend.Domain.Entities;

namespace backend.Infrastructure.Services;

public sealed class RuleBasedAiEnrichmentService(
    IOpportunityScorerService opportunityScorer,
    JobPreprocessorService preprocessor) : IAiEnrichmentService
{
    public Task<JobInsightAnalysisResult> AnalyzeJobAsync(JobOffer job, CancellationToken cancellationToken = default)
    {
        var context = preprocessor.BuildContext(job);

        // Use clean description + title for classification
        var text = $"{job.Title} {context.CleanDescription}".ToLowerInvariant();

        var (painPoint, category) = ResolvePainPoint(text);

        var urgency = CalculateUrgencyScore(text);
        var isDirectClient = !context.IsConsulting;
        var companyType = isDirectClient ? "DirectClient" : "Consulting";
        var opportunity = opportunityScorer.CalculateOpportunityScore(text, urgency, context.IsConsulting);

        var suggestedSolution = category switch
        {
            "Scaling"             => "Build an API performance accelerator MVP with caching, bottleneck tracing and SLA alerting.",
            "Migration"           => "Create a modernization MVP focused on safe legacy migration with phased rollout and compatibility checks.",
            "Integration"         => "Develop an integration hub MVP to orchestrate third-party APIs and reduce manual operational overhead.",
            "Automation"          => "Deliver a workflow automation MVP that eliminates repetitive manual tasks and improves cycle time.",
            "CloudModernization"  => "Implement a cloud modernization MVP with observability, cost controls and deployment hardening.",
            "DataEngineering"     => "Build a data pipeline MVP that centralizes ingestion, transformation and analytics delivery.",
            "Security"            => "Deliver a security hardening MVP covering auth, secret management and vulnerability scanning.",
            "DevOps"              => "Implement a DevOps acceleration MVP: CI/CD pipelines, infra-as-code and deployment gates.",
            "Microservices"       => "Design a microservices decomposition MVP with domain boundaries, async messaging and observability.",
            "Observability"       => "Deploy an observability MVP with distributed tracing, structured logging and alerting dashboards.",
            _                     => "Design a focused discovery MVP to validate pain points and ship a practical first solution increment."
        };

        var leadMessage = $"Hi {job.Company}, I noticed your team is hiring around '{painPoint}'. We can help with a fast MVP to address this challenge and reduce delivery risk in the first weeks.";

        var techTokensJson = JsonSerializer.Serialize(context.TechTokens);

        var result = new JobInsightAnalysisResult(
            MainPainPoint: painPoint,
            PainCategory: category,
            PainDescription: BuildPainDescription(painPoint, category),
            TechStack: context.NormalizedTechStack,
            OpportunityScore: opportunity,
            UrgencyScore: urgency,
            SuggestedSolution: suggestedSolution,
            LeadMessage: leadMessage,
            IsDirectClient: isDirectClient,
            CompanyType: companyType,
            ConfidenceScore: 0.72,
            DecisionSource: "Rules",
            Status: "Processed",
            RawModelResponse: null,
            Industry: context.IndustryLabel,
            NormalizedTechStack: context.NormalizedTechStack,
            TechTokensJson: techTokensJson,
            LeadScore: 0); // LeadScore is computed after save (needs CapturedAt from JobOffer)

        return Task.FromResult(result);
    }

    private static (string PainPoint, string Category) ResolvePainPoint(string text)
    {
        if (ContainsAny(text, "legacy", "monolith", "migration", "rewrite", "moderniz"))
        {
            return ("Legacy Modernization", "Migration");
        }

        if (ContainsAny(text, "scale", "high traffic", "performance", "latency", "throughput", "bottleneck"))
        {
            return ("API Scaling", "Scaling");
        }

        if (ContainsAny(text, "integration", "integrate", "third-party", "api gateway", "webhook", "middleware"))
        {
            return ("System Integrations", "Integration");
        }

        if (ContainsAny(text, "manual", "spreadsheet", "repetitive", "backoffice", "automation", "automate", "workflow"))
        {
            return ("Workflow Automation", "Automation");
        }

        if (ContainsAny(text, "cloud", "aws", "azure", "gcp", "kubernetes", "containeriz", "serverless"))
        {
            return ("Cloud Modernization", "CloudModernization");
        }

        if (ContainsAny(text, "data pipeline", "etl", "data warehouse", "datalake", "spark", "kafka stream", "databricks", "airflow", "dbt", "analytics engineer"))
        {
            return ("Data Engineering", "DataEngineering");
        }

        if (ContainsAny(text, "security", "pentest", "vulnerability", "soc", "siem", "zero trust", "oauth", "keycloak", "identity", "authentication", "authorization"))
        {
            return ("Security & Identity", "Security");
        }

        if (ContainsAny(text, "devops", "ci/cd", "cicd", "pipeline", "deploy", "release", "infrastructure as code", "terraform", "ansible", "helm"))
        {
            return ("DevOps & CI/CD", "DevOps");
        }

        if (ContainsAny(text, "microservice", "micro service", "domain-driven", "ddd", "event-driven", "event sourcing", "cqrs", "service mesh"))
        {
            return ("Microservices Architecture", "Microservices");
        }

        if (ContainsAny(text, "observab", "monitoring", "tracing", "telemetry", "prometheus", "grafana", "datadog", "opentelemetry", "sentry", "alerting"))
        {
            return ("Observability & Monitoring", "Observability");
        }

        return ("Platform Stabilization", "General");
    }

    private static int CalculateUrgencyScore(string text)
    {
        var urgencySignals = new[] { "urgent", "asap", "immediately", "critical", "production issue", "blocking", "outage" };
        var signalCount = urgencySignals.Count(signal => text.Contains(signal, StringComparison.Ordinal));
        return Math.Clamp(4 + (signalCount * 2), 1, 10);
    }

    private static bool ContainsAny(string text, params string[] values)
    {
        return values.Any(value => text.Contains(value, StringComparison.Ordinal));
    }

    private static string BuildPainDescription(string painPoint, string category)
    {
        return $"The role indicates recurring pressure around {painPoint}. Category: {category}. This suggests a reusable delivery opportunity rather than a one-off task.";
    }
}
