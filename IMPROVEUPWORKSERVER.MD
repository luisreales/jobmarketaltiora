Prompt definitivo para Copilot
Role: Act as a Senior DevOps Engineer and Full-Stack .NET/Node.js Architect.

Context: I have a .NET Core backend that handles job portal authentication and scraping via Playwright. Playwright is getting blocked by Cloudflare on Upwork. I need to decouple all Upwork web automation by creating a dedicated Node.js microservice using puppeteer-real-browser to bypass Cloudflare, and containerize the whole solution using Docker Compose.

Current .NET Architecture Map (For Upwork Flow):

Controllers/AuthController.cs: Entry point for login (POST api/auth/login). Calls orchestrator.LoginAsync.

Infrastructure/Services/JobOrchestrator.cs: Orchestrates authentication. Important Note: It currently overrides request credentials with config credentials (Jobs:Credentials:Upwork) before calling sessionManager.LoginAsync.

Infrastructure/Sessions/LinkedInSessionManager.cs: Currently handles BOTH LinkedIn and Upwork. Contains massive methods like LoginUpworkAsync, EnsureUpworkAuthenticatedAsync, etc.

Infrastructure/Repositories/ProviderSessionRepository.cs: Persists session state (using ProviderSession entity).

Infrastructure/Providers/UpworkProvider.cs: Uses the session. If it detects a broken session, it clears it and forces re-login.

Infrastructure/Scraping/PlaywrightScraper.cs: Currently handles the actual Upwork scraping and block detection locally.

Program.cs: DI registrations (ISessionManager -> LinkedInSessionManager, IJobProvider -> UpworkProvider, IJobOrchestrator -> JobOrchestrator).

Task List:

1. Node.js Scraper Microservice (Express + Puppeteer):

Create a server.js file using Express.js. Expose POST /upwork/login and POST /upwork/scrape.

Use puppeteer-real-browser inside these endpoints to handle the Cloudflare bypass (headless: true, args like --no-sandbox, --disable-setuid-sandbox).

The /upwork/scrape endpoint should return JSON structured data of the jobs.

2. Docker Infrastructure:

Create a Dockerfile for the Node service using the official ghcr.io/puppeteer/puppeteer:latest base image (crucial for Chromium Linux dependencies).

Create a docker-compose.yml defining two services: scraper-api (Node) and core-backend (.NET). Set shm_size: '2gb' for the Node container. Map http://scraper-api:3000.

3. .NET Core Refactoring (The Surgery):

New Client: Create a Typed HttpClient UpworkScraperClient in C# (injecting IHttpClientFactory) to call the Node microservice endpoints. Register it in Program.cs pointing to the Docker service URL.

Clean Up LinkedInSessionManager.cs: Completely remove all Upwork logic (LoginUpworkAsync, challenge waiting, etc.). This class should be strictly for LinkedIn now.

Refactor JobOrchestrator.cs: Modify LoginAsync. When provider == "upwork", use the new UpworkScraperClient to trigger the login on the Node container. Make sure it still reads the config credentials (Jobs:Credentials:Upwork) to pass to the Node API.

Refactor UpworkProvider.cs & PlaywrightScraper.cs: Stop using local Playwright contexts for Upwork. Update these classes to call UpworkScraperClient.ScrapeAsync() to get the data from the Node service.

Session State: Ensure the session state (cookies/tokens returned by Node) is still correctly saved using ProviderSessionRepository.cs.