const express = require("express");
const { connect } = require("puppeteer-real-browser");
const fs = require("fs");
const path = require("path");

const app = express();
app.use(express.json({ limit: "1mb" }));

const UPWORK_HEADLESS = (process.env.UPWORK_HEADLESS || "true").toLowerCase() === "true";
const MANUAL_LOGIN_TIMEOUT_SECONDS = Number(process.env.MANUAL_LOGIN_TIMEOUT_SECONDS || 300);
const UPWORK_FORCE_MANUAL_LOGIN = (process.env.UPWORK_FORCE_MANUAL_LOGIN || "false").toLowerCase() === "true";
const SESSION_FILE_PATH = process.env.UPWORK_SESSION_FILE_PATH || path.join(__dirname, ".upwork-session.json");
const IS_CONTAINER = fs.existsSync("/.dockerenv");

let sessionCookies = [];
let sessionToken = null;
let sessionExpiresAt = null;

function saveSessionToDisk() {
  const payload = {
    sessionCookies,
    sessionToken,
    sessionExpiresAt
  };

  fs.writeFileSync(SESSION_FILE_PATH, JSON.stringify(payload, null, 2), "utf8");
}

function loadSessionFromDisk() {
  if (!fs.existsSync(SESSION_FILE_PATH)) {
    return false;
  }

  try {
    const raw = fs.readFileSync(SESSION_FILE_PATH, "utf8");
    const payload = JSON.parse(raw);
    sessionCookies = Array.isArray(payload.sessionCookies) ? payload.sessionCookies : [];
    sessionToken = typeof payload.sessionToken === "string" ? payload.sessionToken : null;
    sessionExpiresAt = typeof payload.sessionExpiresAt === "string" ? payload.sessionExpiresAt : null;
    return true;
  } catch (error) {
    console.error("failed to load upwork session file:", error?.message || error);
    return false;
  }
}

function clearSessionState() {
  sessionCookies = [];
  sessionToken = null;
  sessionExpiresAt = null;

  if (fs.existsSync(SESSION_FILE_PATH)) {
    try {
      fs.unlinkSync(SESSION_FILE_PATH);
    } catch (error) {
      console.error("failed to remove upwork session file:", error?.message || error);
    }
  }
}

function isAuthenticatedSession() {
  if (!sessionToken || !sessionExpiresAt) {
    return false;
  }

  return new Date(sessionExpiresAt).getTime() > Date.now();
}

function ensureString(value) {
  return typeof value === "string" ? value.trim() : "";
}

function buildUpworkSearchUrl(query, page) {
  const encodedQuery = encodeURIComponent(query);
  return `https://www.upwork.com/nx/search/jobs/?q=${encodedQuery}&page=${page}`;
}

function normalizeUpworkUrl(href) {
  if (!href || typeof href !== "string") {
    return "";
  }

  let url = href.trim();
  if (!url) {
    return "";
  }

  if (!url.startsWith("http")) {
    url = `https://www.upwork.com${url.startsWith("/") ? "" : "/"}${url}`;
  }

  return url.split(/[?#]/)[0].replace(/\/$/, "");
}

async function findJobCards(page) {
  const selectors = [
    "article[data-test='job-tile']",
    "article[data-test='JobTile']",
    "section[data-test='job-tile']",
    "[data-test='job-tile-list'] article",
    "section.air3-card-section"
  ];

  for (const selector of selectors) {
    let cards;
    try {
      cards = await page.$$(selector);
    } catch (error) {
      const message = String(error?.message || "").toLowerCase();
      if (message.includes("execution context was destroyed") || message.includes("most likely because of a navigation")) {
        return { cards: [], selector: null, retriableNavigationError: true };
      }

      throw error;
    }

    if (cards.length > 0) {
      return { cards, selector };
    }
  }

  return { cards: [], selector: null, retriableNavigationError: false };
}

async function waitForJobCards(page, timeoutMs) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    const result = await findJobCards(page);
    if (result.cards.length > 0) {
      return result;
    }

    if (result.retriableNavigationError) {
      await new Promise((resolve) => setTimeout(resolve, 700));
      continue;
    }

    await new Promise((resolve) => setTimeout(resolve, 1000));
  }

  return { cards: [], selector: null, retriableNavigationError: false };
}

