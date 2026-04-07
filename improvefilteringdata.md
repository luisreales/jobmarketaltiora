🚀 PROMPT PARA COPILOT
You are a senior .NET architect and data processing expert.

I have a job scraping system that stores JobOffer entities from LinkedIn and Indeed.

I want to evolve this system into a LEAD GENERATION ENGINE.

---

🎯 GOAL

Enhance the system to FILTER, CLASSIFY, and SCORE job offers to identify REAL CLIENT OPPORTUNITIES instead of consulting/intermediary companies.

---

🧠 CORE BUSINESS RULE (VERY IMPORTANT)

We ONLY want job offers from DIRECT CLIENT COMPANIES.

We must EXCLUDE consulting/intermediary companies such as:

- Softtek
- Capgemini
- Stefanini
- Globant
- Accenture
- Cognizant
- EPAM Systems

These companies are NOT valuable leads because they hire for third-party clients.

---

## 🧱 TASKS TO IMPLEMENT

### 1. Extend JobOffer entity

Add the following fields:

```csharp
public string Category { get; set; } // e.g. "Backend", "Fullstack", "Data"
public int OpportunityScore { get; set; } // 0–100
public bool IsConsultingCompany { get; set; }
public string CompanyType { get; set; } // "DirectClient", "Consulting", "Unknown"
2. Create Company Classification Service

Create:

public interface ICompanyClassifier
{
    CompanyClassificationResult Classify(string companyName, string description);
}

Return model:

public class CompanyClassificationResult
{
    public bool IsConsultingCompany { get; set; }
    public string CompanyType { get; set; }
}
3. Implement Classification Logic

Rules:

Normalize company name (lowercase, trim)
Check against predefined blacklist:
var consultingCompanies = new[]
{
    "softtek",
    "capgemini",
    "stefanini",
    "globant",
    "accenture",
    "cognizant",
    "epam",
    "epam systems"
};
If match → IsConsultingCompany = true
If NOT in blacklist, analyze description for keywords:
"clients"
"outsourcing"
"staff augmentation"
"for our client"
"third-party projects"

If found → mark as consulting

Otherwise → DirectClient
4. Create Opportunity Scoring Service
public interface IOpportunityScorer
{
    int Score(JobOffer job);
}

Scoring rules:

+30 → DirectClient
-20 → ConsultingCompany

+20 → contains ".NET", "C#", "ASP.NET"
+15 → contains "microservices", "scalability", "high traffic"
+10 → contains "Azure", "AWS", "cloud"
+10 → contains "API", "distributed systems"

Cap score between 0 and 100.

5. Integrate into Orchestrator Pipeline

After scraping and BEFORE saving:

Pipeline must be:

Scrape jobs
Normalize data
Classify company
Calculate OpportunityScore
Store enriched JobOffer
6. Filtering Capability

Add method:

Task<List<JobOffer>> GetHighValueLeadsAsync();

Criteria:

IsConsultingCompany == false
OpportunityScore >= 60
7. Optional (Recommended)

Add caching layer to avoid re-processing same jobs:

Key: ExternalId
TTL: 30 minutes
⚠️ CONSTRAINTS
Keep existing repository interfaces
Do not break current scraping logic
Use clean architecture principles
Make code testable and modular
🎯 FINAL RESULT

The system should no longer behave like a scraper, but like:

👉 A system that detects REAL companies that NEED .NET solutions and can be contacted directly.

Generate production-ready C# code with:

Services
Interfaces
Dependency Injection
Integration example

##UPDATE FILTERING

You are a senior .NET architect and data processing expert.

I have a Playwright-based job scraping system that stores JobOffer entities from LinkedIn and Indeed.

I want to evolve this system into a LEAD GENERATION ENGINE.

---

🎯 CRITICAL ARCHITECTURE RULE (DO NOT VIOLATE)

The scraping process MUST remain FAST, LIGHTWEIGHT, and UNCHANGED.

👉 DO NOT add classification, scoring, or heavy logic inside the scraping flow.

👉 ALL business logic must be executed in a SEPARATE BACKGROUND PROCESS.

---

🎯 GOAL

Enhance the system to FILTER, CLASSIFY, and SCORE job offers to identify REAL CLIENT OPPORTUNITIES instead of consulting/intermediary companies.

This must be done AFTER data is stored, using an asynchronous processing layer.

---

🧠 CORE BUSINESS RULE

We ONLY want job offers from DIRECT CLIENT COMPANIES.

We must EXCLUDE consulting/intermediary companies such as:

- Softtek
- Capgemini
- Stefanini
- Globant
- Accenture
- Cognizant
- EPAM Systems

These companies are NOT valuable leads because they hire for third-party clients.

---

## 🧱 ARCHITECTURE REQUIREMENT

Split the system into TWO phases:

### PHASE 1 — SCRAPING (UNCHANGED)

- Use Playwright
- Collect job data
- Save raw JobOffer to database
- Set IsProcessed = false

❌ No classification  
❌ No scoring  
❌ No filtering  

---

### PHASE 2 — BACKGROUND PROCESSING (NEW)

- Runs asynchronously (BackgroundService or worker)
- Processes unprocessed jobs
- Applies classification + scoring
- Updates database

---

## 🧱 TASKS TO IMPLEMENT

### 1. Extend JobOffer entity

```csharp
public string Category { get; set; }
public int OpportunityScore { get; set; }
public bool IsConsultingCompany { get; set; }
public string CompanyType { get; set; }

