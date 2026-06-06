using backend.Application.Contracts;
using backend.Application.Interfaces;
using backend.Domain.Entities;
using backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace backend.Infrastructure.Services;

public class TechnologyIntelligenceService(
    ApplicationDbContext db,
    TechCanonicalizer canonicalizer,
    ILogger<TechnologyIntelligenceService> logger) : ITechnologyIntelligenceService
{
    // Canonical names that are AI-related
    private static readonly HashSet<string> AiTokens = new(StringComparer.Ordinal)
    {
        "OPENAI", "LANGCHAIN", "RAG", "VECTORDB", "AIAGENT", "COPILOT", "SEMANTICKERNEL",
        "PYTORCH", "TENSORFLOW", "LLAMA", "HUGGINGFACE", "MLFLOW", "AUTOGEN", "CLAUDE"
    };

    // Canonical names that are cloud-related
    private static readonly HashSet<string> CloudTokens = new(StringComparer.Ordinal)
    {
        "AZURE", "AWS", "GCP"
    };

    // Display names for canonical tokens
    private static readonly Dictionary<string, string> DisplayNames = new(StringComparer.Ordinal)
    {
        ["NET"] = ".NET", ["CSHARP"] = "C#", ["EF"] = "Entity Framework",
        ["SPRING"] = "Spring Boot", ["JAVA"] = "Java", ["KOTLIN"] = "Kotlin", ["SCALA"] = "Scala",
        ["NODE"] = "Node.js", ["TYPESCRIPT"] = "TypeScript", ["JAVASCRIPT"] = "JavaScript",
        ["REACT"] = "React", ["ANGULAR"] = "Angular", ["VUE"] = "Vue.js",
        ["NEXTJS"] = "Next.js", ["FASTAPI"] = "FastAPI", ["DJANGO"] = "Django",
        ["FLASK"] = "Flask", ["PYTHON"] = "Python", ["GO"] = "Go", ["RUST"] = "Rust",
        ["SQL"] = "SQL / PostgreSQL", ["MONGODB"] = "MongoDB", ["REDIS"] = "Redis",
        ["ELASTICSEARCH"] = "Elasticsearch", ["CASSANDRA"] = "Cassandra",
        ["DYNAMODB"] = "DynamoDB", ["COUCHBASE"] = "Couchbase",
        ["KAFKA"] = "Kafka", ["RABBITMQ"] = "RabbitMQ", ["SERVICEBUS"] = "Azure Service Bus",
        ["PUBSUB"] = "Pub/Sub",
        ["AZURE"] = "Azure", ["AWS"] = "AWS", ["GCP"] = "Google Cloud",
        ["KUBERNETES"] = "Kubernetes", ["DOCKER"] = "Docker", ["HELM"] = "Helm",
        ["TERRAFORM"] = "Terraform",
        ["MICROSERVICES"] = "Microservices", ["HEXAGONAL"] = "Hexagonal Architecture",
        ["DDD"] = "Domain-Driven Design", ["EVENTDRIVEN"] = "Event-Driven",
        ["CQRS"] = "CQRS", ["GRAPHQL"] = "GraphQL", ["GRPC"] = "gRPC",
        ["OPENAPI"] = "OpenAPI / Swagger",
        ["DATADOG"] = "Datadog", ["PROMETHEUS"] = "Prometheus", ["GRAFANA"] = "Grafana",
        ["OPENTELEMETRY"] = "OpenTelemetry", ["SPLUNK"] = "Splunk",
        ["NEWRELIC"] = "New Relic", ["SENTRY"] = "Sentry",
        ["OAUTH"] = "OAuth", ["JWT"] = "JWT", ["KEYCLOAK"] = "Keycloak",
        ["OPENAI"] = "OpenAI", ["LANGCHAIN"] = "LangChain", ["RAG"] = "RAG",
        ["VECTORDB"] = "Vector DB", ["AIAGENT"] = "AI Agents", ["COPILOT"] = "Copilot",
        ["SEMANTICKERNEL"] = "Semantic Kernel", ["PYTORCH"] = "PyTorch",
        ["TENSORFLOW"] = "TensorFlow", ["LLAMA"] = "Llama / Ollama",
        ["HUGGINGFACE"] = "Hugging Face", ["MLFLOW"] = "MLflow",
        ["AUTOGEN"] = "AutoGen", ["CLAUDE"] = "Claude / Anthropic"
    };

    public async Task<TechRebuildResultDto> RebuildAsync(CancellationToken ct = default)
    {
        var startedAt = DateTime.UtcNow;
        logger.LogInformation("TechnologyIntelligenceService: rebuild started.");

        // 1. Load jobs with insights
        var jobs = await db.JobOffers
            .AsNoTracking()
            .Where(j => j.Description != null && j.Description.Length > 30)
            .Select(j => new
            {
                j.Id,
                j.Title,
                j.Description,
                j.CapturedAt,
                Insight = db.JobInsights
                    .Where(i => i.JobId == j.Id)
                    .Select(i => new
                    {
                        i.OpportunityScore,
                        i.UrgencyScore,
                        i.LeadScore,
                        i.Industry,
                        i.ClusterId
                    })
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        logger.LogInformation("TechnologyIntelligenceService: loaded {Count} jobs.", jobs.Count);

        var now = DateTime.UtcNow;
        var week0Start = now.AddDays(-7);
        var week1Start = now.AddDays(-14);

        // Per-token aggregation: Name → stats
        var stats = new Dictionary<string, TechStats>(StringComparer.Ordinal);

        // Per-job token list for co-occurrence
        var jobTokensList = new List<(DateTime CapturedAt, List<string> Tokens,
            string Industry, double OppScore)>(jobs.Count);

        foreach (var job in jobs)
        {
            var tokens = canonicalizer.ExtractTokens(job.Title + " " + job.Description).ToList();
            if (tokens.Count == 0) continue;

            var industry = job.Insight?.Industry ?? "Unknown";
            var oppScore = job.Insight?.OpportunityScore ?? 0.0;
            var urgency = job.Insight?.UrgencyScore ?? 0.0;
            var leadScore = job.Insight?.LeadScore ?? 0.0;
            var clusterId = job.Insight?.ClusterId;

            jobTokensList.Add((job.CapturedAt, tokens, industry, oppScore));

            foreach (var token in tokens)
            {
                if (!stats.TryGetValue(token, out var s))
                {
                    s = new TechStats();
                    stats[token] = s;
                }

                s.TotalMentions++;
                if (job.CapturedAt >= week0Start) s.WeeklyMentions++;
                if (job.CapturedAt >= week1Start && job.CapturedAt < week0Start) s.PrevWeekMentions++;
                if (s.FirstSeen == default || job.CapturedAt < s.FirstSeen) s.FirstSeen = job.CapturedAt;
                if (job.CapturedAt > s.LastSeen) s.LastSeen = job.CapturedAt;
                s.Industries.Add(industry);
                if (clusterId.HasValue) s.ClusterIds.Add(clusterId.Value);
                s.TotalOppScore += oppScore;
                s.TotalUrgency += urgency;
                s.TotalLeadScore += leadScore;
            }
        }

        // 2. Upsert Technologies
        var existingTechs = await db.Technologies.ToDictionaryAsync(t => t.Name, t => t, ct);
        var upsertedCount = 0;

        // Pre-compute demand rank percentile (0=highest, 1=lowest) for lifecycle fallback
        // when weekly signals are absent (all jobs are older than 14 days).
        var sortedByMentions = stats.Keys
            .OrderByDescending(k => stats[k].TotalMentions)
            .ToList();
        var demandRank = sortedByMentions
            .Select((n, idx) => (n, pct: (double)idx / Math.Max(sortedByMentions.Count - 1, 1)))
            .ToDictionary(x => x.n, x => x.pct);

        foreach (var (name, s) in stats)
        {
            var count = s.TotalMentions;
            var growthRate = (s.WeeklyMentions - s.PrevWeekMentions) / Math.Max(s.PrevWeekMentions, 1.0) * 100;
            var momentumScore = Math.Clamp(growthRate, -100, 100);
            var demandScore = Math.Min(100, Math.Log(1 + count) / Math.Log(1 + 200) * 100);
            var competitionScore = Math.Min(100, s.ClusterIds.Count / 15.0 * 100);
            var avgOpp = count > 0 ? s.TotalOppScore / count : 0;
            var opportunityScore = demandScore * 0.4 + (100 - competitionScore) * 0.3 + avgOpp * 0.3;
            var daysSinceFirst = (now - s.FirstSeen).TotalDays;
            var recency = Math.Max(0, (60 - daysSinceFirst) / 60.0 * 100);
            var emergingScore = recency * 0.5 + Math.Max(0, momentumScore) * 0.5;
            var rankPct = demandRank.TryGetValue(name, out var rp) ? rp : 0.5;
            var lifecycleStage = ComputeLifecycle(daysSinceFirst, momentumScore, count, growthRate, rankPct);
            var avgUrgency = count > 0 ? s.TotalUrgency / count : 0;
            var avgLeadScore = count > 0 ? s.TotalLeadScore / count : 0;
            var isAi = AiTokens.Contains(name);
            var isCloud = CloudTokens.Contains(name);
            var displayName = DisplayNames.TryGetValue(name, out var dn) ? dn : name;
            var category = GetCategory(name);

            if (existingTechs.TryGetValue(name, out var existing))
            {
                existing.DisplayName = displayName;
                existing.Category = category;
                existing.LastSeenAt = s.LastSeen;
                existing.TotalMentions = count;
                existing.WeeklyMentions = s.WeeklyMentions;
                existing.GrowthRate = Math.Round(growthRate, 2);
                existing.MomentumScore = Math.Round(momentumScore, 2);
                existing.DemandScore = Math.Round(demandScore, 2);
                existing.CompetitionScore = Math.Round(competitionScore, 2);
                existing.OpportunityScore = Math.Round(opportunityScore, 2);
                existing.EmergingScore = Math.Round(emergingScore, 2);
                existing.AvgLeadScore = Math.Round(avgLeadScore, 2);
                existing.AvgUrgency = Math.Round(avgUrgency, 2);
                existing.IndustryCoverageCount = s.Industries.Count;
                existing.ClusterCoverageCount = s.ClusterIds.Count;
                existing.IsAiRelated = isAi;
                existing.IsCloudRelated = isCloud;
                existing.IsLegacy = lifecycleStage == "Legacy";
                existing.LifecycleStage = lifecycleStage;
                existing.UpdatedAt = now;
            }
            else
            {
                var tech = new Technology
                {
                    Name = name,
                    DisplayName = displayName,
                    Category = category,
                    FirstSeenAt = s.FirstSeen,
                    LastSeenAt = s.LastSeen,
                    TotalMentions = count,
                    WeeklyMentions = s.WeeklyMentions,
                    GrowthRate = Math.Round(growthRate, 2),
                    MomentumScore = Math.Round(momentumScore, 2),
                    DemandScore = Math.Round(demandScore, 2),
                    CompetitionScore = Math.Round(competitionScore, 2),
                    OpportunityScore = Math.Round(opportunityScore, 2),
                    EmergingScore = Math.Round(emergingScore, 2),
                    AvgLeadScore = Math.Round(avgLeadScore, 2),
                    AvgUrgency = Math.Round(avgUrgency, 2),
                    IndustryCoverageCount = s.Industries.Count,
                    ClusterCoverageCount = s.ClusterIds.Count,
                    IsAiRelated = isAi,
                    IsCloudRelated = isCloud,
                    IsLegacy = lifecycleStage == "Legacy",
                    LifecycleStage = lifecycleStage,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                db.Technologies.Add(tech);
                existingTechs[name] = tech;
            }
            upsertedCount++;
        }

        await db.SaveChangesAsync(ct);

        // Reload to get IDs for all upserted techs
        var techLookup = await db.Technologies.ToDictionaryAsync(t => t.Name, t => t.Id, ct);

        // 3. Co-occurrence relationships
        var pairStats = new Dictionary<(string A, string B), PairStats>();

        foreach (var (capturedAt, tokens, industry, oppScore) in jobTokensList)
        {
            var sorted = tokens.Order().ToList();
            for (var i = 0; i < sorted.Count; i++)
            for (var j = i + 1; j < sorted.Count; j++)
            {
                var key = (sorted[i], sorted[j]);
                if (!pairStats.TryGetValue(key, out var ps))
                {
                    ps = new PairStats();
                    pairStats[key] = ps;
                }
                ps.Count++;
                if (capturedAt > ps.LastSeen) ps.LastSeen = capturedAt;
                ps.IndustryCounts.TryGetValue(industry, out var ic);
                ps.IndustryCounts[industry] = ic + 1;
                ps.TotalOppScore += oppScore;
            }
        }

        // Delete existing relationships and rebuild (simpler than upsert for composite PK)
        await db.TechnologyRelationships.ExecuteDeleteAsync(ct);

        var relationships = new List<TechnologyRelationship>();
        foreach (var ((nameA, nameB), ps) in pairStats)
        {
            if (ps.Count < 2) continue;
            if (!techLookup.TryGetValue(nameA, out var idA)) continue;
            if (!techLookup.TryGetValue(nameB, out var idB)) continue;

            var mentionsA = stats.TryGetValue(nameA, out var sA) ? sA.TotalMentions : 1;
            var mentionsB = stats.TryGetValue(nameB, out var sB) ? sB.TotalMentions : 1;
            var correlation = ps.Count / (double)Math.Min(mentionsA, mentionsB) * 100;
            var topIndustry = ps.IndustryCounts.OrderByDescending(k => k.Value).First().Key;
            var avgOpp = ps.Count > 0 ? ps.TotalOppScore / ps.Count : 0;

            relationships.Add(new TechnologyRelationship
            {
                SourceTechnologyId = idA,
                TargetTechnologyId = idB,
                CoOccurrenceCount = ps.Count,
                CorrelationScore = Math.Round(correlation, 2),
                IndustryAffinity = topIndustry,
                OpportunityAffinity = Math.Round(avgOpp, 2),
                AiAffinity = AiTokens.Contains(nameA) && AiTokens.Contains(nameB),
                LastSeenAt = ps.LastSeen
            });
        }

        db.TechnologyRelationships.AddRange(relationships);
        await db.SaveChangesAsync(ct);

        // 4. Weekly trend snapshots (append-only)
        var existingSnapshots = await db.TechnologyTrendSnapshots
            .Select(s => new { s.TechnologyId, s.SnapshotWeek })
            .ToListAsync(ct);
        var existingSnapshotKeys = existingSnapshots
            .ToHashSet(s => (s.TechnologyId, s.SnapshotWeek));

        // Group job mentions by (tech, week)
        var weeklyGroups =
            from entry in jobTokensList
            from token in entry.Tokens
            where techLookup.ContainsKey(token)
            let weekStart = GetWeekStart(entry.CapturedAt)
            group (entry.CapturedAt, entry.OppScore) by (techLookup[token], weekStart)
            into g
            select new
            {
                TechId = g.Key.Item1,
                Week = g.Key.weekStart,
                MentionCount = g.Count(),
                UniqueJobCount = g.Select(x => x.CapturedAt.Date).Distinct().Count(),
                AvgOpp = g.Average(x => x.OppScore)
            };

        var newSnapshots = new List<TechnologyTrendSnapshot>();
        foreach (var wg in weeklyGroups)
        {
            if (existingSnapshotKeys.Contains((wg.TechId, wg.Week))) continue;
            newSnapshots.Add(new TechnologyTrendSnapshot
            {
                TechnologyId = wg.TechId,
                SnapshotWeek = wg.Week,
                MentionCount = wg.MentionCount,
                UniqueJobCount = wg.UniqueJobCount,
                AvgOpportunityScore = Math.Round(wg.AvgOpp, 2),
                CreatedAt = now
            });
        }

        db.TechnologyTrendSnapshots.AddRange(newSnapshots);
        await db.SaveChangesAsync(ct);

        var duration = DateTime.UtcNow - startedAt;
        logger.LogInformation(
            "TechnologyIntelligenceService: done. techs={T} rels={R} snapshots={S} jobs={J} duration={D}",
            upsertedCount, relationships.Count, newSnapshots.Count, jobs.Count, duration);

        return new TechRebuildResultDto(
            TechnologiesUpserted: upsertedCount,
            RelationshipsUpserted: relationships.Count,
            SnapshotsAdded: newSnapshots.Count,
            JobsProcessed: jobs.Count,
            Duration: duration,
            RanAt: now);
    }

    private static string ComputeLifecycle(double daysSinceFirst, double momentum, int mentions, double growthRate, double demandRankPct)
    {
        // Primary: time-based signals (requires fresh weekly data)
        if (daysSinceFirst < 60 && momentum > 5) return "Emerging";
        if (momentum > 10) return "Growing";
        if (growthRate < -30 && mentions > 10) return "Legacy";
        if (momentum < -15 && mentions > 5) return "Declining";

        // Fallback rank-based classification when weekly data is stale (all momentum ≈ 0)
        if (mentions <= 2) return "Emerging";                           // very niche / rarely seen
        if (demandRankPct <= 0.25) return "Growing";                    // top quartile by mentions
        if (demandRankPct >= 0.75 && mentions >= 5) return "Declining"; // bottom quartile
        return "Mature";
    }

    private static string GetCategory(string canonical) => canonical switch
    {
        "OPENAI" or "LANGCHAIN" or "RAG" or "VECTORDB" or "AIAGENT" or "COPILOT"
            or "SEMANTICKERNEL" or "PYTORCH" or "TENSORFLOW" or "LLAMA" or "HUGGINGFACE"
            or "MLFLOW" or "AUTOGEN" or "CLAUDE" => "AI",

        "AZURE" or "AWS" or "GCP" => "Cloud",

        "NET" or "CSHARP" or "JAVA" or "KOTLIN" or "SCALA" or "PYTHON" or "FASTAPI"
            or "DJANGO" or "FLASK" or "GO" or "RUST" or "NODE" or "SPRING" or "EF" => "Backend",

        "REACT" or "ANGULAR" or "VUE" or "NEXTJS" or "TYPESCRIPT" or "JAVASCRIPT" => "Frontend",

        "SQL" or "MONGODB" or "REDIS" or "ELASTICSEARCH" or "CASSANDRA"
            or "DYNAMODB" or "COUCHBASE" => "Database",

        "DOCKER" or "KUBERNETES" or "HELM" or "TERRAFORM" => "DevOps",

        "MICROSERVICES" or "HEXAGONAL" or "DDD" or "EVENTDRIVEN"
            or "CQRS" or "GRAPHQL" or "GRPC" or "OPENAPI" => "Architecture",

        "DATADOG" or "PROMETHEUS" or "GRAFANA" or "OPENTELEMETRY"
            or "SPLUNK" or "NEWRELIC" or "SENTRY" => "Observability",

        "OAUTH" or "JWT" or "KEYCLOAK" => "Security",

        "KAFKA" or "RABBITMQ" or "SERVICEBUS" or "PUBSUB" => "Messaging",

        _ => "Other"
    };

    private static DateTime GetWeekStart(DateTime date)
    {
        var utc = date.ToUniversalTime();
        var diff = (int)utc.DayOfWeek - (int)DayOfWeek.Monday;
        if (diff < 0) diff += 7;
        return utc.Date.AddDays(-diff);
    }

    // Helpers for aggregation
    private sealed class TechStats
    {
        public int TotalMentions;
        public int WeeklyMentions;
        public int PrevWeekMentions;
        public DateTime FirstSeen;
        public DateTime LastSeen;
        public HashSet<string> Industries = [];
        public HashSet<int> ClusterIds = [];
        public double TotalOppScore;
        public double TotalUrgency;
        public double TotalLeadScore;
    }

    private sealed class PairStats
    {
        public int Count;
        public DateTime LastSeen;
        public Dictionary<string, int> IndustryCounts = [];
        public double TotalOppScore;
    }
}

// Extension to create HashSet from IEnumerable with a key selector
file static class HashSetExtensions
{
    public static HashSet<T> ToHashSet<TSource, T>(
        this IEnumerable<TSource> source,
        Func<TSource, T> keySelector)
    {
        var set = new HashSet<T>();
        foreach (var item in source) set.Add(keySelector(item));
        return set;
    }
}
