Database Audit — Current State
Raw counts
Table	Rows	Problem
JobOffers	608	All from Apr 7–22, 2026 — no new scrapes since
JobInsights	155	Only 25% of jobs analyzed by AI
MarketClusters	15	5 pending synthesis, scoring distorted
ProductSuggestions	4	All LlmStatus='pending' — never refined
AppSumoProducts	207	All have NULL description and NULL rating
AppSumoReviews	134	Reviews exist but no product context
Opportunities	6	3 converted, 1 failed
OpportunityIdeas	33	Reasonable quality
🔴 Critical problems
1. 453 of 608 jobs (74%) have ZERO AI analysis
The pipeline only processed 155 jobs. The other 453 are raw captures sitting untouched — no JobInsight, never clustered, never scored.

2. 145 of 155 insights are NOT assigned to any cluster
Only 10 insights have a ClusterId. The clustering engine ran against a tiny subset of the data. The 15 current clusters are built from an unrepresentative sample.

3. ProviderSessions are EXPIRED

LinkedIn session expired: April 23, 2026
Upwork session expired: April 22, 2026
Both are dead. You cannot scrape new jobs without re-authenticating.

4. AppSumo data is hollow
207 products scraped but every single one has Description=NULL and OverallRating=NULL. The scraper got names and slugs but never fetched the product detail pages.

🟡 Important quality issues
5. 5 clusters have PriorityScoreV2 = 0
LlmStatus='pending' — never synthesized, never scored by OpportunityEngineV2.

6. Single-job clusters rank above large clusters
The top 5 clusters each have exactly 1 job but score 77–76. The cluster with 30 jobs scores 75. This is a TAM/Fintech formula distortion — 1-job clusters get artificially boosted.

7. LlmConfidence is NULL on 14 of 15 clusters
Only cluster #15 has a confidence value. All others were synthesized before the LlmConfidence field was added.

8. All 4 products never had LLM refinement
ProductSuggestions were auto-generated but the refinement pass was never triggered.

🟢 Minor issues
9. 21 jobs with 13-character descriptions — LinkedIn failed to scrape the description body. These jobs are useless for analysis.

10. ~10 duplicate jobs — same Title+Company but different ExternalId (LinkedIn pagination duplicates).

11. AiPromptLogs: 27 calls with 0 tokens — model name mismatch in token tracking (claude-4-6 vs bedrock/anthropic.claude-4-6-sonnet).

Recommended Strategy

Phase 1 — Clean garbage (fast, safe)
  ├── Delete 21 jobs with description < 50 chars (+ their insights if any)
  ├── Delete duplicate jobs (keep highest OpportunityScore)
  └── Delete expired ProviderSessions (they're dead weight)

Phase 2 — Run the pipeline properly (the real fix)
  ├── Step A: Trigger Worker 1 LLM analysis on the 453 unprocessed jobs
  ├── Step B: Re-run clustering with ALL 155 insights (currently 145 are orphaned)
  ├── Step C: Run backfill-insights on Unknown-industry clusters
  ├── Step D: Re-synthesize all 15 clusters (reset LlmStatus → pending)
  └── Step E: Re-run OpportunityEngineV2 + ProductGenerator

Phase 3 — Fix scoring formula (code change)
  └── Add log(JobCount) confidence factor to PriorityScoreV2
      so 1-job clusters don't outrank 30-job clusters

Phase 4 — New data (requires re-auth)
  ├── Re-authenticate LinkedIn + Upwork
  ├── Scrape fresh jobs (last data is 4 weeks old)
  └── Re-run full pipeline end-to-end

Phase 5 — AppSumo (optional)
  └── Re-run scraper with detail page fetching to get descriptions + ratings
The single biggest lever: Phase 2 Step A. Once the 453 unprocessed jobs get analyzed and reclustered, the entire intelligence picture changes — you likely go from 15 clusters to 40–60, with much richer industry and pain category distribution.

Want me to proceed with Phase 1 (cleanup) first, or go straight to Phase 2?