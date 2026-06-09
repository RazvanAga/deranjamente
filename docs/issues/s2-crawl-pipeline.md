# Slice 2 — Crawl pipeline spine + Hangfire (AFK)

## Parent
#1

## What to build
The crawl pipeline spine that all real crawlers will plug into, proven end-to-end with one sample/stub crawler whose rows flow through to the existing API/UI.

Introduce a common `ICrawler` interface (a crawler returns normalized `Outage`s and contains only parsing logic), a shared `CrawlPipeline` that performs content-hash upsert/dedup and lifecycle stamping, a `CrawlerSource` config table, a `CrawlRun` audit table, and Hangfire scheduling (Postgres-backed) with its dashboard. Each crawler run is isolated so one failing crawler never stops the others.

Identity/dedup (per PRD): content-hash on `provider + localitate + startsAt + affectedArea`; `endsAt` excluded from the hash and treated as a mutable field on a matched record. On each crawl: existing → bump `lastSeenAt` + update mutable fields; new → insert; vanished → stamp `disappearedAt`.

Prove it with a single sample crawler returning a couple of fixed outages (registered under a key, scheduled by Hangfire). This sample is replaced by the real Aquatim/Rețele Electrice crawlers in #4 and #5.

## Acceptance criteria
- [ ] `ICrawler` interface; a crawler registers under a key and returns normalized `Outage`s
- [ ] `CrawlPipeline` upserts by content hash: re-running the same crawl produces no duplicates
- [ ] A changed `endsAt` on a known outage updates the existing row (no new row)
- [ ] An outage that vanishes from a crawl gets `disappearedAt` stamped
- [ ] `CrawlerSource` table (url, displayName, judet, type, enabled, cadence, lookaheadDays, attribution) seeded on startup
- [ ] `CrawlRun` audit rows recorded (provider, startedAt, durationMs, rowsFound, status)
- [ ] Hangfire scheduling runs the sample crawler; one crawler throwing does not stop others
- [ ] Sample crawler output is visible via the existing `GET /api/outages` and the web page
- [ ] xUnit/Testcontainers tests cover upsert/dedup, endsAt mutation, and disappearance
- [ ] After implementation, create a commit with a descriptive message

## Blocked by
- #2
