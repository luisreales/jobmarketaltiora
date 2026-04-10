🚀 PROMPT PARA COPILOT — UPWORK SCRAPER (PRODUCTION READY)

You are a senior .NET architect specialized in Playwright scraping, authentication flows, and scalable provider-based architectures.

I have an existing scraping system using Clean Architecture with:

IJobProvider (Strategy Pattern)
PlaywrightScraper
BrowserPool
SessionManager
JobOrchestrator

I want to ADD a new provider: UpworkProvider

🎯 GOAL

Implement scraping for Upwork job listings using Playwright, including:

Authentication (login required)
Session persistence
Job extraction
Integration into existing provider architecture
⚠️ IMPORTANT RULE

🚫 DO NOT modify existing providers or orchestrator logic
✅ Follow existing abstractions (IJobProvider, IPlaywrightScraper, ISessionManager)

🧱 TASKS
1. Add Upwork Provider

Create:

public class UpworkProvider : IJobProvider
{
    private readonly IPlaywrightScraper _scraper;
    private readonly ISessionManager _sessionManager;

    public string Name => "upwork";

    public async Task<List<JobOffer>> SearchAsync(SearchRequest request, CancellationToken ct)
    {
        var rawJobs = await _scraper.ScrapeUpworkAsync(request, ct);

        return rawJobs.Select(job => new JobOffer
        {
            ExternalId = job.ExternalId,
            Title = job.Title,
            Company = job.Company ?? "Client",
            Location = job.Location ?? "Remote",
            Description = job.Description,
            Url = job.Url,
            Source = "upwork",
            SearchTerm = request.Query,
            CapturedAt = DateTime.UtcNow
        }).ToList();
    }

    public async Task<bool> IsAuthenticatedAsync(CancellationToken ct)
    {
        return await _sessionManager.ValidateAsync("upwork");
    }
}
2. Extend PlaywrightScraper

Add:

Task<List<RawJobData>> ScrapeUpworkAsync(SearchRequest request, CancellationToken ct);
3. Implement Upwork Scraping Logic

Inside PlaywrightScraper:

🔐 Login Flow

Use credentials from appsettings:

"Jobs": {
  "Credentials": {
    "Upwork": {
      "Username": "",
      "Password": ""
    }
  }
}
Login Steps:
Navigate to:
https://www.upwork.com/ab/account-security/login
Fill email:
input#login_username
Click continue
Fill password:
input#login_password
Submit
Handle redirect
Save session (cookies/localStorage)
Session Persistence

Use:

context.StorageStateAsync()

Save to:

playwright-state/upwork.json
Validate Session

Navigate to:

https://www.upwork.com/nx/find-work/best-matches

If redirected to login → session invalid

4. Scraping Jobs

Navigate to:

https://www.upwork.com/nx/search/jobs/?q={query}&page={page}
Extract per job:
Title
URL
Description
Budget (if available)
Posted time
Experience level
Job type (Hourly / Fixed)
Example selectors:
article[data-test="job-tile"]
h2 a
[data-test="job-description-text"]
[data-test="job-type"]
[data-test="experience-level"]
[data-test="budget"]
5. Raw Model
public class RawJobData
{
    public string ExternalId { get; set; }
    public string Title { get; set; }
    public string Url { get; set; }
    public string Description { get; set; }
    public string Company { get; set; }
    public string Location { get; set; }
}
6. Use BrowserPool

🚫 DO NOT launch browser per request
✅ Use IBrowserPool

var browser = await _browserPool.GetBrowserAsync();
7. Anti-Bot Considerations
Add delay between actions (300–1000ms)
Use real user-agent
Use headless=false for login (configurable)
Handle CAPTCHA manually (log warning)
8. Session Manager Integration

Use:

await _sessionManager.SaveAsync("upwork", storageState);
9. Dependency Injection

Register:

services.AddScoped<IJobProvider, UpworkProvider>();
10. Optional Improvements
Pagination support (page parameter)
Limit results
Retry with Polly
Logging per step
🎯 FINAL RESULT

The system should now support:

LinkedIn ✅
Indeed ✅
Upwork ✅ (NEW)

All using:

Strategy Pattern
Shared Playwright infrastructure
Persistent sessions
Scalable architecture
🚀 BONUS

If possible, add:

Budget parsing (USD)
Detection of high-value freelance jobs
Tag jobs as "FreelanceOpportunity"
🔥 IMPORTANT

Ensure:

Fully async
Clean separation of concerns
Production-ready code
No hardcoded credentials
Configuration-driven behavior