function isUpworkAuthenticatedUrl(url) {
  const current = (url || "").toLowerCase();
  if (!current) {
    return false;
  }

  if (current.includes("accounts.google.com") || current.includes("/ab/account-security/login") || current.includes("/login")) {
    return false;
  }

  return current.includes("upwork.com/nx/find-work") || current.includes("upwork.com/nx/search/jobs");
}

async function waitForManualLoginCompletion(page, timeoutSeconds) {
  const deadline = Date.now() + timeoutSeconds * 1000;

  while (Date.now() < deadline) {
    const currentUrl = page.url() || "";
    if (isUpworkAuthenticatedUrl(currentUrl)) {
      return true;
    }

    await new Promise((resolve) => setTimeout(resolve, 1000));
  }

  return false;
}

async function newBrowserSession() {
  const launchArgs = IS_CONTAINER
    ? [
      "--no-sandbox",
      "--disable-setuid-sandbox",
      "--disable-dev-shm-usage"
    ]
    : [];

  return connect({
    headless: UPWORK_HEADLESS,
    args: launchArgs
  });
}

async function ensureAuthenticatedPage({ page }) {
  if ((!Array.isArray(sessionCookies) || sessionCookies.length === 0) && !loadSessionFromDisk()) {
    throw new Error("No active Upwork session. Login is required.");
  }

  await page.setCookie(...sessionCookies);
}

app.get("/health", (_, res) => {
  res.json({ status: "ok", authenticated: isAuthenticatedSession() });
});

app.post("/upwork/login", async (req, res) => {
  const username = ensureString(req.body?.username);
  const password = ensureString(req.body?.password);

  if (!username || !password) {
    return res.status(400).json({ error: "username and password are required" });
  }

  let browser;
  let page;

  try {
    const browserSession = await newBrowserSession();
    browser = browserSession.browser;
    page = browserSession.page;

    await page.goto("https://www.upwork.com/ab/account-security/login", {
      waitUntil: "domcontentloaded",
      timeout: 60000
    });

    if (UPWORK_FORCE_MANUAL_LOGIN) {
      if (UPWORK_HEADLESS) {
        return res.status(409).json({
          error: "UPWORK_FORCE_MANUAL_LOGIN requires UPWORK_HEADLESS=false so you can interact with the browser window."
        });
      }

      console.log("Manual Upwork login mode enabled. Complete login in the opened browser window.");
      const solved = await waitForManualLoginCompletion(page, MANUAL_LOGIN_TIMEOUT_SECONDS);
      if (!solved) {
        return res.status(408).json({ error: "Manual Upwork login was not completed before timeout." });
      }

      sessionCookies = await page.cookies();
      sessionToken = `upw_${Date.now()}`;
      sessionExpiresAt = new Date(Date.now() + 12 * 60 * 60 * 1000).toISOString();
      saveSessionToDisk();

      return res.json({
        isAuthenticated: true,
        expiresAt: sessionExpiresAt,
        sessionToken
      });
    }

    await page.waitForSelector("input#login_username, input[name='login[username]']", {
      timeout: 45000
    });
    await page.type("input#login_username, input[name='login[username]']", username, { delay: 25 });

    const continueSelector = "button#login_password_continue, button[button-role='continue'], button[data-ev-label='Continue']";
    if (await page.$(continueSelector)) {
      await page.click(continueSelector);
    } else {
      await page.keyboard.press("Enter");
    }

    await page.waitForSelector("input#login_password, input[name='password'], input[name='login[password]']", {
      timeout: 45000
    });
    await page.type("input#login_password, input[name='password'], input[name='login[password]']", password, { delay: 25 });

    const submitSelector = "button#login_control_continue, button[type='submit']";
    await page.click(submitSelector);

    await page.waitForNavigation({ waitUntil: "domcontentloaded", timeout: 60000 }).catch(() => null);
    await page.goto("https://www.upwork.com/nx/find-work/best-matches", {
      waitUntil: "domcontentloaded",
      timeout: 60000
    });

    const currentUrl = (page.url() || "").toLowerCase();
    let isAuthenticated = isUpworkAuthenticatedUrl(currentUrl);

    if (!isAuthenticated) {
      const html = (await page.content()).toLowerCase();
      if (html.includes("captcha") || html.includes("cloudflare") || html.includes("forbidden")) {
        if (UPWORK_HEADLESS) {
          return res.status(409).json({
            error: "Upwork challenge/block detected during login. Set UPWORK_HEADLESS=false and complete login manually in a real browser session."
          });
        }

        const solved = await waitForManualLoginCompletion(page, MANUAL_LOGIN_TIMEOUT_SECONDS);
        if (!solved) {
          return res.status(408).json({ error: "Manual Upwork login was not completed before timeout." });
        }

        isAuthenticated = true;
      }

      if (!isAuthenticated) {
        if (UPWORK_HEADLESS) {
          return res.status(401).json({ error: "Upwork login failed in headless mode." });
        }

        const solved = await waitForManualLoginCompletion(page, MANUAL_LOGIN_TIMEOUT_SECONDS);
        if (!solved) {
          return res.status(408).json({ error: "Manual Upwork login was not completed before timeout." });
        }

        isAuthenticated = true;
      }
    }

    sessionCookies = await page.cookies();
    sessionToken = `upw_${Date.now()}`;
    sessionExpiresAt = new Date(Date.now() + 12 * 60 * 60 * 1000).toISOString();
    saveSessionToDisk();

    return res.json({
      isAuthenticated: true,
      expiresAt: sessionExpiresAt,
      sessionToken
    });
  } catch (error) {
    console.error("upwork login error:", error?.stack || error?.message || error);
    return res.status(500).json({ error: error.message || "Unhandled login error" });
  } finally {
    if (browser) {
      await browser.close().catch(() => undefined);
    }
  }
});

