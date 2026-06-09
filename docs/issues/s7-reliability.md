# Slice 7 — Reliability: soft-failure guard + Sentry + Serilog (AFK)

## Parent
#1

## What to build
Defenses against silent crawler rot, plus observability.

Add a **soft-failure guard** to the `CrawlPipeline`: when a crawl returns ~0 rows while recent `CrawlRun`s for that source averaged many, do NOT mark everything `disappeared` — suppress the destructive update and raise an alert instead (the most embarrassing silent failure). Let crawlers declare cheap invariants (e.g. localitate non-empty) that the pipeline enforces. Wire **Serilog** structured logging and **Sentry** error tracking + alerting so both hard failures (exceptions) and soft failures (anomalies) surface.

## Acceptance criteria
- [ ] A crawl returning ~0 rows when recent runs averaged many is treated as suspect: destructive `disappeared` updates suppressed
- [ ] Soft-failure and hard-failure both raise a Sentry alert
- [ ] Crawler-declared invariants are enforced by the pipeline; violations are logged/alerted, not silently stored
- [ ] Serilog structured logging in place across API + crawl pipeline
- [ ] Sentry integrated (DSN via env/`.env`), capturing unhandled exceptions
- [ ] Tests assert the anomaly guard suppresses mass-disappearance on a 0-row crawl
- [ ] After implementation, create a commit with a descriptive message

## Blocked by
- #3
