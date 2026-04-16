# Backend Testing Guide

## 1) Scope
This guide covers backend endpoint validation using:
- xUnit integration tests
- Postman collection imports

## 2) Projects
- Unit tests: backend/backend.Tests
- Integration tests: backend/backend.IntegrationTests

## 3) Run tests locally
From backend folder:

```bash
dotnet build
dotnet test backend.Tests/backend.Tests.csproj
dotnet test backend.IntegrationTests/backend.IntegrationTests.csproj
```

Optional custom API URL for integration tests:

```bash
MARKET_API_BASE_URL=http://localhost:8080 dotnet test backend.IntegrationTests/backend.IntegrationTests.csproj
```

## 4) What integration tests validate
- GET /api/jobs/jobs/query
- GET /api/jobs/jobs/full
- GET /api/auth/status/linkedin
- GET /api/market/opportunities
- GET /api/market/leads
- GET /api/market/trends

## 5) Postman import file
Import this collection:
- backend/postman/LinkedInScrapingJobs-Backend.postman_collection.json

Collection variable:
- baseUrl = http://localhost:8080

## 6) Recommended Postman test order
1. Auth - Status LinkedIn
2. Jobs - Query (paged)
3. Jobs - Full
4. Market - Opportunities
5. Market - Leads
6. Market - Trends
7. Scrape - Upwork (optional, slower and auth-dependent)

## 7) Notes
- Scrape endpoints may require valid provider session and can return 409/408 when re-authentication/manual action is needed.
- Market endpoints depend on existing processed JobInsights data.
