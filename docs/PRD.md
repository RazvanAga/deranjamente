# PRD — deranjamente.com v1 (Outage Aggregator / Crawling)

> Status: draft. v1 is **crawl-only** and ships as a **Timiș-first MVP**, then expands
> nationally. User-generated reports, confirmations, and the JWT/role auth system are
> explicitly deferred to **v2**.

## Problem Statement

When a Romanian's water, electricity, gas, or internet goes out — or is about to be cut
for planned maintenance — there is no single place to check "is my area affected, right
now or soon?" The information *does* exist, but it is scattered across dozens of utility
providers' websites, each with its own layout, its own calendar widget, its own weekly
PDF, and no common format. A person who wants to know whether the water will be off on
their street tomorrow has to know who their operator is, find their site, and dig through
an awkward page. There is no national, cross-utility, cross-provider view. The closest
competitor (pretcurent.ro) covers only electricity.

## Solution

deranjamente.com crawls Romanian utility providers automatically, normalizes their
heterogeneous outage data into one shape, and presents it as a single, fast, map-first
website. A visitor lands on an interactive map of Romania's 41 județe colored by how many
outages are active in each, clicks their județ (or searches for their localitate), and
sees the planned interventions and unplanned faults affecting their area — with the time
window and a link back to the official source.

**v1 ships as a Timiș-first MVP**: two real sources for one county the founder can
personally validate — **Aquatim** (water) and **Rețele Electrice** (electricity,
ex-E-Distribuție Banat) — proving the full pipeline end-to-end, then expanding county by
county and then into gas/internet. The durable advantage is breadth into the categories
nobody aggregates — water especially, which is municipally fragmented and therefore the
most defensible. Electricity is table stakes (pretcurent.ro already covers it); it proves
the pipeline and anchors the map.

## User Stories

1. As a resident, I want to see a map of Romania colored by outage severity, so that I can immediately tell where problems are concentrated.
2. As a resident, I want to click my județ on the map, so that I can see outages affecting my area without scrolling a list of 41 counties.
3. As a resident, I want to search for my localitate by name, so that I can find my area even if I don't want to use the map.
4. As a resident, I want to see both planned interventions (intervenții programate) and unplanned faults (avarii), so that I know about both scheduled cut-offs and emergencies.
5. As a resident, I want each outage to show the affected streets/zone as text, so that I can tell whether my specific street is impacted.
6. As a resident, I want each outage to show its start and (where known) end time, so that I can plan around it.
7. As a resident, I want outages that are happening now to be clearly distinguished from ones scheduled for later, so that I understand urgency at a glance.
8. As a resident, I want to filter by utility type (curent / apă / toate), so that I can focus on the service I care about.
9. As a resident, I want a link back to the official provider page for each outage, so that I can verify the information at the source.
10. As a resident, I want to see which provider and when the data was last updated, so that I can trust the freshness of what I'm reading.
11. As a resident, I want the county pages to load fast and be findable on Google (e.g. "pană curent Timiș"), so that I can reach the right page directly from a search.
12. As a resident on mobile, I want the map and lists to work on a small screen, so that I can check outages from my phone during one.
13. As a resident, I want counties we don't cover yet to look clearly "not covered" rather than "no outages", so that I'm not misled into thinking an uncovered area is problem-free.
14. As a resident in Timiș, I want a searchable, severity-sorted list of affected localities in my county, so that I can quickly find mine.
15. As an operator of the site (admin), I want a dashboard showing each crawler's last run, row count, and status, so that I can tell at a glance whether crawling is healthy.
16. As an admin, I want to be alerted when a crawler that usually returns data suddenly returns nothing, so that I learn about a broken scraper before users do.
17. As an admin, I want to enable or disable a specific crawler without redeploying, so that I can take a broken source offline immediately.
18. As an admin, I want to retune a crawler's cadence and look-ahead horizon without redeploying, so that I can adapt to a source's behavior.
19. As an admin, I want to manually add an outage for a source that has no scrapable page, so that coverage isn't gated on a crawler existing.
20. As an admin, I want manually-entered outages to be visibly distinguished from scraped ones (provenance), so that the data's origin is auditable.
21. As an admin, I want to hide an erroneous/garbage outage without permanently deleting it, so that mistakes are reversible and history is preserved.
22. As an admin, I want place-names that can't be confidently matched to a canonical locality surfaced for review, so that I can correct them instead of the system guessing or dropping them.
23. As an admin, I want the operational dashboard and admin actions protected by a login, so that they aren't publicly accessible.
24. As an admin, I want a historical record of each crawl run, so that I can investigate when and why a source stopped returning data.
25. As a developer, I want to add a new provider by writing a single parse method against a common interface, so that scaling coverage is cheap and repeatable.
26. As a developer, I want crawler parsing tested against saved real-world HTML/JSON/PDF fixtures, so that tests are deterministic and run offline.
27. As a developer, I want one source breaking to never stop the others from crawling, so that the system is resilient.
28. As a developer, I want the dedup logic to recognize an outage already seen on a previous crawl, so that the database doesn't fill with duplicates.
29. As a developer, I want a changed end-time on a known outage to update the existing record rather than create a new one, so that history stays clean.
30. As a developer, I want sources that publish data per-calendar-day to be crawled across a forward window of dates, so that upcoming outages are captured even when the source only answers one day at a time.
31. As a developer, I want document-based sources (weekly PDFs) ingested once per document and skipped when unchanged, so that we don't re-download and re-parse static files constantly.
32. As a developer, I want an empty/placeholder PDF to be re-checked until it's replaced with real content, so that we don't permanently miss data from a late-published document.
33. As a developer, I want outages assigned to the correct județ even when the source spells localities inconsistently or with broken diacritics, so that the county page and map are accurate.
34. As a developer, I want errors and structured logs captured centrally, so that I can debug crawler failures.
35. As a future analyst, I want past outages and their source documents retained, so that history, trends, and re-parsing remain possible.

