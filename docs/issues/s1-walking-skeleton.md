# Slice 1 — Walking skeleton: Outage → API → web (AFK)

## Parent
#1

## What to build
The end-to-end walking skeleton that wires every layer together, plus initial project scaffolding and dependency installation. Bootstrap the repo: an ASP.NET Core API solution and a Next.js + TypeScript app, with all base dependencies installed (NuGet + npm) and a local `docker-compose.yml` running api + web + PostgreSQL.

Define the `Outage` entity and its first EF Core migration, expose a single read endpoint `GET /api/outages?judet=` that returns rows from the DB, and render a minimal Next.js page that fetches and lists Timiș outages. Manually seed one `Outage` row so the whole path (DB → API → UI) is demoable without any crawler yet.

`Outage` fields (per PRD): provider, type, judet, localitate, sirutaCode (nullable for now), affectedArea (raw text), startsAt, endsAt (nullable), isPlanned, source (scraped|manual), isVisible, sourceUrl, rawText, firstSeenAt, lastSeenAt, disappearedAt.

## Acceptance criteria
- [ ] .NET solution (ASP.NET Core API) and Next.js + TS app scaffolded; all dependencies installed and building (NuGet restore + npm install)
- [ ] `docker-compose.yml` brings up api + web + PostgreSQL locally with one command
- [ ] `Outage` entity + initial EF Core migration; schema applies to a fresh Postgres
- [ ] `GET /api/outages?judet=Timiș` returns seeded outage(s) as JSON
- [ ] Next.js page fetches the endpoint and lists Timiș outages (provider, localitate, time window, source link)
- [ ] xUnit integration test (Testcontainers Postgres) asserts the endpoint returns a seeded outage
- [ ] After implementation, create a commit with a descriptive message (e.g. "Walking skeleton: Outage → API → web")

## Blocked by
None - can start immediately
