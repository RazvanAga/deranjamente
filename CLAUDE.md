# deranjamente.com

A "Downdetector for Romania" — a crawl-based aggregator of utility outages (planned
*intervenții programate* and unplanned *avarii*) for electricity, water, gas, internet.
Currently **greenfield**: planning is done, implementation is starting.

## Status & where the canonical specs live

- **PRD:** [docs/PRD.md](docs/PRD.md) (also GitHub issue #1). The single source of truth for v1 scope and decisions.
- **Work is sliced into GitHub issues #2–#10** (tracer-bullet vertical slices), with full specs also in [docs/issues/](docs/issues/).
- **Decisions were grilled in depth** and also recorded in the project's auto-memory (see the memory `MEMORY.md` index) — trust the PRD/issues first; memory mirrors them.

## v1 = Timiș MVP

v1 is **crawl-only** (no user accounts/reports/auth — that's v2). The MVP covers **one
county, Timiș**, with two real sources, then expands county-by-county, then to gas/internet:
- **Aquatim** (apă) and **Rețele Electrice** (curent, ex-E-Distribuție Banat).

## Implementation order (critical path)

`#2 → #3 → {#4, #5, #6} → #7`. After #3 (pipeline), slices #4/#5/#6/#8 can run in parallel.
#10 (deploy) can start once #2 lands and is the only **HITL** slice (needs infra/DNS/secrets).
Implement one issue at a time; **commit after each** with a descriptive message.

## Tech stack

- **Backend:** C# / ASP.NET Core, EF Core (Code-First, versioned migrations), PostgreSQL.
- **Crawling:** common `ICrawler` + shared `CrawlPipeline`, scheduled by **Hangfire**. PDF via **PdfPig**. HTTP+AngleSharp / JSON-XHR / PDF / Playwright (last resort).
- **Frontend:** Next.js + TypeScript; SVG choropleth via `react-simple-maps`.
- **Infra:** Hetzner VPS + Docker Compose + self-hosted Postgres; nginx + SSL; GitHub Actions → GHCR → SSH deploy.
- **Obs:** Serilog + Sentry. **Tests:** xUnit + Testcontainers; crawlers tested via **golden fixtures** (saved HTML/JSON/PDF), never live-hitting sites.

## Key locked decisions (see PRD for full rationale)

- One `Outage` entity for planned+unplanned (`isPlanned`); `source` = scraped|manual (user=v2); soft-hide via `isVisible` (never hard-delete).
- **Dedup:** content-hash upsert on `provider + localitate + startsAt + affectedArea`; `endsAt` mutable; track firstSeen/lastSeen/disappearedAt.
- **"Active"** = `endsAt >= now` (ongoing + upcoming); past kept for history, excluded from live views.
- **Geo:** canonical **SIRUTA** tables; **județ comes from config/structure, never fuzzy-matched** (so mis-countying is impossible); localitate resolved within the known județ via exact→normalized→fuzzy→raw-fallback + an alias table.
- **Severity:** derived on read (cached `GROUP BY`), absolute bucket colors, map has a distinct **not-covered** state.
- **Retention:** keep everything; archive each source PDF once off the hot table.

## Ground-truthed gotchas (verified against the live sources — don't re-learn the hard way)

- **All sources:** send a **browser-like User-Agent**. Some (e.g. Aquatim) 403 non-browser UAs via a superficial check. Data is public; robots.txt permits it.
- **Aquatim:** a date-driven calendar widget; outages load **per selected date via XHR JSON** (not in static HTML). Crawl as a *date-windowed* source (`lookaheadDays`≈30); sniff the per-date endpoint. Exposes both avarii + intervenții programate.
- **Rețele Electrice:** old `e-distributie.com` is dead. Use `reteleelectrice.ro/intreruperi/programate/` — **static HTML with all PDF links in the DOM** (accordion is cosmetic; no Playwright). PDFs are **weekly + national** (one section per județ; Timiș is a *section inside the PDF*), on **AWS S3 presigned URLs that expire ~1h** → identity = filename/date-range, **never the URL**; fetch a fresh signed URL from the listing at download time. `pdftotext` **destroys diacritics** (font lacks ToUnicode, e.g. `Sânandrei→bnandrei`) → use **PdfPig coordinate extraction** + closed-set locality matching + alias table. Crawl as a *document-source* (see #5/#4... i.e. issue #5 spec).

## Conventions

- Domain vocabulary is **Romanian** (deranjament, avarie, intervenție programată, județ, localitate, curent, apă). Use it in code/UI/issues.
- Two flagged follow-ups before locking crawl policy / shipping: (1) a one-time discovery crawl of all providers to learn who permits/blocks; (2) recalibrate severity buckets against real data.
