# PRD — deranjamente.com v1 (Outage Aggregator / Crawling)

> Status: draft. v1 is **crawl-only**. User-generated reports, confirmations, and the
> JWT/role auth system are explicitly deferred to **v2**.

## Problem Statement

When a Romanian's water, electricity, gas, or internet goes out — or is about to be cut
for planned maintenance — there is no single place to check "is my area affected, right
now or soon?" The information *does* exist, but it is scattered across dozens of utility
providers' websites, each with its own layout, its own calendar widget, its own PDF, and
no common format. A person who wants to know whether the water will be off on their street
tomorrow has to know who their operator is, find their site, and dig through an awkward
page. There is no national, cross-utility, cross-provider view. The closest competitor
(pretcurent.ro) covers only electricity.

## Solution

deranjamente.com crawls Romanian utility providers automatically, normalizes their
heterogeneous outage data into one shape, and presents it as a single, fast, map-first
website. A visitor lands on an interactive map of Romania's 40 județe colored by how many
outages are active in each, clicks their județ (or searches for their localitate), and
sees the planned interventions and unplanned faults affecting their area — with the time
window and a link back to the official source.

v1 proves the crawling pipeline end-to-end on electricity (table stakes) plus one water
operator (Aquatim, Timiș). The durable advantage is breadth into the categories nobody
aggregates — water, gas, internet — starting with water, which is municipally fragmented
and therefore the most defensible.

## User Stories

1. As a resident, I want to see a map of Romania colored by outage severity, so that I can immediately tell where problems are concentrated.
2. As a resident, I want to click my județ on the map, so that I can see outages affecting my area without scrolling a list of 40 counties.
3. As a resident, I want to search for my localitate by name, so that I can find my area even if I don't want to use the map.
4. As a resident, I want to see both planned interventions (intervenții programate) and unplanned faults (avarii), so that I know about both scheduled cut-offs and emergencies.
5. As a resident, I want each outage to show the affected streets/zone as text, so that I can tell whether my specific street is impacted.
6. As a resident, I want each outage to show its start and (where known) end time, so that I can plan around it.
7. As a resident, I want outages that are happening now to be clearly distinguished from ones scheduled for later, so that I understand urgency at a glance.
8. As a resident, I want to filter by utility type (curent / apă / toate), so that I can focus on the service I care about.
9. As a resident, I want a link back to the official provider page for each outage, so that I can verify the information at the source.
10. As a resident, I want to see which provider and when the data was last updated, so that I can trust the freshness of what I'm reading.
11. As a resident, I want the county pages to load fast and be findable on Google (e.g. "pană curent Cluj"), so that I can reach the right page directly from a search.
12. As a resident on mobile, I want the map and lists to work on a small screen, so that I can check outages from my phone during one.
13. As an operator of the site (admin), I want a dashboard showing each crawler's last run, row count, and status, so that I can tell at a glance whether crawling is healthy.
14. As an admin, I want to be alerted when a crawler that usually returns data suddenly returns nothing, so that I learn about a broken scraper before users do.
15. As an admin, I want to enable or disable a specific crawler without redeploying, so that I can take a broken source offline immediately.
16. As an admin, I want to retune a crawler's cadence and look-ahead horizon without redeploying, so that I can adapt to a source's behavior.
17. As an admin, I want to manually add an outage for a source that has no scrapable page, so that coverage isn't gated on a crawler existing.
18. As an admin, I want manually-entered outages to be visibly distinguished from scraped ones (provenance), so that the data's origin is auditable.
19. As an admin, I want the operational dashboard and admin actions protected by a login, so that they aren't publicly accessible.
20. As an admin, I want a historical record of each crawl run, so that I can investigate when and why a source stopped returning data.
21. As a developer, I want to add a new provider by writing a single parse method against a common interface, so that scaling coverage is cheap and repeatable.
22. As a developer, I want crawler parsing tested against saved real-world HTML/JSON fixtures, so that tests are deterministic and run offline.
23. As a developer, I want one source breaking to never stop the others from crawling, so that the system is resilient.
24. As a developer, I want the dedup logic to recognize an outage already seen on a previous crawl, so that the database doesn't fill with duplicates.
25. As a developer, I want a changed end-time on a known outage to update the existing record rather than create a new one, so that history stays clean.
26. As a developer, I want sources that publish data per-calendar-day to be crawled across a forward window of dates, so that upcoming outages are captured even when the source only answers one day at a time.
27. As a developer, I want errors and structured logs captured centrally, so that I can debug crawler failures.

## Implementation Decisions

