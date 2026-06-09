# Slice 8 — Admin surface (Basic Auth + panel) (AFK)

## Parent
#1

## What to build
The operational surface for running the site, protected by HTTP Basic Auth (env-var credentials) — no user auth in v1.

Protect `/hangfire` and all admin endpoints with Basic Auth. Build an admin panel that lets the operator:
- view/toggle `CrawlerSource` enable/disable and retune cadence + `lookaheadDays` **without a redeploy**;
- manually add an outage (`source = manual`) for sources with no scrapable page, visibly distinguished by provenance;
- soft-hide an erroneous/garbage outage via the `isVisible` flag (never hard-delete, reversible);
- work the **geo-review queue**: localities the `GeoResolver` couldn't confidently match, resolving each to a canonical locality (which becomes a permanent alias).

The Hangfire dashboard + `CrawlRun` history serve as the crawler-health view (last run, row count, status).

## Acceptance criteria
- [ ] `/hangfire` and admin endpoints require Basic Auth (env-var creds); unauthenticated access is rejected
- [ ] Admin can enable/disable a crawler and change cadence/lookaheadDays at runtime (no redeploy); changes take effect
- [ ] Admin can add a manual outage (`source=manual`); it shows with clear provenance
- [ ] Admin can soft-hide an outage (`isVisible=false`); hidden rows disappear from public views but remain in the DB and are reversible
- [ ] Geo-review queue lists unresolved localities; resolving one creates a reusable alias and links the affected outages
- [ ] Crawler health (last run, rows, status) is visible via Hangfire dashboard / CrawlRun history
- [ ] After implementation, create a commit with a descriptive message

## Blocked by
- #3
- #6
