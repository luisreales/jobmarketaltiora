# Upwork Scraping — Setup & Testing Guide

## Why scraper-api must run locally (not in Docker)

Upwork is protected by **Cloudflare Turnstile**. Any headless browser running inside a Docker
container is fingerprinted and blocked — it receives a "Just a moment…" challenge page instead
of the login form, regardless of which browser or `turnstile: true` flag is used.

The only way to bypass it is opening a **real visible Chrome window** (`showBrowser: true`) on
the host OS (macOS). The scraper-api must therefore run as a native Node.js process on your Mac,
not inside Docker. The backend container reaches it via `host.docker.internal:3000`.

---

## Architecture

```
Postman / Frontend
        │
        ▼
backend (Docker :8080)
        │  UpworkScraper__BaseUrl = http://host.docker.internal:3000
        ▼
scraper-api (LOCAL :3000)   ← must run on Mac, not in Docker
        │  showBrowser=true → opens real Chrome window
        ▼
Upwork.com
```

---

## Step 1 — Start Docker services (backend + postgres)

```bash
cd /Users/luisreales/LinkedInScrapingJobs

# Start only backend + postgres (NOT scraper-api profile)
docker-compose up -d postgres backend
```

Verify:
```bash
docker-compose ps
# jobmarketaltiora-postgres  → running (healthy)
# jobmarketaltiora-backend   → running (:8080)
```

---

## Step 2 — Start scraper-api locally on Mac

Open a dedicated terminal tab and keep it running:

```bash
cd /Users/luisreales/LinkedInScrapingJobs/scraper-api
node server.js
```

Expected output:
```
upwork scraper api running on port 3000
```

> **Important:** Do NOT use `UPWORK_HEADLESS=true` here — the default already handles that.
> The `showBrowser` flag sent at login time controls whether Chrome opens visibly.

Health check:
```bash
curl http://localhost:3000/health
# {"status":"ok","authenticated":false}
```

---

## Step 3 — Login to Upwork (Postman or curl)

This step opens a **real Chrome window** on your screen. You may need to complete any
2FA or CAPTCHA manually inside that window. The session is saved automatically.

### Postman

```
POST http://localhost:8080/api/auth/login
Content-Type: application/json

{
  "provider": "upwork",
  "username": "<your-upwork-email>",
  "password": "<your-upwork-password>",
  "showBrowser": true
}
```

### curl

```bash
curl -s -X POST http://localhost:8080/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"provider":"upwork","username":"YOUR_EMAIL","password":"YOUR_PASSWORD","showBrowser":true}' \
  | jq .
```

### Expected response (success)

```json
{
  "provider": "upwork",
  "isAuthenticated": true,
  "lastLoginAt": "2026-06-04T12:46:01.120167Z",
  "lastUsedAt": "2026-06-04T12:46:01.120186Z",
  "expiresAt": "2026-06-05T00:46:01.000000Z"
}
```

The session lasts **12 hours**. While Chrome is open during login, do not close it manually —
it closes automatically once the session is captured.

### Possible errors

| Status | Meaning | Fix |
|--------|---------|-----|
| `409` | Cloudflare challenge detected | Complete it in the Chrome window that opened |
| `408` | Manual login timed out (5 min) | Retry; complete 2FA faster |
| `401` | Login failed in headless mode | Always send `"showBrowser": true` |
| `500 connection refused` | scraper-api not running | Start it in Step 2 |

---

## Step 4 — Verify session status

```bash
curl -s http://localhost:8080/api/auth/status/upwork | jq .
# {"provider":"upwork","isAuthenticated":true,"expiresAt":"..."}
```

---

## Step 5 — Run Upwork scraping

### Option A — From the UI

1. Open `http://localhost:4200/scraping`
2. Fill in the **Upwork** card: query, location, max results
3. Click **Run Upwork Scraping**

### Option B — curl

```bash
curl -s -X POST http://localhost:8080/api/jobs/scrape \
  -H "Content-Type: application/json" \
  -d '{
    "query": ".NET developer",
    "location": "Remote",
    "limit": 20,
    "providers": ["upwork"],
    "startPage": 1,
    "endPage": 2
  }' | jq .
```

### Expected response

```json
{
  "savedCount": 18,
  "totalFound": 20,
  "executedAtUtc": "2026-06-04T13:00:00Z"
}
```

---

## Step 6 — Verify results

```bash
# Count jobs saved from Upwork
curl -s "http://localhost:8080/api/jobs?source=upwork&pageSize=5" | jq '{total: .totalCount, items: [.items[].title]}'
```

Or browse `http://localhost:4200` → Jobs tab, filter by Source = upwork.

---

## Session expiry & re-login

Sessions expire after 12 hours. The scraper-api also persists the session to
`.upwork-session.json` on disk — if you restart `node server.js`, the session is
restored automatically as long as it hasn't expired.

When a scrape returns HTTP 409 with `"Upwork session expired during scrape"`:
1. Repeat Step 3 (login again with `showBrowser: true`)
2. Re-run the scrape

---

## Quick restart checklist (Docker was stopped)

```bash
# 1. Start containers
docker-compose up -d postgres backend

# 2. Start scraper-api locally (separate terminal, keep it open)
cd /Users/luisreales/LinkedInScrapingJobs/scraper-api && node server.js

# 3. Login via Postman (POST /api/auth/login, showBrowser: true)

# 4. Verify
curl http://localhost:3000/health          # scraper-api alive
curl http://localhost:8080/api/auth/status/upwork  # session active

# 5. Scrape from UI or curl
```

---

## Troubleshooting

### `connect ECONNREFUSED 127.0.0.1:3000` from the backend

The scraper-api is not running locally. The backend connects via `host.docker.internal:3000`,
which maps to `127.0.0.1` on your Mac. Start `node server.js` in Step 2.

### Chrome opens but shows "Just a moment…" and never logs in

- Wait 10–30 seconds — Cloudflare may solve itself with a visible browser
- If it hangs, complete the CAPTCHA manually in the Chrome window
- Increase `MANUAL_LOGIN_TIMEOUT_SECONDS` if needed (default 300 s):
  ```bash
  MANUAL_LOGIN_TIMEOUT_SECONDS=600 node server.js
  ```

### Scrape returns 0 jobs / card selectors not found

Upwork periodically changes their HTML. Check `diagnostics.pages[].cardCount` in the
raw scraper-api response. If all pages show `cardCount: 0`, the CSS selectors in
`server.js → findJobCards()` need updating.

### Backend can't reach `host.docker.internal`

Confirm `extra_hosts` is set in `docker-compose.yml` for the backend service:
```yaml
extra_hosts:
  - "host.docker.internal:host-gateway"
```

### Session not persisting across scraper-api restarts

Check that `.upwork-session.json` exists in the `scraper-api/` directory and is not
corrupted. Delete it and re-login if needed.
