You are a senior .NET architect specialized in Clean Architecture, scalable scraping systems, and Playwright optimization.

I have an existing class `JobOrchestrator` that currently:

* Handles provider selection (LinkedIn, Indeed)
* Executes scraping using Playwright
* Manages authentication sessions
* Contains business logic and data mapping
* Stores results via repository

This violates separation of concerns and is hard to scale.

Your task is to REFACTOR the system into a clean, extensible architecture.

---

## 🎯 OBJECTIVES

Refactor the current implementation to achieve:

1. Separation of responsibilities (SRP)
2. Provider-based extensibility (Strategy Pattern)
3. Playwright performance optimization (browser reuse)
4. Resilience using retry policies
5. Testability and maintainability

---

## 🧱 TARGET ARCHITECTURE

Implement the following structure:

Application Layer:

* IJobOrchestrator
* JobOrchestrator (ONLY orchestration logic)
* IJobProvider (strategy interface)

Infrastructure Layer:

* Providers/

  * LinkedInProvider
  * IndeedProvider
* Scraping/

  * IPlaywrightScraper
  * PlaywrightScraper
  * IBrowserPool (browser reuse)
* Sessions/

  * ISessionManager
  * LinkedInSessionManager

Domain Layer:

* JobOffer (already exists)

---

## 🧩 REQUIRED IMPLEMENTATION DETAILS

### 1. Provider Strategy Pattern

Create:

```csharp
public interface IJobProvider
{
    string Name { get; }
    Task<List<JobOffer>> SearchAsync(SearchRequest request, CancellationToken ct);
    Task<bool> IsAuthenticatedAsync(CancellationToken ct);
}
```

Each provider must:

* Encapsulate its own scraping logic
* Handle its own authentication validation
* NOT depend on orchestrator internals

---

### 2. Refactor JobOrchestrator

It should:

* Resolve providers dynamically (via DI or factory)
* Execute providers in parallel (Task.WhenAll)
* Handle logging and aggregation
* NOT contain scraping logic

---

### 3. Introduce Browser Pool

Create:

```csharp
public interface IBrowserPool
{
    Task<IBrowser> GetBrowserAsync();
}
```

Implementation:

* Reuse a single Playwright instance
* Avoid launching browser per request
* Thread-safe

---

### 4. Extract Scraping Logic

Move ALL Playwright code into:

```csharp
public interface IPlaywrightScraper
{
    Task<List<RawJobData>> ScrapeLinkedInAsync(...);
    Task<List<RawJobData>> ScrapeGoogleAsync(...);
}
```

Then map:

```csharp
RawJobData → JobOffer
```

---

### 5. Session Management

Create:

```csharp
public interface ISessionManager
{
    Task<bool> ValidateAsync(string provider);
    Task SaveAsync(string provider, object sessionData);
    Task ClearAsync(string provider);
}
```

Move ALL session logic out of orchestrator.

---

### 6. Add Resilience (Polly)

Wrap provider execution with:

* Retry (3 attempts)
* Timeout
* Circuit breaker (optional)

---

### 7. Add Caching (optional but recommended)

Cache search results by:

* query + location
* expiration: 10–30 minutes

---

## ⚠️ CONSTRAINTS

* Keep .NET 8/9 compatibility
* Use async/await correctly
* Do NOT break existing repository interfaces
* Maintain current JobOffer structure
* Keep configuration via IConfiguration

---

## 📦 DELIVERABLES

Generate:

1. Refactored JobOrchestrator
2. IJobProvider + implementations (LinkedIn, Indeed)
3. PlaywrightScraper service
4. BrowserPool implementation
5. SessionManager implementation
6. Example DI configuration (Program.cs / Startup)
7. Clean folder structure

---

## 🚀 BONUS (if possible)

* Add SemaphoreSlim to limit concurrency
* Add structured logging (ILogger)
* Add basic unit test example for orchestrator

---

Refactor the code incrementally and ensure it compiles.

Avoid pseudo-code. Provide real, production-ready C# code.
