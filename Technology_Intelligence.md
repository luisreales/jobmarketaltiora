# TASK — Design and Implement Technology Intelligence Module for Engine Altiora v2

You are acting as Principal AI Architect and Market Intelligence Engineer for Engine Altiora v2.

Your objective is to evolve Altiora from:
“job + opportunity analyzer”
into:
“AI-powered Technology Market Intelligence Platform”.

You must analyze the existing architecture and implement a NEW module called:

# TECHNOLOGY INTELLIGENCE

This module must transform raw job market data into:

* technology trends
* stack intelligence
* emerging tech detection
* market shifts
* SaaS opportunity signals
* AI adoption analytics
* technology relationship graphs

IMPORTANT:
Do NOT replace existing architecture.
This is an additive intelligence layer.

---

# CURRENT SYSTEM CONTEXT

Existing pipeline:

Stage 0 → Semantic Embeddings
Stage 1 → SHA256 Clustering
Stage 2 → Decision Engine
Stage 2b → Semantic Groups
Stage 3 → Opportunity Engine V2
Stage 4 → Product Generator
Stage 5 → LLM Synthesis

Existing entities:

* JobOffer
* JobInsight
* MarketCluster
* ProductSuggestion
* Opportunities
* Semantic Groups

Existing services:

* TechCanonicalizer
* IndustryClassifier
* LeadScoringService
* ClusterEngine
* SemanticClusterEngine
* OpportunityEngineV2
* ProductGeneratorService
* ClusterSynthesisService

Current UI already has:

* Jobs
* Opportunities
* Products
* Clusters
* Semantic Groups
* Commercial Strategy
* MVP Requirements
* AI Audit
* Prompt AI
* Scraping
* AppSumo

There is currently NO dedicated technology analytics module.

---

# OBJECTIVE

Create a full Technology Intelligence layer capable of answering:

* Which technologies are growing?
* Which stacks are declining?
* Which technologies appear together?
* Which stacks generate the most opportunities?
* Which technologies correlate with urgency?
* Which technologies correlate with modernization projects?
* Which technologies are associated with AI adoption?
* Which technologies have low competition but high demand?
* Which industries use which stacks?
* Which technologies are generating SaaS opportunities?

---

# PHASE 1 — TECHNOLOGY CATALOG

Create entity:

## Technology

Fields:

* Id
* Name
* CanonicalName
* Category
* Description
* FirstSeenAt
* LastSeenAt
* TotalMentions
* WeeklyMentions
* GrowthRate
* MomentumScore
* DemandScore
* CompetitionScore
* OpportunityScore
* AvgLeadScore
* AvgUrgency
* AvgSalarySignal
* IndustryCoverageCount
* ClusterCoverageCount
* EmergingScore
* IsAiRelated
* IsCloudRelated
* IsLegacy
* LifecycleStage
* CreatedAt
* UpdatedAt

LifecycleStage enum:

* Emerging
* Growing
* Mature
* Declining
* Legacy

---

# PHASE 2 — TECHNOLOGY EXTRACTION ENGINE

Create:

## TechnologyIntelligenceService

Responsibilities:

* Parse all JobInsights
* Normalize technologies using TechCanonicalizer
* Aggregate technology statistics
* Detect trending technologies
* Compute momentum
* Compute growth rate
* Compute industry coverage
* Compute opportunity correlation

Must support:

* incremental updates
* full rebuild mode
* background processing
* batch execution

---

# PHASE 3 — STACK RELATIONSHIP ENGINE

Create:

## TechnologyRelationship

Fields:

* SourceTechnologyId
* TargetTechnologyId
* CoOccurrenceCount
* CorrelationScore
* IndustryAffinity
* OpportunityAffinity
* AiAffinity
* LastSeenAt

Detect combinations such as:

* .NET + Azure + SQL
* React + Node + AWS
* Python + LangChain + OpenAI

Implement:

* graph generation
* relationship weighting
* strongest pair detection
* stack ecosystem mapping

---

# PHASE 4 — TREND ENGINE