app.post("/upwork/scrape", async (req, res) => {
  const query = ensureString(req.body?.query);
  const location = ensureString(req.body?.location) || "Remote";
  const limit = Math.max(1, Math.min(Number(req.body?.limit || 20), 5000));
  const maxPages = 5;
  const startPage = Math.max(1, Math.min(Number(req.body?.startPage || 1), maxPages));
  const endPageRequest = Number(req.body?.endPage || startPage);
  const endPage = Math.max(startPage, Math.min(endPageRequest, maxPages));

  if (!query) {
    return res.status(400).json({ error: "query is required" });
  }

  if (!isAuthenticatedSession()) {
    loadSessionFromDisk();
  }

  if (!isAuthenticatedSession()) {
    return res.status(401).json({ error: "No authenticated Upwork session. Login first." });
  }

  let browser;
  let page;

  try {
    const browserSession = await newBrowserSession();
    browser = browserSession.browser;
    page = browserSession.page;

    await ensureAuthenticatedPage({ page });

    const nowIso = new Date().toISOString();
    const jobs = [];
    const byUrl = new Map();
    const pageDiagnostics = [];

    for (let currentPage = startPage; currentPage <= endPage && jobs.length < limit; currentPage += 1) {
      const url = buildUpworkSearchUrl(query, currentPage);
      await page.goto(url, { waitUntil: "domcontentloaded", timeout: 60000 });
      await new Promise((resolve) => setTimeout(resolve, 800));

      const currentUrl = (page.url() || "").toLowerCase();
      if (currentUrl.includes("/login") || currentUrl.includes("account-security")) {
        if (UPWORK_HEADLESS) {
          clearSessionState();
          return res.status(409).json({ error: "Upwork session expired during scrape." });
        }

        console.log("Upwork challenge/login detected during scrape. Complete it manually in the browser window.");
        const solved = await waitForManualLoginCompletion(page, MANUAL_LOGIN_TIMEOUT_SECONDS);
        if (!solved) {
          return res.status(408).json({ error: "Manual Upwork challenge/login during scrape was not completed before timeout." });
        }

        sessionCookies = await page.cookies();
        sessionToken = `upw_${Date.now()}`;
        sessionExpiresAt = new Date(Date.now() + 12 * 60 * 60 * 1000).toISOString();
        saveSessionToDisk();

        await page.goto(url, { waitUntil: "domcontentloaded", timeout: 60000 });
        await new Promise((resolve) => setTimeout(resolve, 800));
      }

      const pageHtml = (await page.content()).toLowerCase();
      if (pageHtml.includes("captcha") || pageHtml.includes("cloudflare") || pageHtml.includes("access denied")) {
        if (UPWORK_HEADLESS) {
          return res.status(409).json({ error: "Upwork challenge/block detected during scrape in headless mode." });
        }

        console.log("Upwork anti-bot challenge detected during scrape. Waiting for manual resolution.");
        const solved = await waitForManualLoginCompletion(page, MANUAL_LOGIN_TIMEOUT_SECONDS);
        if (!solved) {
          return res.status(408).json({ error: "Manual Upwork challenge during scrape was not completed before timeout." });
        }

        sessionCookies = await page.cookies();
        sessionToken = `upw_${Date.now()}`;
        sessionExpiresAt = new Date(Date.now() + 12 * 60 * 60 * 1000).toISOString();
        saveSessionToDisk();

        await page.goto(url, { waitUntil: "domcontentloaded", timeout: 60000 });
        await new Promise((resolve) => setTimeout(resolve, 800));
      }

      const waitResult = await waitForJobCards(page, 15000);
      const cards = waitResult.cards;
      pageDiagnostics.push({
        page: currentPage,
        url: page.url(),
        cardSelector: waitResult.selector,
        cardCount: cards.length
      });

      if (!cards.length) {
        continue;
      }

      for (let index = 0; index < cards.length && jobs.length < limit; index += 1) {
        const card = cards[index];

        const href = await card.$eval("h2 a, a[data-test='job-title-link']", (el) => el.getAttribute("href")).catch(() => "");
        const normalizedUrl = normalizeUpworkUrl(href);
        if (!normalizedUrl || byUrl.has(normalizedUrl)) {
          continue;
        }

        const title = await card.$eval("h2, a[data-test='job-title-link']", (el) => (el.textContent || "").trim()).catch(() => "");
        if (!title) {
          continue;
        }

        const description = await card.$eval("[data-test='job-description-text'], [data-test='UpCLineClamp JobDescription']", (el) => (el.textContent || "").trim()).catch(() => title);
        const budget = await card.$eval("[data-test='budget'], [data-test='is-fixed-price']", (el) => (el.textContent || "").trim()).catch(() => "");
        const experience = await card.$eval("[data-test='experience-level'], [data-test='expert-level']", (el) => (el.textContent || "").trim()).catch(() => "");
        const jobType = await card.$eval("[data-test='job-type'], [data-test='duration-label']", (el) => (el.textContent || "").trim()).catch(() => "");

        const metadataJson = JSON.stringify({
          origin: "upwork-puppeteer-real-browser",
          page: currentPage,
          rank: index + 1,
          budget,
          experience,
          jobType
        });

        const item = {
          externalKey: normalizedUrl.toLowerCase(),
          title,
          company: "Client",
          location,
          description,
          url: normalizedUrl,
          source: "upwork",
          searchTerm: query,
          capturedAt: nowIso,
          metadataJson
        };

        byUrl.set(normalizedUrl, item);
        jobs.push(item);
      }
    }

    return res.json({
      isAuthenticated: true,
      sessionToken,
      jobs,
      diagnostics: {
        pages: pageDiagnostics,
        totalPagesVisited: pageDiagnostics.length
      }
    });
  } catch (error) {
    console.error("upwork scrape error:", error?.stack || error?.message || error);
    return res.status(500).json({ error: error.message || "Unhandled scrape error" });
  } finally {
    if (browser) {
      await browser.close().catch(() => undefined);
    }
  }
});

app.listen(process.env.PORT || 3000, () => {
  loadSessionFromDisk();
  console.log("upwork scraper api running on port", process.env.PORT || 3000);
});
