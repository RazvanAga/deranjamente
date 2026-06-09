# Slice 6 — Map homepage + county page + /api/severity (AFK)

## Parent
#1

## What to build
The real user-facing experience: the national severity map and the county page, replacing slice 1's minimal placeholder page.

API: `GET /api/severity` returning per-județ (and per-`type`) active-outage counts, **derived on read** via a cached `GROUP BY judet, type WHERE active` query (no maintained counter), honoring the "active" definition (`endsAt >= now`) and `isVisible`.

Homepage: an SVG choropleth of Romania's 41 județe (`react-simple-maps` + RO județe GeoJSON) with **three color states** — not-covered (hatched/disabled, "extindem curând"), covered+0 (calm), covered+N (absolute severity buckets: 0=neutru, 1-5=galben, 6-15=portocaliu, 16-30=roșu, 30+=roșu intens). Timiș is the only covered county at MVP and is focused/highlighted; clicking a covered județ → its county page. Severity fetched client-side so the map is always current. A text search box is the fallback to the map.

County page: `/curent/{judet}` via SSG/ISR (revalidate ~10 min, for SEO like "pană curent Timiș"), a ranked + searchable list of affected localities with outage cards showing time window, affected area, "Sursă: X" link, and an "în desfășurare acum" vs "programat — [dată/oră]" badge (ongoing-first). Category tabs: curent / apă / toate (default toate).

## Acceptance criteria
- [ ] `GET /api/severity` returns correct per-județ/per-type active counts (derived, cached), respecting `endsAt >= now` and `isVisible`; `type` filter works
- [ ] SVG choropleth renders 41 județe with the three distinct color states; not-covered is visually distinct from covered+0
- [ ] Map severity is fetched client-side and reflects current data; clicking a covered județ navigates to its county page
- [ ] `/curent/{judet}` is SSG/ISR (revalidate ~10 min), searchable, ranked, with outage cards + acum/programat badges
- [ ] Category tabs curent/apă/toate filter via the severity/listing endpoints; default toate
- [ ] Works on mobile
- [ ] After implementation, create a commit with a descriptive message

## Blocked by
- #6
