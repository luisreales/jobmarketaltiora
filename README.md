# HotelScrapingVIP

Full-stack hotel price tracker:

- backend: .NET Web API + Playwright scraper + PostgreSQL
- frontend: Angular dashboard/search/detail UI

## 1. Run Full Stack With Docker (recommended)

From project root:

```bash
docker compose up --build -d
```

Check status:

```bash
docker compose ps
docker compose logs -f backend
docker compose logs -f frontend
```

URLs:

- Frontend: http://localhost:4200
- Backend API: http://localhost:8080

Quick endpoint test:

```bash
curl "http://localhost:8080/api/booking/search?city=Madrid&checkIn=2026-06-10&checkOut=2026-06-12"
```

Stop stack:

```bash
docker compose down
```

Stop and remove DB volume:

```bash
docker compose down -v
```

### LinkedIn checkpoint/challenge in Docker

The Docker backend forces `Jobs__Playwright__LoginHeadless=true`, so LinkedIn manual verification cannot be completed inside the container.

Use this flow:

1. Stop backend container (`docker compose stop backend`).
2. Run backend locally with `Jobs:Playwright:LoginHeadless=false`.
3. Call `POST /api/auth/login`, complete the LinkedIn checkpoint in the opened browser, and wait for success.
4. Confirm `backend/playwright-state/linkedin.json` exists.
5. Start backend container again (`docker compose start backend`) and continue searches.

When scraping LinkedIn, backend now validates session health by opening `/feed` with stored state before running the search. If LinkedIn redirects to login/checkpoint/challenge, API returns `409` and asks for re-authentication.

## 2. Run Backend Locally (without Docker)

### Prerequisites

- .NET SDK installed (project currently targets net9.0)
- PostgreSQL running on localhost:5432
- DB credentials matching backend/appsettings.Development.json

### Install Playwright browsers (required one-time)

From backend folder:

```bash
dotnet build
pwsh bin/Debug/net9.0/playwright.ps1 install
```

If `pwsh` is not installed, use Docker mode instead, or install PowerShell.

### Start API

```bash
cd backend
dotnet run
```

If you get process exit code 134, it is usually missing Playwright browser dependencies; Docker mode is the fastest fix.

## 3. Run Frontend Locally

```bash
cd frontend
npm install
npm start
```

Frontend URL:

- http://localhost:4200

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
