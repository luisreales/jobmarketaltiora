using backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace backend.Infrastructure.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken = default)
    {
        await EnsureJobOfferSchemaAsync(dbContext, cancellationToken);

        if (!await dbContext.ProviderSessions.AnyAsync(cancellationToken))
        {
            dbContext.ProviderSessions.Add(new ProviderSession
            {
                Provider = "linkedin",
                Username = "not-logged-in",
                IsAuthenticated = false
            });
        }

        if (!await dbContext.JobOffers.AnyAsync(cancellationToken))
        {
            var now = DateTime.UtcNow;
            dbContext.JobOffers.AddRange(
                new JobOffer
                {
                    ExternalId = "seed-001",
                    Title = "Senior .NET Developer",
                    Company = "Contoso Tech",
                    Location = "Remote - LATAM",
                    Description = "Build backend services with .NET 9, PostgreSQL and Azure.",
                    Url = "https://www.linkedin.com/jobs/view/seed-001",
                    Contact = "recruiter@contoso.example",
                    SalaryRange = "USD 4500 - 6500",
                    PublishedAt = now.AddDays(-2),
                    Seniority = "Senior",
                    ContractType = "Full-time",
                    Source = "linkedin",
                    SearchTerm = ".NET",
                    CapturedAt = now.AddHours(-1),
                    MetadataJson = "{\"seed\":true}"
                },
                new JobOffer
                {
                    ExternalId = "seed-002",
                    Title = "Backend Engineer C#",
                    Company = "Fabrikam Data",
                    Location = "Bogota, Colombia",
                    Description = "Design APIs and data pipelines with C#, .NET and SQL.",
                    Url = "https://www.linkedin.com/jobs/view/seed-002",
                    Contact = null,
                    SalaryRange = null,
                    PublishedAt = now.AddDays(-1),
                    Seniority = "Mid-Senior level",
                    ContractType = "Contract",
                    Source = "linkedin",
                    SearchTerm = "C# backend",
                    CapturedAt = now.AddMinutes(-20),
                    MetadataJson = "{\"seed\":true}"
                });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await SeedIntelligenceAsync(dbContext, cancellationToken);
    }

    private static async Task SeedIntelligenceAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // ── Market Clusters ──────────────────────────────────────────────────────
        if (!await db.MarketClusters.AnyAsync(ct))
        {
            db.MarketClusters.AddRange(
                new MarketCluster
                {
                    ClusterKey                    = "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4a1b2c3d4e5f6a1b2c3d4e5f600001",
                    Label                         = "Fintech migrating .NET to Azure (Direct Clients)",
                    PainCategory                  = "CloudModernization",
                    NormalizedTechStack           = "AZURE, NET, SQL",
                    Industry                      = "Fintech",
                    CompanyType                   = "DirectClient",
                    JobCount                      = 8,
                    DirectClientCount             = 7,
                    DirectClientRatio             = 0.875,
                    AvgOpportunityScore           = 72,
                    AvgUrgencyScore               = 7.2,
                    GrowthRate                    = 18.5,
                    BuyingPowerScore              = 100,
                    PainSpecificityScore          = 100,
                    EaseOfSaleScore               = 79,
                    BlueOceanScore                = 74.2,
                    RoiRank                       = 1,
                    OpportunityType               = "MVPProduct",
                    IsActionable                  = true,
                    RecommendedStrategy           = "Build MVP + Validate + Ads",
                    PriorityScore                 = 76,
                    EstimatedTam                  = 180,
                    BuyingIntentScore             = 71.5,
                    EnterpriseComplexity          = 55,
                    HiringVelocity                = 62,
                    DeliveryFeasibility           = 78,
                    SalesFriction                 = 22,
                    RevenuePotential              = 82,
                    PriorityScoreV2               = 78,
                    RecommendedServiceModel       = "Fixed-Price Sprint",
                    SalesAngle                    = "Reduce cloud migration risk and cut infrastructure costs by 30% in 90 days.",
                    WhyNow                        = "Hiring velocity surged 18% — the team is expanding to meet a migration deadline.",
                    EstimatedCloseProbability     = 0.72,
                    EstimatedDealSizeUsd          = 28_000m,
                    LlmStatus                     = "completed",
                    LlmConfidence                 = 0.88,
                    SynthesizedPain               = "Fintech companies are stuck on legacy on-premise .NET monoliths they must migrate to Azure to meet compliance and scale, but lack internal expertise.",
                    SynthesizedBusinessOpportunity= "A fixed-price Azure migration sprint targeting .NET Fintech teams — $20k–$35k, 6–8 weeks, zero-risk guarantee.",
                    SynthesizedMvp                = "Migrate one microservice to Azure Container Apps with CI/CD, monitoring, and documentation included.",
                    SynthesizedLeadMessage        = "Hi [Name], your team is hiring 3 .NET Azure engineers — typically a $200k+/year commitment. We migrate one service in 6 weeks for a fixed $28k. Worth a 15-min call?",
                    MvpType                       = "MVPProduct",
                    EstimatedBuildDays            = 42,
                    SemanticGroupKey              = "cloud-migration-net",
                    FirstSeenAt                   = now.AddDays(-14),
                    LastUpdatedAt                 = now.AddDays(-1),
                    EngineVersion                 = "cluster-v2"
                },
                new MarketCluster
                {
                    ClusterKey                    = "b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4b2c3d4e5f6a1b2c3d4e5f6a1b200002",
                    Label                         = "AI/LLM integration in .NET SaaS products",
                    PainCategory                  = "AIAdoption",
                    NormalizedTechStack           = "NET, OPENAI, SEMANTICKERNEL",
                    Industry                      = "Tech",
                    CompanyType                   = "DirectClient",
                    JobCount                      = 12,
                    DirectClientCount             = 11,
                    DirectClientRatio             = 0.917,
                    AvgOpportunityScore           = 81,
                    AvgUrgencyScore               = 8.1,
                    GrowthRate                    = 42.0,
                    BuyingPowerScore              = 100,
                    PainSpecificityScore          = 100,
                    EaseOfSaleScore               = 85,
                    BlueOceanScore                = 83.6,
                    RoiRank                       = 1,
                    OpportunityType               = "MVPProduct",
                    IsActionable                  = true,
                    RecommendedStrategy           = "Build MVP + Validate + Ads",
                    PriorityScore                 = 88,
                    EstimatedTam                  = 350,
                    BuyingIntentScore             = 85.0,
                    EnterpriseComplexity          = 60,
                    HiringVelocity                = 78,
                    DeliveryFeasibility           = 74,
                    SalesFriction                 = 18,
                    RevenuePotential              = 91,
                    PriorityScoreV2               = 88,
                    RecommendedServiceModel       = "SaaS MVP",
                    SalesAngle                    = "Ship your first production AI feature in 3 weeks with Semantic Kernel + OpenAI — no AI team required.",
                    WhyNow                        = "AI hiring velocity jumped 42% — companies are racing to ship LLM features before competitors.",
                    EstimatedCloseProbability     = 0.78,
                    EstimatedDealSizeUsd          = 45_000m,
                    LlmStatus                     = "completed",
                    LlmConfidence                 = 0.92,
                    SynthesizedPain               = "SaaS companies know they need AI features but their .NET engineers lack LLM integration experience, causing them to miss market windows.",
                    SynthesizedBusinessOpportunity= "An AI integration sprint for .NET SaaS — $35k–$55k, 3 weeks, includes Semantic Kernel, OpenAI, RAG pipeline, and production deployment.",
                    SynthesizedMvp                = "One AI-powered feature (smart search, copilot, or document summarization) deployed to production with evaluation pipeline.",
                    SynthesizedLeadMessage        = "Hi [Name], your team is hiring 5 .NET engineers with 'AI experience required' — that's $500k+/year. We ship your first production AI feature in 3 weeks for $45k. 15-min call?",
                    MvpType                       = "MVPProduct",
                    EstimatedBuildDays            = 21,
                    SemanticGroupKey              = "ai-dotnet-integration",
                    FirstSeenAt                   = now.AddDays(-7),
                    LastUpdatedAt                 = now,
                    EngineVersion                 = "cluster-v2"
                },
                new MarketCluster
                {
                    ClusterKey                    = "c3d4e5f6a1b2c3d4e5f6a1b2c3d4c3d4e5f6a1b2c3d4e5f6a1b2c3d400003",
                    Label                         = "Data pipelines with Python/Kafka (Consulting agencies)",
                    PainCategory                  = "DataEngineering",
                    NormalizedTechStack           = "PYTHON, SQL, KAFKA",
                    Industry                      = "Unknown",
                    CompanyType                   = "Consulting",
                    JobCount                      = 5,
                    DirectClientCount             = 1,
                    DirectClientRatio             = 0.2,
                    AvgOpportunityScore           = 54,
                    AvgUrgencyScore               = 5.4,
                    GrowthRate                    = 6.0,
                    BuyingPowerScore              = 50,
                    PainSpecificityScore          = 100,
                    EaseOfSaleScore               = 31,
                    BlueOceanScore                = 52.1,
                    RoiRank                       = 3,
                    OpportunityType               = "QuickWin",
                    IsActionable                  = true,
                    RecommendedStrategy           = "Direct Outreach (manual)",
                    PriorityScore                 = 54,
                    EstimatedTam                  = 90,
                    BuyingIntentScore             = 48.0,
                    EnterpriseComplexity          = 42,
                    HiringVelocity                = 38,
                    DeliveryFeasibility           = 82,
                    SalesFriction                 = 55,
                    RevenuePotential              = 55,
                    PriorityScoreV2               = 56,
                    RecommendedServiceModel       = "Retainer",
                    SalesAngle                    = "Cut data pipeline delivery time by 40% with a reusable Python/Kafka framework.",
                    WhyNow                        = "The market is steady — agencies need to differentiate their data delivery offering.",
                    EstimatedCloseProbability     = 0.45,
                    EstimatedDealSizeUsd          = 12_000m,
                    LlmStatus                     = "pending",
                    FirstSeenAt                   = now.AddDays(-10),
                    LastUpdatedAt                 = now.AddDays(-3),
                    EngineVersion                 = "cluster-v2"
                },
                new MarketCluster
                {
                    ClusterKey                    = "d4e5f6a1b2c3d4e5f6a1b2c3d4e5d4e5f6a1b2c3d4e5f6a1b2c3d4e500004",
                    Label                         = "React/Next.js frontend modernization (E-commerce)",
                    PainCategory                  = "FrontendModernization",
                    NormalizedTechStack           = "REACT, NEXTJS, TYPESCRIPT",
                    Industry                      = "Ecommerce",
                    CompanyType                   = "DirectClient",
                    JobCount                      = 6,
                    DirectClientCount             = 5,
                    DirectClientRatio             = 0.833,
                    AvgOpportunityScore           = 63,
                    AvgUrgencyScore               = 6.5,
                    GrowthRate                    = 11.2,
                    BuyingPowerScore              = 85,
                    PainSpecificityScore          = 100,
                    EaseOfSaleScore               = 72,
                    BlueOceanScore                = 64.8,
                    RoiRank                       = 2,
                    OpportunityType               = "QuickWin",
                    IsActionable                  = true,
                    RecommendedStrategy           = "Direct Outreach (manual)",
                    PriorityScore                 = 66,
                    EstimatedTam                  = 120,
                    BuyingIntentScore             = 62.0,
                    EnterpriseComplexity          = 40,
                    HiringVelocity                = 52,
                    DeliveryFeasibility           = 88,
                    SalesFriction                 = 28,
                    RevenuePotential              = 70,
                    PriorityScoreV2               = 68,
                    RecommendedServiceModel       = "Fixed-Price Sprint",
                    SalesAngle                    = "Migrate your e-commerce frontend to Next.js 14 — improve Core Web Vitals by 40% in 4 weeks.",
                    WhyNow                        = "E-commerce teams are racing to adopt RSC for SEO performance before Q4 peak season.",
                    EstimatedCloseProbability     = 0.60,
                    EstimatedDealSizeUsd          = 18_000m,
                    LlmStatus                     = "completed",
                    LlmConfidence                 = 0.84,
                    SynthesizedPain               = "E-commerce companies on legacy React SPAs are losing SEO ranking and conversion rates to competitors shipping Next.js storefronts.",
                    SynthesizedBusinessOpportunity= "A Next.js migration sprint for e-commerce — $15k–$22k, 4 weeks, measurable Core Web Vitals improvement guaranteed.",
                    SynthesizedMvp                = "Migrate the product listing and checkout flow to Next.js App Router with SSR, Lighthouse score report included.",
                    SynthesizedLeadMessage        = "Hi [Name], your team is hiring 3 React engineers — Lighthouse score likely costing you 5-10% in conversions. We migrate your core flow to Next.js in 4 weeks for $18k. Quick call?",
                    MvpType                       = "QuickWin",
                    EstimatedBuildDays            = 28,
                    SemanticGroupKey              = "frontend-modernization",
                    FirstSeenAt                   = now.AddDays(-12),
                    LastUpdatedAt                 = now.AddDays(-2),
                    EngineVersion                 = "cluster-v2"
                });
            await db.SaveChangesAsync(ct);
        }

        // ── Commercial Strategies ────────────────────────────────────────────────
        if (!await db.CommercialStrategies.AnyAsync(ct))
        {
            db.CommercialStrategies.AddRange(
                new CommercialStrategy
                {
                    ProductId            = null,
                    ProductName          = "Azure Migration Sprint (.NET Fintech)",
                    CompanyContext       = "Fintech startup — 50 engineers, migrating .NET monolith to Azure for compliance",
                    RealBusinessProblem  = "Legacy .NET monolith blocks regulatory certification and prevents elastic scaling during trading peaks.",
                    FinancialImpact      = "On-premise infrastructure costs $180k/year; compliance delays cost $50k+/month in deferred revenue. Migration pays back in 4 months.",
                    MvpDefinition        = "Migrate the authentication and transaction modules to Azure Container Apps with Key Vault, CI/CD pipeline, and SLA monitoring dashboard.",
                    TargetBuyer          = "CTO or VP Engineering. Angle: reduce compliance risk and cut infra cost — not a tech upgrade, a business obligation.",
                    PricingStrategy      = "Fixed-price $28,000 sprint (6 weeks). Optional: $4,500/month retainer for ongoing cloud ops and security patching.",
                    OutreachMessage      = "Hi [Name],\n\nYour team is posting .NET + Azure roles at a pace that signals a major migration deadline. Hiring 3 engineers for 6 months = $210k+.\n\nWe migrate one critical module to Azure Container Apps in 6 weeks for a fixed $28k — includes CI/CD, Key Vault integration, and a production SLA dashboard.\n\nIf the first module works, the rest follows. If not, you owe us nothing.\n\n15-min call this week?",
                    GeneratedAt          = DateTime.UtcNow.AddDays(-3)
                },
                new CommercialStrategy
                {
                    ProductId            = null,
                    ProductName          = "AI Feature Sprint (Semantic Kernel + OpenAI)",
                    CompanyContext       = "B2B SaaS company shipping a project management tool — wants to add AI-powered features to compete with Notion AI",
                    RealBusinessProblem  = "SaaS product losing trials to AI-native competitors because it lacks smart automation, reducing conversion rate by ~20%.",
                    FinancialImpact      = "Adding one AI feature (e.g., smart summaries) recovers 8–12% trial conversion — worth $40k–$80k ARR per 1,000 monthly trials.",
                    MvpDefinition        = "Integrate Semantic Kernel + OpenAI to deliver one AI-powered feature: auto-generated project summaries with user feedback loop and token cost monitoring.",
                    TargetBuyer          = "CPO or Head of Product. Angle: ship an AI feature this sprint or lose the next 20% of trials to Notion AI — this is a churn problem, not a feature request.",
                    PricingStrategy      = "Fixed-price $45,000 (3 weeks). Includes production deployment, A/B test setup, evaluation pipeline, and a token cost dashboard.",
                    OutreachMessage      = "Hi [Name],\n\nYour product team is hiring 5 .NET engineers with 'LLM/AI experience required' — that's $500k+/year. And your roadmap is already 6 months behind Notion AI.\n\nWe ship one production AI feature with Semantic Kernel + OpenAI in 3 weeks for $45k — includes A/B test, eval pipeline, and cost dashboard.\n\nMost teams recoup it in 60 days via trial conversion lift.\n\nWorth 20 minutes?",
                    GeneratedAt          = DateTime.UtcNow.AddDays(-1)
                });
            await db.SaveChangesAsync(ct);
        }

        // ── MVP Requirements ─────────────────────────────────────────────────────
        if (!await db.MvpRequirements.AnyAsync(ct))
        {
            db.MvpRequirements.AddRange(
                new MvpRequirement
                {
                    ProductId            = null,
                    ProductName          = "Architecture Blueprint: .NET Microservices on Azure",
                    CompanyContext       = "Fintech startup migrating .NET 6 monolith to Azure Container Apps for PCI-DSS compliance",
                    ArchitectureStrategy = "Strangler Fig pattern — extract auth and transaction modules as containerized microservices first. Use Azure Service Bus for inter-service messaging. Keep monolith running in parallel until 3 core modules are migrated.",
                    RequiredTechStackJson= """["NET9","AzureContainerApps","AzureServiceBus","AzureKeyVault","AzureMonitor","Docker","GitHubActions","PostgreSQL","Redis"]""",
                    EstimatedTimelines   = "Week 1–2: Containerize auth module + CI/CD pipeline. Week 3–4: Migrate transaction module + Key Vault integration. Week 5–6: Load tests, SLA dashboard, documentation, handover.",
                    CoreFeaturesJson     = """["Containerized auth microservice with Azure AD B2C","Transaction module on ACA with auto-scaling","Azure Key Vault for secrets management","GitHub Actions CI/CD with staging environment","Azure Monitor dashboard with SLA alerts","Database migration scripts with rollback plan"]""",
                    GeneratedAt          = DateTime.UtcNow.AddDays(-3)
                },
                new MvpRequirement
                {
                    ProductId            = null,
                    ProductName          = "Technical Spec: AI Copilot with Semantic Kernel",
                    CompanyContext       = "B2B SaaS project management tool on .NET 8 + Angular, wants to add AI-powered project summaries",
                    ArchitectureStrategy = "Plugin-based Semantic Kernel integration. AI features behind a feature flag (LaunchDarkly). Streaming responses via SignalR. Token usage tracked per tenant in a separate AiUsage table for cost attribution.",
                    RequiredTechStackJson= """["NET9","SemanticKernel","OpenAIGPT4o","SignalR","LaunchDarkly","PostgreSQL","Redis","Docker","AzureContainerApps"]""",
                    EstimatedTimelines   = "Week 1: SK setup, OpenAI integration, streaming endpoint + unit tests. Week 2: Project summary feature, A/B test config, token cost dashboard. Week 3: Eval pipeline, load tests, production deploy, handover.",
                    CoreFeaturesJson     = """["Semantic Kernel plugin for project summarization","Streaming API endpoint with SignalR","Feature flag integration (LaunchDarkly)","Token cost tracking per tenant","A/B test framework for AI feature rollout","Evaluation pipeline with accuracy metrics","Production-ready deployment with monitoring"]""",
                    GeneratedAt          = DateTime.UtcNow.AddDays(-1)
                });
            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task EnsureJobOfferSchemaAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"JobOffers\" ADD COLUMN IF NOT EXISTS \"Category\" character varying(80) NOT NULL DEFAULT 'Unknown';",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"JobOffers\" ADD COLUMN IF NOT EXISTS \"OpportunityScore\" integer NOT NULL DEFAULT 0;",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"JobOffers\" ADD COLUMN IF NOT EXISTS \"IsConsultingCompany\" boolean NOT NULL DEFAULT false;",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"JobOffers\" ADD COLUMN IF NOT EXISTS \"CompanyType\" character varying(40) NOT NULL DEFAULT 'Unknown';",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"JobOffers\" ADD COLUMN IF NOT EXISTS \"IsProcessed\" boolean NOT NULL DEFAULT false;",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"JobOffers\" ADD COLUMN IF NOT EXISTS \"ProcessedAt\" timestamp with time zone NULL;",
            cancellationToken);
    }
}
