You are a senior full-stack architect.

Generate a complete full-stack project with two separate folders:

/backend  → .NET 10 Web API (LTS)
/frontend → Angular 20 (LTS)

The system is a hotel price tracker with scraping capabilities and a dashboard UI.

====================================
📁 ROOT STRUCTURE
====================================

Create:

/backend
/frontend
/docker-compose.yml
/README.md

====================================
🔧 BACKEND REQUIREMENTS (.NET 10)
====================================

Tech:
- .NET 10 Web API
- Entity Framework Core
- PostgreSQL (NOT SQL Server)
- Playwright for scraping
- HttpClient
- Serilog for logging

Architecture:
- Clean Architecture style (Controllers, Services, Repositories)
- Dependency Injection

------------------------------------
📦 FEATURES
------------------------------------

1. Booking scraping (2 methods):
   - DOM scraping using Playwright
   - API interception using Playwright network events

2. Hybrid scraping service:
   - Try API interception first
   - Fallback to DOM scraping

3. Price tracking:
   - Store historical prices

4. Deal detection:
   score = (averagePrice - currentPrice) / averagePrice

5. REST API endpoints:

GET /api/booking/search
GET /api/hotels
GET /api/hotels/{id}/prices
GET /api/hotels/{id}/analysis
POST /api/hotels

------------------------------------
🧱 DATABASE
------------------------------------

Use PostgreSQL with EF Core:

Tables:

Hotel:
- Id
- Name
- City

HotelPrice:
- Id
- HotelId
- Price
- Currency
- DateCaptured

------------------------------------
🧠 SERVICES
------------------------------------

Create:

- BookingPlaywrightService
- BookingApiInterceptorService
- BookingHybridService
- PriceTrackingService
- DealAnalysisService

------------------------------------
🐳 DOCKER BACKEND
------------------------------------

Include Dockerfile using:

mcr.microsoft.com/playwright/dotnet

Expose port 8080

====================================
🎨 FRONTEND REQUIREMENTS (Angular 20)
====================================

Tech:
- Angular 20 (standalone components)
- Angular Material
- Chart.js
- RxJS
- HttpClient

------------------------------------
📦 FEATURES
------------------------------------

Pages:

1. Dashboard:
   - Table of hotels
   - Columns:
     - Name
     - Current price
     - Average price
     - Score
     - Trend

2. Hotel Detail:
   - Chart.js line chart of price history

3. Search Page:
   - Form:
     - City
     - Check-in
     - Check-out
   - Calls backend scraping endpoint
   - Displays results

------------------------------------
🧩 COMPONENTS
------------------------------------

- hotel-table.component
- hotel-chart.component
- search-form.component

------------------------------------
🌐 API INTEGRATION
------------------------------------

Use Angular services:

- booking.service.ts
- hotel.service.ts

====================================
🐳 DOCKER COMPOSE
====================================

Create docker-compose.yml with:

- backend
- postgres

====================================
⚙️ CONFIG
====================================

Backend appsettings:

- ConnectionStrings
- Scraping.Headless

Frontend environment:

- API_URL

====================================
📄 README
====================================

Include instructions:

1. Run backend
2. Run frontend
3. Run with Docker
4. How to test scraping endpoint

====================================
🎯 OUTPUT
====================================

Generate FULL working code:

- Backend project
- Frontend Angular project
- Docker setup
- No TODOs
- No placeholders
- Everything runnable

Code must be clean, modular, and production-ready.


git init
git add README.md
git commit -m "first commit"
git branch -M master
git remote add origin git@github.com:luisreales/hotelscrapingvip.git
git push -u origin master