# Altiora Platform — Architecture Review

> Full high-level description of every module, service, and data flow.
> Stack: .NET 9 · EF Core · PostgreSQL · Semantic Kernel · Angular 20 · Node.js · Docker

---

## Table of Contents

1. [System Overview](#1-system-overview)
2. [Infrastructure — Docker Compose](#2-infrastructure--docker-compose)
3. [Data Model — Entities & Relationships](#3-data-model--entities--relationships)
4. [Backend — Module Map](#4-backend--module-map)
5. [Scraping Layer](#5-scraping-layer)
6. [AI Enrichment Pipeline](#6-ai-enrichment-pipeline)
7. [Clustering Intelligence Pipeline](#7-clustering-intelligence-pipeline)
8. [Technology Intelligence Module](#8-technology-intelligence-module)
9. [Company & Revenue Intelligence](#9-company--revenue-intelligence)
10. [LLM / Semantic Kernel Layer](#10-llm--semantic-kernel-layer)
11. [REST API — Controllers](#11-rest-api--controllers)
12. [Frontend — Angular 20](#12-frontend--angular-20)
13. [Full Data Flow (end-to-end)](#13-full-data-flow-end-to-end)
14. [Scoring Formulas Reference](#14-scoring-formulas-reference)
15. [Key Design Decisions](#15-key-design-decisions)

---

## 1. System Overview

Altiora is a **market intelligence platform for B2B software agencies**. It answers three questions:

1. **What problems are companies hiring to solve?** (job scraping + AI enrichment)
2. **Which of those problems represent a real sales opportunity?** (clustering + scoring)
3. **What product or service should we offer?** (LLM synthesis + product generation)

The platform ingests job postings from LinkedIn, Indeed, and Upwork, extracts pain points and tech stacks with AI, clusters them into market segments, scores their commercial potential, and generates ready-to-pitch service packages.

```
┌─────────────────────────────────────────────────────────────────────┐
│                          Altiora Platform                           │
│                                                                     │
│  ┌──────────┐   ┌──────────┐   ┌──────────┐   ┌─────────────────┐  │
│  │ Scraping │──▶│   Jobs   │──▶│  AI Enr. │──▶│   Clustering    │  │
│  │  Layer   │   │  (DB)    │   │ Pipeline │   │    Pipeline     │  │
│  └──────────┘   └──────────┘   └──────────┘   └────────┬────────┘  │
│                                                         │           │
│  ┌─────────────┐   ┌──────────┐   ┌─────────────────────▼────────┐ │
│  │  Frontend   │◀──│  REST    │◀──│  Opportunities / Products    │ │
│  │  Angular 20 │   │   API    │   │  LLM Synthesis               │ │
│  └─────────────┘   └──────────┘   └──────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 2. Infrastructure — Docker Compose

Four containers, one optional:

| Container | Image | Port | Role |
|-----------|-------|------|------|
| `jobmarketaltiora-postgres` | postgres:16 | 5432 | Primary database |
| `jobmarketaltiora-backend` | .NET 9 (custom) | 8080 | REST API + all background workers |
| `jobmarketaltiora-frontend` | nginx (Angular build) | 4200 | SPA served via nginx |
| `scraper-api` | node:20-slim | 3000 | Upwork Puppeteer scraper — **profile: scraper, runs locally on Mac** |

### Networking note

The backend container reaches the local Mac's port 3000 via `host.docker.internal:3000` (configured with `extra_hosts: host.docker.internal:host-gateway`). This is required because Upwork's Cloudflare protection blocks all headless browsers inside containers — the scraper-api must run natively on macOS with a visible Chrome window.

### Configuration

Secrets and feature flags are injected via environment variables:
- `ConnectionStrings__DefaultConnection` — PostgreSQL
- `Jobs__Credentials__Upwork__*` — Upwork login
- `SemanticKernel__*` — OpenAI/Azure OpenAI API keys and model names
- `UpworkScraper__BaseUrl` — points to `http://host.docker.internal:3000`

---

## 3. Data Model — Entities & Relationships

```
JobOffer ──────────────────────────────────────────┐
    │                                               │
    ▼                                               │
JobInsight (AI enrichment)                         │
    │  PainCategory, TechTokensJson                 │
    │  LeadScore, OpportunityScore                  │
    │  EmbeddingVectorJson (semantic)               │
    │                                               │
    ▼                                               │
MarketCluster (grouped by pain+tech+industry)      │
    │  BlueOceanScore, PriorityScoreV2              │
    │  TAM, BuyingIntent, SalesAngle                │
    │  LLM synthesis fields                         │
    │                                               │
    ├──▶ Opportunity ──▶ OpportunityIdea            │
    │                                               │
    └──▶ ProductSuggestion                         │
              │                                    │
              ├──▶ CommercialStrategy              │
              └──▶ MvpRequirement                 │
                                                   │
Technology (canonical name, lifecycle, momentum)   │
TechnologyRelationship (co-occurrence graph)        │
TechnologyTrendSnapshot (weekly time series)        │
                                                   │
CompanyProfile (prospect scoring)                  │
                                                   │
AiPromptTemplate (versioned LLM prompts in DB)     │
AiPromptLog (every LLM call logged)                │
                                                   │
ProviderSession (LinkedIn/Upwork auth tokens)      │
                                                   │
AppSumoProduct / AppSumoReview / AppSumoScrapeRun ─┘
```

### Key fields per entity

**JobOffer** — raw job posting: `Title`, `Company`, `Description`, `Url`, `ExternalId` (SHA256 of URL, dedup key), `Source` (linkedin/upwork/indeed), `IsProcessed`

**JobInsight** — AI analysis of one job: `MainPainPoint`, `PainCategory`, `TechTop3`, `TechTokensJson`, `Industry`, `CompanyType`, `LeadScore`, `UrgencyScore`, `OpportunityScore`, `SuggestedSolution`, `EmbeddingVectorJson`

**MarketCluster** — group of similar insights: `ClusterKey` (SHA256), `PainCategory`, `NormalizedTechStack`, `Industry`, `JobCount`, `BlueOceanScore`, `PriorityScoreV2`, `IsActionable`, `OpportunityType`, LLM synthesis output fields

**ProductSuggestion** — sellable service: `Name`, `Description`, `Category`, `DeliveryModel`, `PricingModel`, LLM-synthesized implementation plan

---

## 4. Backend — Module Map

```
backend/
├── Controllers/          15 REST controllers
├── Application/
│   ├── Interfaces/       All service contracts (IJobOrchestrator, IClusterEngine, …)
│   └── Contracts/        Request/response DTOs (records)
├── Domain/
│   └── Entities/         20 EF Core entities
└── Infrastructure/
    ├── Data/             ApplicationDbContext + DatabaseSeeder
    ├── Repositories/     JobRepository, AppSumoRepository
    ├── Services/         All business logic implementations
    ├── Providers/        LinkedIn, Indeed, Upwork scrapers
    └── Scraping/         AppSumoScraperService
```

### Dependency Injection registration order (Program.cs)

1. EF Core + PostgreSQL
2. Repositories (scoped)
3. Singletons: `TechCanonicalizer`, `IndustryClassifier`, `SemanticKernelProvider`, `IBrowserPool`
4. Scraping: `PlaywrightScraper`, `SessionManager`, `LinkedInAuthService`, providers, `JobOrchestrator`
5. AI enrichment: `JobPreprocessorService`, `JobDescriptionCleanerService`, `LeadScoringService`, `RuleBasedAiEnrichmentService`, `MarketIntelligenceService`
6. Clustering: `ClusterEngine`, `SemanticClusterEngine`, `DecisionEngine`, `OpportunityEngineV2`, `ProductGeneratorService`
7. Synthesis: `ClusterSynthesisService`, `ProductSynthesisService`
8. Intelligence: `TechnologyIntelligenceService`, `CompanyIntelligenceService`
9. AppSumo: `AppSumoScraperService`, `AppSumoOrchestratorService`
10. Background workers: `JobsAutomationHostedService`, `JobPostProcessingHostedService`, `MarketIntelligenceHostedService`, `ClusteringHostedService`

---

## 5. Scraping Layer

### Providers

| Provider | Technology | Auth mechanism |
|----------|-----------|----------------|
| LinkedIn | Playwright (headless Chromium) | Session cookies persisted to `playwright-state/linkedin.json` |
| Indeed | Playwright | Session cookies |
| Upwork | Puppeteer Real Browser (Node.js, external process) | Session cookies + token persisted to `.upwork-session.json` |

### Upwork special flow

Upwork uses Cloudflare Turnstile which blocks all headless automation inside Docker. The flow is:

```
Frontend/Postman
    │
    ▼
POST /api/auth/login  { provider:"upwork", showBrowser:true }
    │
    ▼
AuthController → JobOrchestrator.LoginAsync()
    │
    ▼
UpworkProvider → UpworkScraperClient (HTTP to localhost:3000)
    │
    ▼
scraper-api (node server.js running on Mac)
    │  opens real Chrome window
    ▼
Upwork.com — user completes login/2FA in visible window
    │
    ▼
Session saved to .upwork-session.json (12h validity)
```

Once authenticated, scraping calls `POST /upwork/scrape` with `{query, limit, startPage, endPage}`. The scraper navigates pages sequentially (10 jobs/page), re-applies cookies before each page, and returns raw job data.

### Deduplication

`JobRepository.UpsertRangeAsync` deduplicates on `(ExternalId, Source)` — ExternalId is SHA256 of the job URL. Existing records are updated (resetting `IsProcessed=false` to trigger re-enrichment), new records are inserted. `savedCount` only counts net new inserts.

---

## 6. AI Enrichment Pipeline

Triggered by `MarketIntelligenceHostedService` (or manually via API). Processes unprocessed `JobOffer` records in batches.

```
JobOffer (IsProcessed=false)
    │
    ▼
JobPreprocessorService
    ├── RemoveFluffTail() — strips boilerplate (benefits, EEO, etc.)
    ├── DetectConsultingSignals() — flags staffing agencies
    └── Truncate to 4000 chars

    │
    ▼
JobDescriptionCleanerService
    ├── ExtractTechnicalSections() — keeps tech/architecture paragraphs
    ├── RemoveCorporateFluff() — extended blacklist
    └── NormalizeWhitespace()

    │
    ▼
RuleBasedAiEnrichmentService (fast, no LLM cost)
    ├── TechCanonicalizer.ExtractTokens() — maps text to canonical tech names
    │   (NET, REACT, AZURE, OPENAI, LANGCHAIN, RAG, VECTORDB, …)
    ├── IndustryClassifier — finance/healthcare/retail/saas/…
    ├── PainCategoryDetector — migration/modernization/AI adoption/…
    └── CompanyTypeClassifier — directClient/consultingFirm/staffingAgency

    │
    ▼
MarketIntelligenceService (LLM call via Semantic Kernel)
    ├── System prompt: "Extract structured B2B intelligence from this job"
    ├── Output JSON: mainPainPoint, painCategory, techTop3, industry,
    │   companyType, suggestedSolution, urgencyScore, opportunityScore
    └── Logs to AiPromptLog

    │
    ▼
LeadScoringService
    └── LeadScore = (OpportunityScore×0.4 + UrgencyScore×5×0.2
                    + DirectClientBonus(20) + RecencyBoost×0.2)
                   × ConsultingPenalty(0.70 if consulting)

    │
    ▼
JobInsight saved → JobOffer.IsProcessed = true
```

### TechCanonicalizer

A singleton dictionary mapping 100+ text patterns to canonical tokens. Order-sensitive — longer/more-specific patterns first. Categories covered:

- **AI/ML**: OPENAI, LANGCHAIN, RAG, VECTORDB, AIAGENT, SEMANTICKERNEL, PYTORCH, TENSORFLOW, LLAMA, HUGGINGFACE, COPILOT
- **Backend**: NET, CSHARP, JAVA, PYTHON, GO, RUST, NODE, SPRING, FASTAPI
- **Frontend**: REACT, ANGULAR, VUE, NEXTJS, TYPESCRIPT
- **Cloud**: AZURE, AWS, GCP
- **Database**: SQL, MONGODB, REDIS, ELASTICSEARCH, DYNAMODB
- **DevOps**: DOCKER, KUBERNETES, TERRAFORM, HELM
- **Architecture**: MICROSERVICES, DDD, CQRS, EVENTDRIVEN, HEXAGONAL
- **Messaging**: KAFKA, RABBITMQ, SERVICEBUS

---

## 7. Clustering Intelligence Pipeline

Runs every 30 minutes via `ClusteringHostedService` (configurable via `Jobs:Clustering:IntervalSeconds`). Only executes if new `JobInsight` records have been processed since the last run.

```
Stage 0 — SemanticClusterEngine.GenerateEmbeddingsAsync()
    └── Generates float[] embeddings for MainPainPoint+PainCategory+SuggestedSolution
        via SK ITextEmbeddingGenerationService. Stored as JSON in EmbeddingVectorJson.
        Skipped if SK not configured.

Stage 1 — ClusterEngine.RebuildClustersAsync()
    ├── Groups insights by SHA256(PainCategory|TechTop3|Industry|CompanyType)
    ├── Creates/updates MarketCluster records
    └── BlueOceanScore v2:
        volume(0.30) + growth(0.20) + directRatio(0.20)
        + urgency(0.10) + buyingPower(0.10) + easeOfSale(0.10)

Stage 2 — DecisionEngine.EvaluateClustersAsync()
    ├── Sets OpportunityType: MVPProduct / QuickWin / Consulting / Niche
    └── Sets IsActionable flag (minimum thresholds: score, job count, direct clients)

Stage 2b — SemanticClusterEngine.AssignSemanticGroupsAsync()
    └── Computes cosine similarity between cluster centroids.
        Merges clusters with similarity >= 0.82 into SemanticGroupKey.
        Additive — does not break existing SHA256 clusters.

Stage 3 — OpportunityEngineV2.EnrichClustersAsync()
    ├── PriorityScoreV2 = BlueOcean(0.35) + BuyingIntent(0.25)
    │                   + Urgency(0.20) + DirectRatio(0.10) + Growth(0.10)
    ├── EstimatedTam — industry+pain lookup table
    ├── BuyingIntent = DirectRatio×40 + Urgency×30 + Growth×30
    ├── SalesAngle — rule-based text (no LLM)
    ├── WhyNow — rule-based urgency statement
    ├── RecommendedServiceModel — Consulting / SaaS MVP / Fixed-Price Sprint
    └── Applies tech lifecycle momentum boost from Technology table

Stage 4 — ProductGeneratorService.GenerateProductsAsync()
    └── Rule-based consolidation: creates/updates ProductSuggestion from
        actionable clusters. Sets name, category, pricing model from rules.

Stage 5 — ClusterSynthesisService.SynthesizePendingClustersAsync()
    ├── Picks up to 5 actionable clusters without LLM synthesis yet
    ├── LLM call: "Act as a B2B Strategy Director. Analyze these job postings."
    └── Output JSON: pain, businessOpportunity, mvp, leadMessage, confidence

Stage 6 — TechnologyIntelligenceService.RebuildAsync()
    └── Recomputes tech momentum, growth rates, lifecycle stages (see §8)

Stage 7 — CompanyIntelligenceService.RebuildAsync()
    └── Updates CompanyProfile prospect scores (see §9)
```

---

## 8. Technology Intelligence Module

Maintains a real-time view of which technologies are growing, declining, or emerging based on job posting frequency.

### Data sources

Reads from existing `JobInsight.TechTokensJson` — no new scraping needed. Runs on every clustering cycle.

### RebuildAsync algorithm

1. Load all `JobInsight` records with their `CapturedAt`, `OpportunityScore`, `Industry`, `ClusterId`
2. Re-extract canonical tokens per insight using `TechCanonicalizer`
3. Aggregate per token: `totalMentions`, `weeklyMentions` (last 7d), `prevWeekMentions` (7–14d ago), `firstSeen`, `lastSeen`, distinct industries and clusters
4. Compute scores:

| Metric | Formula |
|--------|---------|
| GrowthRate | `(week0 − week1) / max(week1, 1) × 100` |
| MomentumScore | `clamp(GrowthRate, −100, 100)` |
| DemandScore | `min(100, log1p(mentions) / log1p(200) × 100)` |
| CompetitionScore | `min(100, clusterCount / 15.0 × 100)` |
| OpportunityScore | `Demand×0.4 + (100−Competition)×0.3 + AvgOpportunity×0.3` |
| EmergingScore | `recencyFactor×0.5 + max(0, momentum)×0.5` |

5. **LifecycleStage** classification (priority order):
   - `Emerging` — first seen < 60 days ago AND momentum > 5
   - `Growing` — momentum > 10
   - `Declining` — momentum < −15 AND mentions > 5
   - `Legacy` — growth rate < −30% AND mentions > 10
   - `Mature` — everything else

6. Upsert `Technology` records, upsert `TechnologyRelationship` co-occurrence pairs (min 2 co-occurrences), append `TechnologyTrendSnapshot` weekly rows (append-only time series)

### Frontend pages

| Route | Content |
|-------|---------|
| `/technologies` | Sortable table: all techs with lifecycle badge, growth %, demand score |
| `/trends` | 4 sections: Fastest Growing, Emerging, Declining, AI Adoption Wave |
| `/stack-graph` | D3.js force graph — nodes = techs, edges = co-occurrence; click a node for details |

---

## 9. Company & Revenue Intelligence

### CompanyIntelligenceService

Builds `CompanyProfile` records from companies seen in `JobOffer.Company`:

- **ProspectScore** — weighted: DirectClientRatio, tech stack sophistication, hiring velocity, industry alignment, urgency signals
- **TechStack** — aggregated canonical tokens from all their postings
- **HiringVelocity** — job postings per day, normalized
- **EstimatedRevenue** — rule-based range from company size signals in job descriptions

### RevenueController

Aggregates commercial potential across the pipeline:

- Total addressable market per industry segment
- Revenue potential per cluster (EstimatedTam × directRatio × BuyingIntent)
- Pipeline conversion funnel: jobs → insights → clusters → opportunities → products

---

## 10. LLM / Semantic Kernel Layer

### SemanticKernelProvider

Singleton. Initializes a Kernel with:
- **Chat completion** — OpenAI GPT-4o or Azure OpenAI
- **Text embeddings** — `text-embedding-3-small` (optional, for semantic clustering)

All LLM calls go through this provider. If SK is not configured, enrichment falls back to rule-based only.

### Prompt management

Prompts are stored in the `AiPromptTemplates` table (not hardcoded). This allows:
- Updating prompts from the UI (`/prompt-ai`) without redeploying
- Version tracking
- A/B testing different prompt strategies

Every LLM call is logged to `AiPromptLog` with: template key, model, input tokens, output tokens, latency, raw response, error if any.

### Services using LLM

| Service | Purpose | Max calls/cycle |
|---------|---------|-----------------|
| MarketIntelligenceService | Job enrichment | 1 per unprocessed job |
| ClusterSynthesisService | Cluster narrative + MVP | 5 per clustering cycle |
| ProductSynthesisService | Product implementation plan | On-demand |

---

## 11. REST API — Controllers

| Controller | Base route | Key endpoints |
|-----------|-----------|---------------|
| AuthController | `/api/auth` | POST `/login`, GET `/status/{provider}`, POST `/logout/{provider}` |
| JobsController | `/api/jobs` | POST `/search/scrape`, GET `/jobs/query`, POST `/jobs/purge`, GET `/jobs/quality` |
| MarketClusterController | `/api/clusters` | GET (list/detail), POST `/synthesize/{id}` |
| OpportunityController | `/api/opportunities` | Full CRUD |
| ProductController | `/api/products` | Full CRUD + funnel state |
| TechnologiesController | `/api/technologies` | GET `/trending`, `/emerging`, `/declining`, `/ai`, `/graph`, POST `/rebuild` |
| CommercialStrategyController | `/api/commercial-strategies` | Full CRUD |
| MvpRequirementController | `/api/mvp-requirements` | Full CRUD |
| CompaniesController | `/api/companies` | GET list/detail, POST `/rebuild` |
| RevenueController | `/api/revenue` | GET analytics aggregates |
| AppSumoController | `/api/appsumo` | POST `/scrape/start`, GET `/runs`, GET `/stats` |
| AiObservabilityController | `/api/ai-audit` | GET logs, GET templates, POST run-now |
| MarketIntelligenceController | `/api/market-intelligence` | GET status, POST trigger |
| SemanticKernelController | `/api/sk` | GET health |
| OpportunityIdeaController | `/api/opportunity-ideas` | Full CRUD idea vault |

---

## 12. Frontend — Angular 20

### Architecture

Standalone components, lazy-loaded routes, signal-free (uses RxJS Observables). OnPush change detection on heavy pages.

### Navigation structure (app.ts sidebar)

```
Core
  ├── /jobs          — Job search & filtering
  ├── /opportunities — Opportunity pipeline
  └── /products      — Product catalog

Intelligence
  ├── /intelligence  — Market Intelligence worker status
  ├── /clusters      — Cluster browser + LLM synthesis trigger
  ├── /semantic-groups — Semantic cluster view
  ├── /technologies  — Tech catalog + lifecycle
  ├── /trends        — Tech trend dashboard
  ├── /stack-graph   — D3 force graph
  ├── /revenue       — Revenue analytics
  └── /companies     — Prospect company list

Strategy
  ├── /commercial-strategies — B2B pricing & messaging
  └── /mvp-requirements      — Technical MVP specs

Ideas
  └── /opportunity-ideas     — Idea vault

Tools
  ├── /scraping   — Job scraping control + session status + data quality
  ├── /prompt-ai  — Prompt template editor
  ├── /ai-audit   — LLM call logs & worker status
  └── /appsumo    — AppSumo complaint scraper
```

### Key frontend services

| Service | Calls |
|---------|-------|
| ScrapingService | `/api/jobs/search/scrape`, `/api/jobs/jobs/quality`, `/api/jobs/jobs/purge` |
| AuthService | `/api/auth/status/{provider}`, `/api/auth/login`, `/api/auth/logout/{provider}` |
| ClusterService | `/api/clusters` |
| TechnologyService | `/api/technologies/*` |
| PromptAiService | `/api/ai-audit/templates` |
| AiAuditService | `/api/ai-audit/*` |
| AppSumoService | `/api/appsumo/*` |

### /scraping page

The most complex page in the app. Sections:
1. **Provider Session Status bar** — LinkedIn and Upwork cards; shows Active/Offline, last login, expiry, step-by-step fix instructions when offline
2. **Provider scraping cards** — LinkedIn (with start/end page), Upwork (with pages selector), Multi-provider
3. **Data Quality panel** — DB health stats (total, duplicates, stale), Purge button with dry-run mode
4. **AppSumo Complaint Scraper** — category slug, max products, dry run, run history table
5. **AI Analysis section** — manual trigger for the enrichment worker

---

## 13. Full Data Flow (end-to-end)

```
1. SCRAPE
   User sets query + pages in /scraping → clicks Run
   → POST /api/jobs/search/scrape/upwork/login-and-scrape
   → UpworkProvider → scraper-api (Node.js, local Mac)
   → Puppeteer navigates pages 1..N, extracts 10 cards/page
   → JobRepository.UpsertRangeAsync() — dedup by SHA256(url)
   → New jobs saved with IsProcessed=false

2. ENRICH  (MarketIntelligenceHostedService, ~every 5 min)
   → Picks unprocessed JobOffers in batches
   → Clean description → TechCanonicalizer → IndustryClassifier
   → LLM call → JobInsight created with pain, tech, urgency, scores
   → LeadScore computed → JobOffer.IsProcessed=true

3. CLUSTER  (ClusteringHostedService, every 30 min)
   → Stage 1: SHA256 grouping → MarketCluster upserted
   → Stage 2: DecisionEngine → IsActionable, OpportunityType
   → Stage 3: OpportunityEngineV2 → TAM, BuyingIntent, PriorityScoreV2
   → Stage 4: ProductGeneratorService → ProductSuggestion (rules)
   → Stage 5: ClusterSynthesisService → LLM narrative (5/cycle)
   → Stage 6: TechnologyIntelligenceService → momentum scores
   → Stage 7: CompanyIntelligenceService → prospect scores

4. REVIEW  (User in /clusters or /opportunities)
   → Browse clusters sorted by PriorityScoreV2
   → Trigger on-demand LLM synthesis for a specific cluster
   → Convert cluster to Opportunity → attach ProductSuggestion
   → View CommercialStrategy, MvpRequirement, LeadMessage

5. PROSPECT  (User in /companies or /revenue)
   → See which companies match the cluster
   → Revenue analytics: pipeline value, TAM coverage
   → Export or contact via sales playbook

6. ITERATE
   → Edit prompt templates in /prompt-ai
   → Re-run enrichment → clusters refresh with new signals
   → Monitor LLM usage in /ai-audit
```

---

## 14. Scoring Formulas Reference

### LeadScore (JobInsight level)

```
base     = OpportunityScore × 0.40
         + UrgencyScore × 5 × 0.20
         + DirectClientBonus(20 if direct client)
         + RecencyBoost × 0.20

recency  = 100 if age ≤ 7d
         = linear decay to 40 at 30d
         = linear decay to 5 at 90d

LeadScore = base × ConsultingPenalty(0.70 if consulting firm)
```

### BlueOceanScore (MarketCluster level)

```
BlueOceanScore = volume(0.30) + growth(0.20) + directRatio(0.20)
               + urgency(0.10) + buyingPower(0.10) + easeOfSale(0.10)
```

### PriorityScoreV2 (after OpportunityEngineV2)

```
PriorityScoreV2 = BlueOcean(0.35) + BuyingIntent(0.25)
                + Urgency(0.20) + DirectRatio(0.10) + Growth(0.10)

BuyingIntent = DirectRatio×40 + Urgency×30 + GrowthRate×30
```

### Technology scores

```
GrowthRate    = (week0 − week1) / max(week1, 1) × 100
MomentumScore = clamp(GrowthRate, −100, 100)
DemandScore   = min(100, log1p(mentions) / log1p(200) × 100)
OpportunityScore = Demand×0.4 + (100−Competition)×0.3 + AvgJobOppScore×0.3
EmergingScore = recencyFactor×0.5 + max(0, Momentum)×0.5
```

---

## 15. Key Design Decisions

### Why SHA256 clustering (not pure semantic)?

SHA256 on `(PainCategory|TechTop3|Industry|CompanyType)` is **deterministic and cheap** — the same cluster key always maps to the same segment regardless of when it runs. Semantic clustering is additive on top: it can merge clusters that SHA256 would keep separate, but it never breaks existing ones. This means you can always trace a cluster back to its exact defining attributes.

### Why run scraper-api outside Docker?

Cloudflare Turnstile fingerprints the browser environment. Inside any container (even with Chromium + `puppeteer-real-browser`), it serves a challenge page. A real macOS Chrome process with a visible window bypasses the fingerprint check. The `showBrowser: true` flag propagates all the way from the API request through to the Node.js session creation.

### Why store prompts in the database?

LLM prompts are part of the product logic. Keeping them in the DB means: (1) you can tune them from `/prompt-ai` without a deployment, (2) every change is versioned, (3) the `market-job-analysis` key can be swapped at scrape time to test different extraction strategies on the same data.

### Why UpsertRangeAsync resets IsProcessed?

When a job is re-scraped (same URL, newer `CapturedAt`), the description may have changed (salary added, requirements updated). Resetting `IsProcessed=false` queues it for re-enrichment, which keeps the `JobInsight` fresh without manual intervention.

### AppSumo integration

AppSumo reviews (1–3 taco ratings = unhappy customers) are a **competitor intelligence signal** — they reveal what problems SaaS products fail to solve. These feed into the Idea Vault as potential product opportunities orthogonal to the job posting data.