public bool IsProcessed { get; set; }
public DateTime? ProcessedAt { get; set; }


##NEW ENDPOINTS

🚀 DISEÑO DE ENDPOINTS (LO QUE NECESITAS)
🔹 1. Endpoint de datos procesados (EL IMPORTANTE)
GET /api/jobs/leads

👉 Este es tu endpoint principal (el que usará tu dashboard)

Filtros:
GET /api/jobs/leads?minScore=60&directOnly=true
Lógica:
public async Task<List<JobOffer>> GetHighValueLeadsAsync(int minScore = 60)
{
    return await _repo.Query()
        .Where(x => x.IsProcessed)
        .Where(x => !x.IsConsultingCompany)
        .Where(x => x.OpportunityScore >= minScore)
        .OrderByDescending(x => x.OpportunityScore)
        .ToListAsync();
}
🔹 2. Endpoint de todos los procesados
GET /api/jobs/processed
.Where(x => x.IsProcessed)

👉 Para análisis general

🔹 3. Endpoint de NO procesados (debug)
GET /api/jobs/unprocessed
.Where(x => !x.IsProcessed)

👉 Para validar que el worker funciona

🔹 4. Endpoint para disparar procesamiento manual
POST /api/jobs/process
[HttpPost("process")]
public async Task<IActionResult> Process(CancellationToken ct)
{
    await _processingService.ProcessUnprocessedJobsAsync(ct);
    return Ok("Processing triggered");
}
🔥 UPDATE FILTERING (LO QUE PEDISTE)

Aquí es donde haces magia:

Endpoint avanzado:
GET /api/jobs/leads/filter
Parámetros:
minScore=60
category=backend
companyType=DirectClient
search=.net
Implementación:
public async Task<List<JobOffer>> FilterLeadsAsync(JobFilter filter)
{
    var query = _repo.Query().Where(x => x.IsProcessed);

    if (filter.DirectOnly)
        query = query.Where(x => !x.IsConsultingCompany);

    if (filter.MinScore.HasValue)
        query = query.Where(x => x.OpportunityScore >= filter.MinScore);

    if (!string.IsNullOrWhiteSpace(filter.Category))
        query = query.Where(x => x.Category == filter.Category);

    if (!string.IsNullOrWhiteSpace(filter.Search))
        query = query.Where(x =>
            x.Title.Contains(filter.Search) ||
            x.Description.Contains(filter.Search));

    return await query
        .OrderByDescending(x => x.OpportunityScore)
        .ToListAsync();
}
🧱 DTO DE FILTRO
public class JobFilter
{
    public int? MinScore { get; set; }
    public bool DirectOnly { get; set; } = true;
    public string? Category { get; set; }
    public string? Search { get; set; }
}
🖥️ CÓMO LO VERÁS EN TU DASHBOARD

Aquí es donde esto se vuelve poderoso:

🔥 Vista 1 — Clientes reales
IsConsultingCompany = false
🔥 Vista 2 — Oportunidades top
OpportunityScore >= 60
🔥 Vista 3 — Problemas detectados

Filtrar por keywords:

microservices
migration
API
cloud
⚡ FLUJO COMPLETO FINAL
Scraper → guarda RAW
        ↓
Background Worker → procesa
        ↓
DB (enriched)
        ↓
API (/leads)
        ↓
Dashboard
💬 PROMPT PARA COPILOT (ESTA PARTE TE FALTABA)
You are a senior .NET backend engineer.

I have a background processing system that enriches JobOffer data.

Now I need to expose the processed data via API endpoints for a dashboard.

---

🎯 GOAL

Create endpoints to retrieve ONLY processed and high-value job opportunities.

---

## TASKS

### 1. Create endpoint

GET /api/jobs/leads

Return only:

- IsProcessed = true
- IsConsultingCompany = false
- OpportunityScore >= 60

---

### 2. Add filtering support

Query params:

- minScore
- search
- category

---

### 3. Create endpoint

GET /api/jobs/unprocessed

---

### 4. Create endpoint

POST /api/jobs/process

Triggers processing manually

---

## RESULT

The API should serve as a LEAD ENGINE, not a raw job API.
🧠 CONCLUSIÓN

Sí, lo entendiste perfecto:

👉 Scraper → no toca nada
👉 Worker → procesa
👉 Endpoint → muestra valor

Y aquí está el cambio clave:

❌ “ver jobs”
✅ “ver oportunidades de negocio”