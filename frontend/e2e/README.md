# E2E UI Tests (Playwright)

This project uses Playwright to validate Angular UI flows on the jobs dashboard.

## Install

```bash
cd frontend
npm install
npm run e2e:install
```

## Run

```bash
cd frontend
npm run e2e
```

## Run in interactive mode

```bash
cd frontend
npm run e2e:ui
```

## Notes

- Tests run against `http://127.0.0.1:4200`.
- If an app server is already running on that URL, Playwright reuses it.
- If not, Playwright starts Angular automatically with `npm run start -- --host 127.0.0.1 --port 4200`.
