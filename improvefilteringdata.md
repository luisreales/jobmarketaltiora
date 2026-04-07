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