### Scope
- v1 = **crawl-only**. No user accounts, no user-submitted reports, no confirmations. Those are v2.
- v1 targets: the major **electricity distributors** (single-fetch pages) + **Aquatim** (water, Timiș) as the first water crawler. Water expands biggest-city-first afterward.
- Electricity is table stakes (competitor pretcurent.ro already covers it); the differentiator is breadth into apă/gaz/internet. Water is the most defensible because it's municipally fragmented.

### Domain model — `Outage`
A single entity (renamed from the original plan's `Report`/`ScheduledOutage`) covering both planned and unplanned outages:
- `provider` — source/operator (e.g. "Aquatim", "Distribuție Oltenia")
- `type` — `curent | apă | gaz | internet` (v1 populates curent, apă)
- `judet`, `localitate` — normalized geography
- `affectedArea` — affected streets/zone kept as **raw text** (no per-provider address parsing in v1)
- `startsAt`, `endsAt` — time window; `endsAt` may be null/open-ended for avarii
- `isPlanned` — `true` = intervenție programată, `false` = avarie (unplanned)
- `source` — `scraped | manual` (admin manual entry); `user` is reserved for v2
- `sourceUrl`, `rawText` — always stored for traceability and later re-parsing
- `firstSeenAt`, `lastSeenAt`, `disappearedAt` — lifecycle/history stamps

**Identity / dedup:** content-hash upsert on `provider + localitate + startsAt + affectedArea`.
`endsAt` is excluded from the hash and treated as a **mutable field** on a matched record
(so extending a window updates rather than duplicates). On each crawl: existing → bump
`lastSeenAt` + update mutable fields; new → insert; vanished → stamp `disappearedAt`.

**"Active" definition:** an outage is active if `endsAt >= now` (ongoing OR upcoming);
past outages are excluded. Open-ended avarii (null `endsAt`) are active until they
disappear from the source. The UI distinguishes "în desfășurare acum" from
"programat — [dată/oră]" and sorts ongoing-first by proximity.

### Crawler framework
- A common `ICrawler` interface; each crawler contains **only** parsing logic and registers under a key (e.g. `"aquatim"`).
- A shared `CrawlPipeline` performs hash-upsert/dedup, anomaly checking, and `CrawlRun` recording, so individual crawlers don't repeat persistence concerns.
- **Two crawler shapes:** *single-fetch* (one page → all rows; most electricity tables) and *date-windowed* (implements `CrawlDate(date)`; the pipeline iterates `today … today + lookaheadDays`). Default `lookaheadDays ≈ 30` (Aquatim publishes ~a month ahead).
- **Scheduling:** Hangfire (Postgres-backed), which also provides the ops dashboard.
- **Isolation:** each crawler run is wrapped so one failing source never stops the others.
- **Fetch strategy:** plain HTTP + AngleSharp first; if the page is JS/calendar-driven (e.g. Aquatim), sniff for the underlying JSON/XHR endpoint and parse that; Playwright (headless browser) only as a per-source last resort. Aquatim specifically loads outages per selected date via an XHR — find that endpoint.
- **Crawler registry config in DB:** a `CrawlerSource` table holds operational config (url, displayName, judet, type, enabled, cadence, lookaheadDays, attribution), seeded on startup and editable from the admin panel — so a crawler can be disabled or retuned **without a redeploy**.

### Reliability / anti-rot
- A `CrawlRun` audit table records provider, startedAt, durationMs, rowsFound, status — doubling as crawler health history.
- **Soft-failure guard:** if a crawl returns ~0 rows when recent runs averaged many, the pipeline does NOT mark everything `disappeared`; instead it suppresses the destructive update and fires a Sentry alert. Crawlers may also declare cheap invariants (e.g. localitate non-empty).
- Serilog for structured logging; Sentry for error tracking and alerting.

### Crawl politeness & legality
- Honor robots.txt; crawl every 30–60 min per source, one request at a time; attribute the source with a visible link.
- **User-Agent:** send a **browser-like UA** to all sources. Rationale: some sources (e.g. Aquatim) return 403 to non-browser UAs via a superficial UA-string check (not real bot detection); the data is public and robots.txt permits it. This only defeats weak UA-gating — sources with Cloudflare/JS challenges/TLS fingerprinting would need Playwright or another approach.
- **One-time permission/discovery audit:** once the full provider list is compiled, crawl every provider at least once to learn which actually permit vs block access, then revisit the final crawl policy with real data rather than assumptions.

### Frontend (Next.js + TypeScript, read-only in v1)
- **Homepage:** SVG choropleth of Romania's 40 județe via `react-simple-maps` + a RO județe GeoJSON, colored by active-outage severity; click a județ → its page. Leaflet is deferred to v2 (only worth it once street-level geocoding exists). A text search box is the fallback to the map.
- **Severity coloring:** absolute **bucketed thresholds** (not min-max normalization, so color has stable meaning and calm days look calm). Starting buckets (to recalibrate against real data): 0 = neutru, 1–5 = galben, 6–15 = portocaliu, 16–30 = roșu, 30+ = roșu intens.
- **County pages:** `/curent/{judet}` generated via SSG/ISR (revalidate ~10 min) for SEO ("pană curent <județ>") — the primary organic-growth lever. The map links into these.
- **Live map data:** the homepage fetches severity **client-side** from a `/api/severity` endpoint so the heatmap is always current without rebuilds.
- **Category tabs:** curent / apă / toate (default toate) — same severity endpoint with an optional `type` filter.

### API & data freshness
- Severity counts are **derived on read** (a cached `GROUP BY judet, type WHERE active` query), NOT a maintained counter column — so they can't drift from the underlying data.
- The count updates naturally as each crawl changes the underlying outages.

### Infrastructure
- Backend: C# / ASP.NET Core; EF Core (Code-First, versioned migrations); PostgreSQL.
- Hosting: **Hetzner VPS + Docker Compose + self-hosted Postgres** (the original plan's "Azure + Neon" line is dropped — it was a leftover). Nginx reverse proxy + SSL. GitHub Actions → SSH deploy.
- **Auth in v1:** none for users. The full JWT + role system is v2 (it arrives with user accounts). The Hangfire dashboard and admin actions are protected with HTTP Basic Auth via env-var credentials.

## Testing Decisions

A good test asserts **external behavior**, not implementation details — given an input
fixture, the observable output is correct — so tests survive refactors of internal code.

Test seams, highest-first:
- **`ICrawler` parse boundary (primary seam):** each crawler is tested against **golden fixtures** — saved real source HTML/JSON snapshots committed to the test suite — asserting the parser extracts the expected normalized `Outage` list. Deterministic, offline, no live network. When a source changes its markup, the fixture is updated. This is where most crawler logic is verified.
- **`CrawlPipeline` (dedup + anomaly + lifecycle):** integration tests against a real throwaway Postgres (Testcontainers-style) using synthetic crawler outputs — assert that re-feeding the same data upserts (no duplicates), a changed `endsAt` updates in place, a vanished row gets `disappearedAt`, and a sudden 0-row result triggers the soft-failure guard rather than mass-disappearing records.
- **HTTP API endpoints:** integration tests via the ASP.NET test host (WebApplicationFactory) against a test DB — assert `/api/severity` returns correct per-județ/per-type counts honoring the "active" definition and the `type` filter.
- **Date-windowed crawl driving:** assert the pipeline iterates the configured `lookaheadDays` and calls `CrawlDate` for each date.

Unit + integration tests run in xUnit. Frontend testing in v1 is limited to smoke-level
checks; deep UI testing is out of scope.

## Out of Scope

- User accounts, registration/login, JWT, roles — **v2**.
- User-submitted reports and confirmations (the community/credibility loop) — **v2**.
- Street-level geocoding and a Leaflet pin-map — **v2** (v1 keeps affected streets as raw text and uses an SVG choropleth).
- Gas and internet crawlers — later (v1 is electricity + Aquatim water).
- Full water coverage of all municipal operators — later (biggest-city-first; long tail via admin manual entry / future user tips).
- LLM-based generic extraction for messy long-tail sources — a later experiment, never the v1 core.
- Push/email notifications/alerts to end users — later.
- Mobile native apps.
- Rich frontend test coverage.

## Further Notes

- **First targets shape the schema:** Aquatim (water) is a date-driven calendar widget that
  loads avarii + intervenții programate per selected date via XHR, exposing data ~a month
  ahead; robots.txt permits it; it is only UA-gated. The electricity distributors are mostly
  static or XHR-backed tables and publish only planned întreruperi (no `avarii` section).
- **Brand/launch framing:** launching as "deranjamente.com — curent + apă" is credible;
  better to ship two categories that work than four that half-work.
- **Definition of Done (revised from plan-initial.md):** API with outage + severity endpoints;
  electricity distributors + Aquatim crawlers working; EF Core migrations; Hangfire scheduling
  + dashboard (Basic-Auth); `CrawlRun` health + soft-failure alerting; Sentry + Serilog;
  xUnit unit + integration tests (fixture-based crawlers); Next.js frontend (SVG map + SSG
  county pages) deployed and wired to the API; GitHub Actions CI/CD; live on Hetzner;
  technical README. (Dropped from the original DoD: user reports, JWT/roles, Azure + Neon.)
