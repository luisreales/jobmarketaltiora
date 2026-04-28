# LinkedIn Job Scraping Platform

Full-stack job opportunity management system:

- **Backend**: .NET 9 Web API + Playwright scraper + PostgreSQL + EF Core
- **Frontend**: Angular dashboard with job search, opportunity management, and product ideation
- **Scraping**: LinkedIn and Upwork job scraping with AI-powered product idea generation

## Features

- 🔍 **Job Scraping**: Manual scraping from LinkedIn and Upwork
- 🤖 **AI Product Ideas**: Generate commercial product ideas from job postings using Semantic Kernel
- 📊 **Opportunity Management**: Convert jobs to sales opportunities with AI analysis
- 🏷️ **Product Catalog**: Create and manage B2B product suggestions with images
- 📈 **Sales Funnel**: Track opportunities from jobs to products to conversions

## Quick Start with Docker

```bash
# Start all services (backend, frontend, postgres)
docker compose up --build -d

# Check status
docker compose ps
```

**URLs:**
- Frontend: http://localhost:4200
- Backend API: http://localhost:8080
- PostgreSQL: localhost:5432

## Manual Job Scraping

1. Open http://localhost:4200/scraping
2. Choose scraping method:
   - **LinkedIn**: Direct browser scraping
   - **Upwork**: Requires scraper API service
   - **Multi-Provider**: Both platforms

### For Upwork Scraping

Start the scraper API service:

```bash
# Start only the scraper service
docker compose --profile scraper up scraper-api -d

# Or start all services including scraper
docker compose --profile scraper up -d
```

## API Endpoints

### Jobs
- `POST /api/jobs/search/scrape` - Scrape jobs from LinkedIn
- `POST /api/jobs/search/scrape/upwork` - Scrape jobs from Upwork
- `GET /api/jobs` - List scraped jobs

### Opportunities
- `GET /api/opportunities` - List opportunities
- `POST /api/opportunities/{id}/synthesize-ideas` - Generate AI product ideas
- `POST /api/opportunities/from-job/{jobId}` - Create opportunity from job

### Products
- `GET /api/products` - List products
- `POST /api/products/from-opportunity` - Create product from opportunity
- `POST /api/products/{id}/image` - Upload product image

## Environment Variables

Create a `.env` file in the project root:

```bash
# LinkedIn credentials
LINKEDIN_USERNAME=your_email@example.com
LINKEDIN_PASSWORD=your_password

# Upwork credentials
UPWORK_USERNAME=your_upwork_username
UPWORK_PASSWORD=your_upwork_password

# AI/LLM (optional)
SEMANTIC_KERNEL_ENABLED=true
FLOW_API_KEY=your_api_key
```

## Development Setup

### Backend (.NET 9)

```bash
cd backend
dotnet build
dotnet run
```

### Frontend (Angular)

```bash
cd frontend
npm install
npm start
```

### Database

The PostgreSQL container starts automatically with Docker Compose.

## Troubleshooting

### Upwork Scraping Errors

**Error**: "Upwork scraper API is not available"

**Solution**: Start the scraper service:
```bash
docker compose --profile scraper up scraper-api -d
```

### LinkedIn Authentication

**Error**: "Scraping requires re-authentication"

**Solution**: 
1. Stop backend container: `docker compose stop backend`
2. Run backend locally with `Jobs__Playwright__LoginHeadless=false`
3. Call login endpoint and complete LinkedIn verification
4. Restart container: `docker compose start backend`

### Build Issues

If you encounter Playwright browser issues:
```bash
# Install Playwright browsers
cd backend
pwsh bin/Debug/net9.0/playwright.ps1 install
```

## Architecture

- **Backend**: ASP.NET Core 9.0 with EF Core, Polly resilience, Semantic Kernel
- **Frontend**: Angular 18 with standalone components, Tailwind CSS
- **Database**: PostgreSQL with EF Core migrations
- **Scraping**: Playwright for browser automation
- **AI**: Semantic Kernel with OpenAI/GPT integration

## 4. Run Full Stack

Option A (recommended):

1. Start frontend + backend + postgres via Docker compose

Option B:

1. Start postgres locally
2. Start backend locally
3. Start frontend locally

## 5. Useful API Endpoints

- GET /api/booking/search?city=...&checkIn=YYYY-MM-DD&checkOut=YYYY-MM-DD
- GET /api/booking/discount-calendar?location=...&fromDate=YYYY-MM-DD&toDate=YYYY-MM-DD&nights=2
- GET /api/hotels
- GET /api/hotels/{id}/prices
- GET /api/hotels/{id}/analysis
- POST /api/hotels

Daily monitor (optional, disabled by default):

- Configure `Tracking:DailyScraping` in `backend/appsettings.json`
- Set `Enabled=true`
- Add one or more `Targets` with `Location`, occupancy, and stay offset
- The monitor scrapes daily at `RunDailyAtUtc` and stores history automatically

Example create hotel:

```bash
curl -X POST http://localhost:8080/api/hotels \
  -H "Content-Type: application/json" \
  -d '{"name":"Hotel Demo","city":"Madrid"}'
```

## 6. Lead Generation Endpoints

Main endpoint for dashboard leads (processed + high-value):

```bash
curl "http://localhost:8080/api/jobs/leads?minScore=60&directOnly=true"
```

Advanced filtering endpoint:

```bash
curl "http://localhost:8080/api/jobs/leads/filter?minScore=60&directOnly=true&category=Backend&companyType=DirectClient&search=.net"
```

Get all processed jobs:

```bash
curl "http://localhost:8080/api/jobs/processed"
```

Get unprocessed jobs (worker debug):

```bash
curl "http://localhost:8080/api/jobs/unprocessed"
```

Trigger manual processing:

```bash
curl -X POST "http://localhost:8080/api/jobs/process"
```

## 7. How Background Processing Works

Architecture flow:

1. Scraper saves raw jobs only.
2. Raw jobs are stored with `IsProcessed=false`.
3. `JobPostProcessingHostedService` runs in the API process.
4. Worker takes unprocessed rows in batches, classifies/scoring, and updates them as processed.

Automatic worker execution:

- It starts automatically when backend starts (`dotnet run` or Docker backend container).
- No extra command is required.

Worker settings (in `backend/appsettings*.json`):

- `Jobs:PostProcessing:IntervalSeconds`: polling interval when queue is empty.
- `Jobs:PostProcessing:BatchSize`: max jobs processed per cycle.

Manual processing option:

- Use `POST /api/jobs/process` to process pending rows immediately.
