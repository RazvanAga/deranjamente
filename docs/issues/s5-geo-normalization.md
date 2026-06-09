# Slice 5 — Geography normalization (SIRUTA + GeoResolver) (AFK)

## Parent
#1

## What to build
Canonical geography so the map and county pages aggregate correctly and crawled place-names reconcile reliably.

Seed `Judet` + `Localitate` tables from the **SIRUTA** dataset (41 județe + București, ~13k localities with stable codes) and obtain a RO județe GeoJSON whose keys match the județe. Implement a shared `GeoResolver` in the pipeline that resolves each crawled localitate **within its known județ** (județ itself comes from `CrawlerSource`/document structure — never decided by fuzzy match): exact → normalized (lowercase, strip ș/ț/â/î/ă, collapse spaces/hyphens, drop Mun./Oraș/Comuna prefixes) → fuzzy (trigram/Levenshtein) above a confidence threshold against the closed set for that județ → else keep raw text + flag for admin review. Set `sirutaCode` on resolved outages. Maintain an alias table (admin corrections become permanent aliases). This must cope with PDF-mangled diacritics (e.g. `Sânandrei→bnandrei`).

*(Includes sourcing the SIRUTA dataset + RO județe GeoJSON.)*

## Acceptance criteria
- [ ] `Judet` + `Localitate` tables seeded from SIRUTA; RO județe GeoJSON sourced and keyed to the same județe
- [ ] `GeoResolver` resolves localitate within the known județ; județ is never overridden by a fuzzy localitate match
- [ ] Resolution ladder works: exact, normalized (diacritic/spacing/prefix), fuzzy-above-threshold; below-threshold → raw text + flagged, never mis-matched or dropped
- [ ] Alias table consulted before fuzzy; an alias forces a specific resolution
- [ ] `sirutaCode` set on resolved outages; the outage always shows under the correct județ even when localitate is unresolved
- [ ] Table-driven tests over a fixed SIRUTA subset, including PDF-mangled forms
- [ ] After implementation, create a commit with a descriptive message

## Blocked by
- #3
