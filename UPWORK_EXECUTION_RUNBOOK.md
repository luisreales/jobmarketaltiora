# Upwork Scraping Runbook

This document explains how to run Upwork authentication and scraping with the current architecture.

## Architecture Summary

- .NET backend API receives all external requests.
- Node scraper service controls the browser and interacts with Upwork.
- Backend calls Node service for Upwork login and scraping.
- Backend stores auth state and job results in PostgreSQL.

## Endpoints You Use

- Login: `POST http://localhost:8080/api/auth/login`
- Auth status: `GET http://localhost:8080/api/auth/status/upwork`
- Scrape jobs: `POST http://localhost:8080/api/jobs/search/scrape`
- Force Upwork-only scrape: `POST http://localhost:8080/api/jobs/search/scrape/upwork`
- Single call login + scrape: `POST http://localhost:8080/api/jobs/search/scrape/upwork/login-and-scrape`

## Important Rule

Do not call `http://localhost:3000/api/auth/login`.

Port 3000 is the Node scraper internals, not the backend API route.

## Recommended Flow (Production Style)

1. Call login once.
2. Confirm auth status.
3. Call scraping endpoint as many times as needed.
4. Re-login only if status is invalid or scraping reports session expired/challenge.

## Local Manual Login Mode (Best for Cloudflare challenge)

Use this when you must complete login/challenge manually.

This is the most reliable way to validate the full Upwork flow end-to-end.

### 1) Stop scraper container to free port 3000

```bash
docker compose stop scraper-api
```

### 2) Run Node scraper locally in manual mode

From `scraper-api/`:

```bash
UPWORK_HEADLESS=false UPWORK_FORCE_MANUAL_LOGIN=true MANUAL_LOGIN_TIMEOUT_SECONDS=600 PORT=3000 node server.js
```

Keep this terminal open.

### 3) Start backend container pointing to local Node service

From project root:

```bash
UPWORK_SCRAPER_BASE_URL=http://host.docker.internal:3000 docker compose up -d --build backend
```

This command also applies the backend Upwork timeout configuration (900s) used for manual login windows.

### 4) Trigger login from Postman or curl

```bash
curl -i -X POST http://localhost:8080/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"provider":"upwork","username":"x","password":"y"}'
```

Expected behavior:

- Browser window is used for manual login.
- You complete Upwork login/challenge.
- API returns success when session is detected.

### 5) Validate status

```bash
curl -i http://localhost:8080/api/auth/status/upwork
```

### 6) Run scrape

```bash
curl -i -X POST http://localhost:8080/api/jobs/search/scrape \
  -H "Content-Type: application/json" \
  -d '{
    "query":"dotnet",
    "location":"Remote",
    "limit":5,
    "providers":["upwork"],
    "startPage":1,
    "endPage":1
  }'
```

### 7) Run dedicated Upwork scrape endpoint (recommended for validation)

This endpoint forces provider `upwork` and returns `totalFound`, `savedCount`, and a `touched` list.

```bash
curl -i -X POST http://localhost:8080/api/jobs/search/scrape/upwork \
  -H "Content-Type: application/json" \
  -d '{
    "query":"dotnet",
    "location":"Remote",
    "limit":5,
    "startPage":1,
    "endPage":1
  }'
```

How to read the response:

- `totalFound > 0`: scraper executed and found jobs now.
- `savedCount > 0`: new rows inserted.
- `savedCount = 0` and `touchedCount > 0`: scrape executed, but rows already existed and were updated.

### 8) Optional single-call mode (login + scrape in one request)

Use this endpoint when you want backend to do login first and scrape right after in the same HTTP call.

```bash
curl -i -X POST http://localhost:8080/api/jobs/search/scrape/upwork/login-and-scrape \
  -H "Content-Type: application/json" \
  -d '{
    "query":"dotnet",
    "location":"Remote",
    "limit":5,
    "startPage":1,
    "endPage":1
  }'
```

### 9) Verify resulting jobs in DB-backed API

```bash
curl -i http://localhost:8080/api/jobs/jobs/full
```

You can filter in your client by `"source":"upwork"`.

## Session Reuse Behavior

- `/upwork/login` stores session for reuse.
- `/upwork/scrape` launches a new browser instance and reuses the previously stored session.
- Session is persisted in Node scraper file: `.upwork-session.json`.
- If Node restarts, scrape still works while that stored session is valid.
- If Upwork redirects to login during scrape, session is cleared and you must login again.

## Fast End-to-End Test (Minimal)

Run these in order:

1. `docker compose stop scraper-api`
2. `UPWORK_HEADLESS=false UPWORK_FORCE_MANUAL_LOGIN=true MANUAL_LOGIN_TIMEOUT_SECONDS=600 PORT=3000 node server.js` (from `scraper-api/`)
3. `UPWORK_SCRAPER_BASE_URL=http://host.docker.internal:3000 docker compose up -d --build backend`
4. `POST /api/auth/login` (complete manual login in browser)
5. `POST /api/jobs/search/scrape/upwork`
6. `GET /api/jobs/jobs/full`

## Docker-Only Mode

Use when you do not need manual challenge solving.

```bash
docker compose up -d --build
```

In this mode, scraper API runs in container and backend uses `http://scraper-api:3000`.

## Common Errors and Fixes

### 404 Cannot POST /api/auth/login on port 3000

Cause: calling Node service with backend route.

Fix: call backend route on port 8080.

### 415 Unsupported Media Type

Cause: missing JSON content type.

Fix: send `Content-Type: application/json` and raw JSON body.

### TaskCanceledException / HttpClient.Timeout

Cause: manual login took longer than configured backend timeout.

Fix: timeout configured to 900 seconds in current setup. Rebuild/restart backend if needed.

### 409 Authentication flow requires manual action

Cause: scraper reported challenge, timeout, or login issue.

Fix: run manual mode and complete challenge in browser.

### Port 3000 already in use

Cause: scraper container or another process still listening.

Fix:

```bash
docker compose stop scraper-api
```

Then start local Node again.

## Quick Health Checks

- Backend openapi:

```bash
curl -i http://localhost:8080/openapi/v1.json
```

- Node scraper health:

```bash
curl -i http://localhost:3000/health
```

## Operational Notes

- Login and scrape are intentionally separate operations.
- This keeps login retries/challenges isolated from scraping runs.
- Backend orchestrates all business flow; Node focuses on browser automation.
- Use `/api/jobs/search/scrape/upwork` when you want clear proof that Upwork scraping was executed in that request.