## Implementation Decisions

### Scope
- v1 = **crawl-only**. No user accounts, no user-submitted reports, no confirmations. Those are v2.
- **MVP = Timiș county**, two sources: **Aquatim** (apă) + **Rețele Electrice** (curent, ex-E-Distribuție Banat). Prove the full pipeline on a county the founder can validate, then expand county by county, then to gas/internet.
- Electricity is table stakes (pretcurent.ro covers it) and proves/anchors the map; the differentiator is breadth into apă/gaz/internet. Water is the most defensible because it's municipally fragmented; covered biggest-city-first with an admin manual-entry fallback for the long tail.

### Domain model — `Outage`
A single entity (renamed from the original plan's `Report`/`ScheduledOutage`) covering both planned and unplanned outages:
- `provider` — source/operator (e.g. "Aquatim", "Rețele Electrice")
- `type` — `curent | apă | gaz | internet` (v1 populates curent, apă)
- `judet`, `localitate`, `sirutaCode` — normalized geography (see Geography normalization)
- `affectedArea` — affected streets/zone kept as **raw text** (no per-provider address parsing in v1)
- `startsAt`, `endsAt` — time window; `endsAt` may be null/open-ended for avarii
- `isPlanned` — `true` = intervenție programată, `false` = avarie (unplanned)
- `source` — `scraped | manual` (admin manual entry); `user` is reserved for v2
- `isVisible` — admin soft-hide flag (hidden rows excluded from public views, never hard-deleted)
- `sourceUrl`, `rawText` — always stored for traceability and re-normalization
- `firstSeenAt`, `lastSeenAt`, `disappearedAt` — lifecycle/history stamps

**Identity / dedup:** content-hash upsert on `provider + localitate + startsAt + affectedArea`.
`endsAt` is excluded from the hash and treated as a **mutable field** on a matched record
(so extending a window updates rather than duplicates). On each crawl: existing → bump
`lastSeenAt` + update mutable fields; new → insert; vanished → stamp `disappearedAt`.

**"Active" definition:** an outage is active if `endsAt >= now` (ongoing OR upcoming);
past outages are excluded (they remain in the table for history). Open-ended avarii (null
`endsAt`) are active until they disappear from the source. The UI distinguishes "în
desfășurare acum" from "programat — [dată/oră]" and sorts ongoing-first by proximity.

### Geography normalization
Crawled place-names must reconcile to canonical Romanian geography so the map and county pages aggregate correctly.
- **Canonical dataset = SIRUTA** (official registry: 41 județe + București, municipii/orașe/comune/sate, ~13k localities with stable codes). Seed `Judet` + `Localitate` tables from it; store `sirutaCode` on each `Outage`. The map GeoJSON's 41 județe key to these same values.
- **Județ is config/structure, not text-matching.** Crawl sources are geographically scoped (Aquatim = Timiș; the PDF's "Timiș" section is explicit), so județ comes from the `CrawlerSource` config or the document's section structure. This makes the only real correctness risk — mis-countying — essentially impossible, since no fuzzy match decides the county.
- **Localitate is best-effort within the known județ, with raw-text fallback.** A shared `GeoResolver` in the pipeline (consistent, not per-crawler) tries: exact → normalized (lowercase, strip ș/ț/â/î/ă diacritics, collapse spaces/hyphens, drop Mun./Oraș/Comuna prefixes) → fuzzy (trigram/Levenshtein) above a confidence threshold against **only the localities within that județ** (a closed set, ~315 for Timiș) → else keep raw text + flag for admin review. Unresolved names are never silently guessed and never dropped — the outage still shows under the correct județ. Each admin correction becomes a permanent **alias**, which converges fast on a finite vocabulary (important because PDF extraction mangles diacritics, e.g. `Sânandrei→bnandrei`).

### Crawler framework
- A common `ICrawler` interface; each crawler contains **only** parsing logic and registers under a key (e.g. `"aquatim"`, `"retele-electrice"`).
- A shared `CrawlPipeline` performs hash-upsert/dedup, anomaly checking, and `CrawlRun` recording, so individual crawlers don't repeat persistence concerns.
- **Three crawler shapes:**
  1. *single-fetch* — one page → all rows.
  2. *date-windowed* — implements `CrawlDate(date)`; the pipeline iterates `today … today + lookaheadDays`. Default `lookaheadDays ≈ 30` (Aquatim publishes ~a month ahead). Used for calendar/per-day sources.
  3. *document-source* — discovers documents (e.g. weekly PDFs) on a listing page and ingests each once. See "Document sources" below.
- **Scheduling:** Hangfire (Postgres-backed), which also provides the ops dashboard.
- **Isolation:** each crawler run is wrapped so one failing source never stops the others.
- **Fetch/extract strategy:** plain HTTP + AngleSharp first; if the page is JS/calendar-driven (e.g. Aquatim), sniff for the underlying JSON/XHR endpoint and parse that; **PDF** via **PdfPig** (coordinate-aware extraction) for document sources; Playwright (headless browser) only as a per-source last resort. The `ICrawler` contract is unchanged across all of these — only the fetch+extract step differs; every shape produces normalized `Outage`s.
- **Crawler registry config in DB:** a `CrawlerSource` table holds operational config (url, displayName, judet, type, enabled, cadence, lookaheadDays, attribution), seeded on startup and editable from the admin panel — so a crawler can be disabled or retuned **without a redeploy**.

### Document sources (weekly PDFs)
For sources that publish immutable weekly/monthly PDFs (e.g. Rețele Electrice):
- A `CrawledDocument` ledger (stable key = filename/date-range, `contentHash`, `rowsExtracted`, `status`, `lastCheckedAt`) tracks which documents have been ingested.
- Crawl loop: fetch the **listing page** → enumerate documents by stable key (**never** the URL — PDFs sit on S3 behind presigned URLs that expire in ~1h, so the URL is not identity) → keep only documents whose date-range overlaps `[today, today + lookahead]` → for each **new or content-hash-changed** document, download via a freshly-obtained signed URL, extract (PdfPig), parse rows, **filter to the configured județe**, and upsert into `Outage`. Unchanged documents are skipped (so we don't re-crawl static files constantly).
- **Empty-PDF rule:** a document parsing to 0 rows is marked `provisional` and re-checked each run (placeholders get replaced later); once non-empty and the hash stabilizes it becomes `final` and is no longer re-downloaded. Documents whose week is current/future are re-checked even when non-empty (they can be revised); documents whose week is entirely past are frozen.

### Reliability / anti-rot
- A `CrawlRun` audit table records provider, startedAt, durationMs, rowsFound, status — doubling as crawler health history.
- **Soft-failure guard:** if a crawl returns ~0 rows when recent runs averaged many, the pipeline does NOT mark everything `disappeared`; instead it suppresses the destructive update and fires a Sentry alert. Crawlers may also declare cheap invariants (e.g. localitate non-empty). This is the main defense against silent breakage from PDF layout / HTML changes.
- Serilog for structured logging; Sentry for error tracking and alerting.

### Crawl politeness & legality
- Honor robots.txt; crawl every 30–60 min per source, one request at a time; attribute the source with a visible link.
- **User-Agent:** send a **browser-like UA** to all sources. Rationale: some sources (e.g. Aquatim) return 403 to non-browser UAs via a superficial UA-string check (not real bot detection); the data is public and robots.txt permits it. This only defeats weak UA-gating — sources with Cloudflare/JS challenges/TLS fingerprinting would need Playwright or another approach.
- **One-time permission/discovery audit:** once the full provider list is compiled, crawl every provider at least once to learn which actually permit vs block access, then revisit the final crawl policy with real data rather than assumptions.

### Frontend (Next.js + TypeScript, read-only in v1)
- **Homepage:** SVG choropleth of Romania's 41 județe via `react-simple-maps` + a RO județe GeoJSON. Built from day one, with **three** color states: **not-covered** (hatched/disabled, "extindem curând" — distinct so it never reads as "no outages"), **covered + 0 active** (calm/neutral), **covered + N active** (severity bucket). At MVP, Timiș is the only covered/highlighted county; clicking it → the county page. Leaflet is deferred to v2 (only worth it once street-level geocoding exists). A text search box is the fallback to the map.
- **Severity coloring:** absolute **bucketed thresholds** (not min-max normalization, so color has stable meaning and calm days look calm). Starting buckets (to recalibrate against real data): 0 = neutru, 1–5 = galben, 6–15 = portocaliu, 16–30 = roșu, 30+ = roșu intens.
- **County pages:** `/curent/{judet}` generated via SSG/ISR (revalidate ~10 min) for SEO ("pană curent <județ>") — the primary organic-growth lever. The map links into these. For v1 the county page is a **ranked list + search** of affected localities with outage cards (both sources); a commune-level county sub-map is a v1.x upgrade.
- **Live map data:** the homepage fetches severity **client-side** from a `/api/severity` endpoint so the heatmap is always current without rebuilds.
- **Category tabs:** curent / apă / toate (default toate) — same severity endpoint with an optional `type` filter.

### API & data freshness
- Severity counts are **derived on read** (a cached `GROUP BY judet, type WHERE active` query), NOT a maintained counter column — so they can't drift from the underlying data.
- The count updates naturally as each crawl changes the underlying outages.

### Retention
- **Keep everything forever; no pruning.** Past outages are excluded from live views by the `endsAt >= now` query, so they need no special handling — pruning would be *extra* work. Data is tiny and there is no GDPR angle (streets + public business names, no personal data). Retention enables future history/analytics (most-affected counties, provider reliability), SEO ("istoricul întreruperilor"), and audit.
- **Parsed rows and source documents are both kept — they are not substitutes.** `Outage` rows are the primary queryable data; `rawText` per row gives provenance and lets normalization (geo-matching) be re-run without the source. The **source PDF is archived once per document** (keyed by content hash in `CrawledDocument`), in **object storage / filesystem, not inline in the hot table** — because parsing is lossy/brittle, the source is the only way to recover entries the parser missed and to re-parse history after parser improvements. Sizes are negligible (~80KB/week per listing). If storage ever became a constraint, prune old *raw artifacts* while keeping all normalized rows.
- **Admin removal = soft-hide** (`isVisible` flag), never hard-delete. `disappearedAt` records are retained (an outage cancelled before its `endsAt` keeps its row + stamp; analytics can later distinguish happened vs cancelled).

### Infrastructure & CI/CD
- Backend: C# / ASP.NET Core; EF Core (Code-First, versioned migrations); PostgreSQL. PDF parsing via **PdfPig**.
- Hosting: **Hetzner VPS + Docker Compose + self-hosted Postgres** (the original plan's "Azure + Neon" line is dropped — it was a leftover). Nginx reverse proxy + SSL. Next.js runs as a node-server container (ISR requires it) behind nginx.
- **Auth in v1:** none for users. The full JWT + role system is v2 (it arrives with user accounts). The Hangfire dashboard and admin actions are protected with HTTP Basic Auth via env-var credentials.
- **CI/CD (GitHub Actions → Hetzner):** push to main → run xUnit (unit + Testcontainers integration) → build Docker images → push to **GHCR** → SSH to the VPS → run migrations → `docker compose pull && up -d`. The VPS stays lean (no build load; reproducible, rollback-able tagged images).
  - **EF migrations** run as an **explicit migration-bundle step before the new app starts** (gated, visible failures). The app does **not** auto-migrate on boot.
  - **Secrets split:** deploy secrets (SSH key, GHCR token) in GitHub Actions secrets; app secrets (Postgres password, Sentry DSN, Hangfire Basic-Auth creds) in a `.env` on the VPS (chmod 600, not in git), referenced by docker-compose. CI never sees app secrets.
  - **Topology:** API + Hangfire scheduler run as **one deployable** for v1 (the API process hosts the scheduler); split into a separate worker later only if crawl load competes with API responsiveness. Exactly one scheduler instance runs (avoid double-fired jobs).
  - **Downtime:** ~seconds on `compose up -d` is acceptable for v1; blue-green/rolling deferred.

## Testing Decisions

A good test asserts **external behavior**, not implementation details — given an input
fixture, the observable output is correct — so tests survive refactors of internal code.

Test seams, highest-first:
- **`ICrawler` parse boundary (primary seam):** each crawler is tested against **golden fixtures** — saved real source HTML/JSON/**PDF** snapshots committed to the test suite — asserting the parser extracts the expected normalized `Outage` list. Deterministic, offline, no live network. When a source changes its format, the fixture is updated. This is where most crawler logic is verified, including the Rețele Electrice PDF parser (coordinate reconstruction + closed-set locality matching).
- **`CrawlPipeline` (dedup + anomaly + lifecycle):** integration tests against a real throwaway Postgres (Testcontainers-style) using synthetic crawler outputs — assert that re-feeding the same data upserts (no duplicates), a changed `endsAt` updates in place, a vanished row gets `disappearedAt`, and a sudden 0-row result triggers the soft-failure guard rather than mass-disappearing records.
- **`CrawledDocument` ledger / document-source:** assert that an unchanged document is skipped, a changed content-hash triggers re-ingest, a 0-row document stays `provisional` and is re-checked, and out-of-window documents are ignored.
- **HTTP API endpoints:** integration tests via the ASP.NET test host (WebApplicationFactory) against a test DB — assert `/api/severity` returns correct per-județ/per-type counts honoring the "active" definition, the `type` filter, and `isVisible`.
- **Date-windowed crawl driving:** assert the pipeline iterates the configured `lookaheadDays` and calls `CrawlDate` for each date.
- **`GeoResolver`:** table-driven tests over a fixed SIRUTA subset — assert that diacritic/spacing/prefix variants (including PDF-mangled forms) resolve to the right locality within a given județ, that below-threshold names fall back to raw text (not mis-matched), that aliases override fuzzy matching, and that județ is taken from config (never overridden by a fuzzy localitate match).

Unit + integration tests run in xUnit. Frontend testing in v1 is limited to smoke-level
checks; deep UI testing is out of scope.

## Out of Scope

- User accounts, registration/login, JWT, roles — **v2**.
- User-submitted reports and confirmations (the community/credibility loop) — **v2**.
- Street-level geocoding and a Leaflet pin-map — **v2** (v1 keeps affected streets as raw text and uses an SVG choropleth).
- National coverage — the **initial MVP is Timiș only**; other counties are "not covered yet" on the map and filled in afterward.
- Gas and internet crawlers — later (MVP is curent + apă in Timiș).
- Full water coverage of all municipal operators — later (biggest-city-first; long tail via admin manual entry / future user tips).
- A commune-level county sub-map — **v1.x** (v1 county page is list + search).
- History/analytics features and dashboards — **v2+** (data is retained from day one so they're buildable later, but no UI in v1).
- LLM-based generic extraction for messy long-tail sources — a later experiment, never the v1 core.
- OCR-based PDF extraction (Tesseract) — documented fallback only, for a future source with both broken diacritics and an open/unknown locality set.
- Push/email notifications/alerts to end users — later.
- Mobile native apps.
- Zero-downtime/blue-green deploys, multi-instance scaling.
- Rich frontend test coverage.

## Further Notes

- **First targets shape the schema (ground-truthed 2026-06-09):**
  - **Aquatim** (apă, Timiș): a date-driven calendar widget that loads avarii + intervenții programate per selected date via XHR, exposing data ~a month ahead; robots.txt permits the page; it is only UA-gated (browser UA → 200). → *date-windowed* crawler; sniff the per-date JSON endpoint (likely no Playwright).
  - **Rețele Electrice** (curent, ex-E-Distribuție Banat; PPC): the old `e-distributie.com/...banat.aspx` is dead. Live listing is `reteleelectrice.ro/intreruperi/programate/` — **static HTML with all PDF links in the DOM** (accordion is cosmetic, no Playwright). PDFs are **weekly and national** (`Intreruperi programate DD.MM.YYYY - DD.MM.YYYY.pdf`, Mon–Sun), covering the whole ex-Enel footprint, with a TOC by județ — **Timiș is a section inside the PDF, not a separate file**. PDFs are on **AWS S3 with presigned URLs (~1h expiry)** → identity is the filename/date-range, not the URL. PDF columns: Data, Localitate, affected-area/details (streets + `Alte detalii: Total/Partial` + `Agenți economici:`), time window. `pdftotext` destroys diacritics (font lacks ToUnicode) → use PdfPig coordinate extraction + closed-set locality matching + alias table. → *document-source* crawler.
- **Brand/launch framing:** launching as "deranjamente.com — Timiș (curent + apă)" is credible and validatable; better to ship one county that fully works than a thin national slice.
- **Definition of Done (Timiș MVP, revised from plan-initial.md):** API with outage + severity endpoints; Aquatim + Rețele Electrice crawlers working for Timiș (date-windowed + document-source shapes); SIRUTA-seeded geo tables + `GeoResolver`; EF Core migrations (bundle-deployed); Hangfire scheduling + dashboard (Basic-Auth); `CrawlRun` health + soft-failure alerting; `CrawledDocument` ledger; Sentry + Serilog; xUnit unit + integration tests (fixture-based crawlers incl. PDF); Next.js frontend (SVG map with not-covered state + Timiș county list/search page, SSG/ISR) deployed and wired to the API; GitHub Actions CI/CD to GHCR + Hetzner; live on Hetzner; technical README. (Dropped from the original DoD: user reports, JWT/roles, Azure + Neon, "all distributors".)
