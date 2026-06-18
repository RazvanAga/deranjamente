# deranjamente.com

Un *„Downdetector pentru România”* — un agregator, bazat pe crawling, al întreruperilor de
utilități (*intervenții programate* și *avarii* neplanificate) pentru **curent** și **apă**,
prezentat ca un singur site rapid, centrat pe hartă. Vizitatorul ajunge pe o hartă coropletă
a celor 41 de județe colorate după severitatea întreruperilor active, dă click pe județul lui
(sau caută localitatea) și vede întreruperile care îi afectează zona, cu intervalele de timp
și linkuri către sursa oficială.

**v1 este doar crawling** (fără conturi/sesizări/autentificare — acelea sunt v2) și se lansează
ca un **MVP axat pe Timiș**, peste două surse reale: **Aquatim** (apă) și **Rețele Electrice**
(curent, ex-E-Distribuție Banat). Apoi se extinde județ cu județ și către gaz/internet.

Sursa unică de adevăr pentru scop și decizii este **[docs/PRD.md](docs/PRD.md)**; munca este
împărțită în felii verticale de tip *tracer-bullet*, în **[docs/issues/](docs/issues/)**.

## Status

Feliile 1–6 sunt finalizate (vezi istoricul git): scheletul funcțional, pipeline-ul de crawl +
Hangfire, crawlerele Aquatim și Rețele Electrice, normalizarea geografică SIRUTA, plus harta +
pagina de județ + `/api/severity`. Rămase: consolidarea fiabilității/anti-rot (#7), panoul de
admin (#8) și CI/CD + deploy (#9).

## Tech Stack

| Strat        | Alegere |
|--------------|---------|
| Backend      | C# / ASP.NET Core (minimal APIs, .NET 10), EF Core code-first + migrații versionate |
| Bază de date | PostgreSQL 17 |
| Crawling     | `ICrawler` comun + `CrawlPipeline` partajat, programat de **Hangfire** (peste Postgres). HTTP + **AngleSharp**, JSON-XHR și extragere coordonată din PDF cu **PdfPig** |
| Frontend     | Next.js 16 + TypeScript; hartă coropletă SVG randată printr-un pipeline `d3-geo` + `topojson` |
| Infra (v1)   | VPS Hetzner + Docker Compose + Postgres self-hosted; nginx + SSL; GitHub Actions → GHCR → deploy prin SSH |
| Obs / teste  | Serilog + Sentry; xUnit, crawlerele testate cu **golden fixtures** (HTML/JSON/PDF salvate), niciodată live pe site |

## Structura repo-ului

```
src/Deranjamente.Api/        API ASP.NET Core + crawlere + scheduler Hangfire (un singur deployable)
  Crawling/                  ICrawler, CrawlPipeline, cele trei tipuri de crawler
    ReteleElectrice/         crawler document-source PDF (listing → PDF săptămânal național)
    Documents/               registrul CrawledDocument, descărcare PDF + arhivare-o-dată-după-hash
    Pdf/                     extragere de cuvinte conștientă de coordonate, cu PdfPig
  Data/                      AppDbContext, migrații EF, CSV-uri SIRUTA + seedere
  Domain/                    Outage, CrawlerSource, CrawlRun, CrawledDocument, Geo
  Geo/                       GeoNormalize + GeoResolver (județ din config; localitate fuzzy)
  Program.cs                 configurarea DI + endpointurile /api/outages și /api/severity
src/web/                     frontend Next.js (harta de pe homepage + paginile /curent/{judet})
tests/Deranjamente.Api.Tests/  teste xUnit unit + integrare; Fixtures/ golden snapshots
docs/                        PRD.md + specificațiile feliilor (issues)
docker-compose.yml           stiva de dezvoltare locală (db + api + web)
```

## Rulare locală

Calea cea mai rapidă este stiva Docker Compose, care aplică migrațiile, populează geografia +
configul crawlerelor, pornește scheduler-ul Hangfire și lansează câte un crawl per sursă la
pornire:

```bash
docker compose up --build
```

- Web: http://localhost:3000
- API: http://localhost:5123 (OpenAPI la `/openapi/v1.json` în Development)
- Dashboard Hangfire: http://localhost:5123/hangfire (HTTP Basic Auth)
- Postgres: localhost:5433 (`deranjamente` / `deranjamente`)

### Doar backend-ul

```bash
dotnet run --project src/Deranjamente.Api
```

Setează `ConnectionStrings__Default` către Postgres-ul tău. Variabile de mediu utile:
`APPLY_MIGRATIONS=true` (migrează + populează la pornire — doar pentru demo-ul local; în
producție se folosește un migration bundle), `ENABLE_SCHEDULER=true|false` (rulează scheduler-ul
Hangfire) și `Hangfire__Username` / `Hangfire__Password` pentru dashboard.

### Doar frontend-ul

```bash
cd src/web
npm install
npm run dev
```

Setează `API_BASE_URL` către originea API-ului pentru fetch-urile de pe server.

> **Notă:** `src/web/AGENTS.md` semnalează că aceasta este o versiune de Next.js mai nouă, cu
> breaking changes — consultă `node_modules/next/dist/docs/` înainte de a edita cod de frontend.

## API

| Endpoint | Descriere |
|----------|-----------|
| `GET /api/outages?judet=&active=` | Întreruperile vizibile, filtrabile opțional după județ și doar cele active (`endsAt >= now` sau cu interval deschis). |
| `GET /api/severity?type=` | Numărul de întreruperi active per județ (curent/apă/total) raportat la toate cele 41 de județe, cu un flag de acoperire. Derivat la citire printr-un `GROUP BY` cu cache, filtru `type` opțional. Alimentează harta coropletă. |

## Teste

```bash
dotnet test
```

Parserele crawlerelor sunt verificate cu golden fixtures comise în repo (offline, deterministe);
testele de pipeline/registru/endpoint folosesc un Postgres temporar real.

## Decizii cheie (raționamentul complet în PRD)

- **O singură entitate `Outage`** pentru planificat + neplanificat (`isPlanned`); `source` =
  scraped|manual; ascundere soft prin `isVisible`, niciodată ștergere definitivă.
- **Deduplicare:** upsert pe content-hash din `provider + localitate + startsAt + affectedArea`;
  `endsAt` este mutabil; se urmăresc `firstSeen`/`lastSeen`/`disappearedAt`.
- **Geo:** tabele canonice SIRUTA. **Județul vine din configul crawlerului/structura documentului,
  niciodată din fuzzy-match** (greșirea județului devine imposibilă); localitatea se rezolvă în
  cadrul județului cunoscut prin exact → normalizat → fuzzy → fallback pe text brut + un tabel de
  aliasuri.
- **Trei tipuri de crawler:** single-fetch, *date-windowed* (Aquatim, JSON-XHR per zi,
  `lookaheadDays≈30`) și *document-source* (PDF-urile săptămânale naționale ale Rețele Electrice,
  identificate după nume/interval de date — nu după URL-ul S3 care expiră — și arhivate o dată
  după hash).

## Convenții

Vocabularul de domeniu este **românesc** (deranjament, avarie, intervenție programată, județ,
localitate, curent, apă) și este folosit în cod, UI și issues.
