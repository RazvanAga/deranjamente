# Slice 3 — Aquatim crawler (apă, date-windowed) (AFK)

## Parent
#1

## What to build
The first real crawler — Aquatim (water, Timiș) — using the date-windowed crawler shape, delivering real Timiș water outages to the site.

Aquatim's outage page is a date-driven calendar widget that loads data per selected date via an XHR; sniff and call that JSON endpoint directly (avoid Playwright if possible). Implement a date-windowed crawler that iterates `today … today + lookaheadDays` (≈30) and parses both sections the source exposes: **avarii** (unplanned, `isPlanned=false`) and **intervenții programate** (planned, `isPlanned=true`). Use a browser-like User-Agent, honor robots.txt, and store source attribution + `rawText`.

The pipeline's date-loop drives `CrawlDate(date)` for date-windowed crawlers. Open-ended avarii (no firm `endsAt`) remain active until they disappear from the source.

## Acceptance criteria
- [ ] Aquatim crawler registered as a date-windowed source; pipeline iterates the configured `lookaheadDays` calling `CrawlDate(date)`
- [ ] Parses both avarii and intervenții programate, setting `isPlanned` correctly
- [ ] Each outage has provider, judet=Timiș, localitate, affectedArea (raw), startsAt/endsAt, sourceUrl, rawText
- [ ] Browser-like UA used; robots.txt respected; attribution stored
- [ ] Golden JSON fixtures committed; parser tests assert expected normalized `Outage`s offline
- [ ] Real Timiș water outages appear via the API and on the web page
- [ ] After implementation, create a commit with a descriptive message

## Blocked by
- #3