Create:

## TrendEngineService

Capabilities:

### Detect:

* Emerging technologies
* Declining technologies
* Migration patterns
* AI adoption waves
* Stack transitions

Examples:

* Angular → React
* .NET Framework → .NET 8
* VMware → Kubernetes
* SQL Server → Snowflake

Implement:

* time-series trend analysis
* moving averages
* growth acceleration
* anomaly detection

---

# PHASE 5 — TECHNOLOGY OPPORTUNITY ENGINE

Create:

## TechnologyOpportunityEngine

Must compute:

* TechOpportunityScore
* CommercialViability
* SaaSPotential
* ConsultingPotential
* AutomationPotential
* AITransformationPotential
* EnterpriseDemand
* SMBDemand
* CompetitiveDensity

Identify:

* underserved markets
* high-demand low-competition technologies
* technologies driving modernization

---

# PHASE 6 — AI ADOPTION INTELLIGENCE

Create dedicated AI tracking.

Detect:

* OpenAI
* Claude
* MCP
* LangChain
* RAG
* Vector DBs
* AI agents
* Copilots
* LLM infrastructure

Track:

* weekly growth
* industry adoption
* recurring pain points
* AI implementation demand

Generate:
AIAdoptionScore

---

# PHASE 7 — FRONTEND MODULE

Add new sidebar section:

INTELLIGENCE

* Idea Vault
* Intelligence
* Clusters
* Semantic Groups
* Technologies
* Trends
* Stack Graph

---

# TECHNOLOGIES PAGE

Create:
`/technologies`

Features:

* searchable technology catalog
* trend charts
* momentum indicators
* growth metrics
* lifecycle visualization
* industry distribution
* related technologies graph
* top opportunities
* AI adoption indicators

---

# TRENDS PAGE

Create:
`/trends`

Features:

* fastest growing technologies
* declining stacks
* emerging AI tools
* migration waves
* modernization trends
* industry heatmaps

---

# STACK GRAPH PAGE

Create:
`/stack-graph`

Interactive graph visualization:

* nodes = technologies
* edges = relationships
* size = demand
* color = lifecycle stage

---

# PHASE 8 — DATABASE + ANALYTICS

Create migrations for:

* Technology
* TechnologyRelationship
* TechnologyTrendSnapshot
* TechnologyOpportunitySnapshot

Implement:

* snapshot history
* weekly aggregation
* trend persistence
* historical analytics

---

# PHASE 9 — APIs

Create APIs:

GET /api/technologies
GET /api/technologies/{id}
GET /api/technologies/trending
GET /api/technologies/emerging
GET /api/technologies/declining
GET /api/technologies/ai
GET /api/technologies/graph
GET /api/trends
GET /api/trends/migrations
GET /api/trends/industries

---

# PHASE 10 — MARKET INTELLIGENCE INSIGHTS

Generate strategic insights such as:

* “Healthcare is rapidly adopting AI copilots.”
* “Blazor demand is increasing in enterprise modernization.”
* “LangChain adoption grew 230% in SaaS startups.”
* “React + Node + AWS is the dominant SMB stack.”
* “Kubernetes migration demand increased in fintech.”

Insights must be:

* data-driven
* explainable
* reproducible

Avoid hallucinations.

---

# IMPORTANT CONSTRAINTS

DO NOT:

* rewrite current pipeline
* remove clustering
* remove semantic groups
* replace current scoring
* hardcode fake trends
* invent unsupported statistics

This module must derive insights from REAL collected data.

---

# OUTPUT REQUIRED

Generate:

1. Full architecture plan
2. Backend implementation design
3. Entity diagrams
4. EF Core migrations
5. Processing pipeline
6. Technology scoring formulas
7. Trend formulas
8. Relationship graph strategy
9. Frontend architecture
10. Suggested charts and visualizations
11. API contracts
12. Batch processing strategy
13. Observability strategy
14. Example dashboards
15. Production-grade code skeletons

DO NOT provide high-level ideas only.

Provide production-ready engineering architecture and implementation details.
