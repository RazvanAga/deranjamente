# Slice 4 — Rețele Electrice crawler (curent, document-source PDF) (AFK)

## Parent
#1

## What to build
The second real crawler — Rețele Electrice (electricity, ex-E-Distribuție Banat) — introducing the **document-source** crawler shape and PDF parsing, delivering real Timiș electricity outages to the site.

The listing page `reteleelectrice.ro/intreruperi/programate/` is static HTML with all PDF links in the DOM. PDFs are weekly and national (one section per județ); Timiș is a section inside the PDF. PDF URLs are presigned AWS S3 links that expire (~1h) — so identity is the filename/date-range, never the URL, and a fresh signed URL must be obtained from the listing at download time.

Implement: a `CrawledDocument` ledger (stable key = filename/date-range, contentHash, rowsExtracted, status, lastCheckedAt); listing-page discovery; download in-window documents via fresh signed URLs; extract with **PdfPig** (coordinate-aware: reconstruct columns by x-position, group rows by y-bands); parse columns (Data, Localitate, affected-area/details, time window); filter rows to Timiș; upsert into `Outage`. Archive each source PDF once (object storage/filesystem, keyed by content hash).

Empty-PDF rule: a 0-row document is `provisional` and re-checked each run; once non-empty + hash-stable it becomes `final`. Current/future-week docs are re-checked even when non-empty; fully-past docs are frozen. Skip unchanged documents (don't re-crawl static files constantly).

## Acceptance criteria
- [ ] Document-source crawler shape + `CrawledDocument` ledger implemented
- [ ] Listing page parsed for PDF documents; only in-window (by date-range) docs processed; unchanged docs skipped
- [ ] PDF identity = filename/date-range (never the expiring URL); fresh signed URL fetched at download time
- [ ] PdfPig coordinate extraction parses the Timiș section into normalized `Outage`s (Data, Localitate, affectedArea raw, time window)
- [ ] Empty/placeholder PDF stays `provisional` and is re-checked until replaced; then marked `final`
- [ ] Source PDF archived once per document (off the hot table), referenced by content hash
- [ ] Golden PDF fixtures committed; parser tests assert expected normalized `Outage`s offline
- [ ] Real Timiș electricity outages appear via the API and on the web page
- [ ] After implementation, create a commit with a descriptive message

## Blocked by
- #3
