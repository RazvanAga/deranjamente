// Build-time generator for the județe choropleth geometry.
//
// Reads a RO counties GeoJSON (source below), projects it with d3-geo into a fixed SVG
// viewBox, and emits a small `judete-geo.json` of { code, name, d, cx, cy } per județ.
// Doing the projection here means the browser ships zero map libraries — the homepage map
// component just renders the precomputed <path> strings and colors them by severity.
//
// Source geometry: codeforamerica/click_that_hood `public/data/romania.geojson`
//   https://raw.githubusercontent.com/codeforamerica/click_that_hood/master/public/data/romania.geojson
// (42 features, diacritic-free ASCII names — matched to our canonical codes via NAME_TO_JUDET).
//
// Regenerate:  node scripts/build-geo.mjs
// (Expects scripts/ro-counties.source.geojson; the raw source is gitignored — re-download
//  from the URL above if missing.)

import { readFileSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";
import { geoMercator, geoPath } from "d3-geo";
import { topology } from "topojson-server";
import { presimplify, simplify, quantile } from "topojson-simplify";
import { feature } from "topojson-client";

const here = dirname(fileURLToPath(import.meta.url));
const SOURCE = join(here, "ro-counties.source.geojson");
const OUT = join(here, "..", "src", "app", "_data", "judete-geo.json");

const WIDTH = 800;
const HEIGHT = 520;

// ASCII GeoJSON feature name -> canonical { code, display name with diacritics }.
// Mirrors src/Deranjamente.Api/Data/siruta/judete.csv (41 județe + municipiul București).
const NAME_TO_JUDET = {
  Alba: ["AB", "Alba"],
  Arad: ["AR", "Arad"],
  Arges: ["AG", "Argeș"],
  Bacau: ["BC", "Bacău"],
  Bihor: ["BH", "Bihor"],
  "Bistrita-Nasaud": ["BN", "Bistrița-Năsăud"],
  Botosani: ["BT", "Botoșani"],
  Braila: ["BR", "Brăila"],
  Brasov: ["BV", "Brașov"],
  Bucuresti: ["B", "București"],
  Buzau: ["BZ", "Buzău"],
  Calarasi: ["CL", "Călărași"],
  "Caras-Severin": ["CS", "Caraș-Severin"],
  Cluj: ["CJ", "Cluj"],
  Constanta: ["CT", "Constanța"],
  Covasna: ["CV", "Covasna"],
  Dambovita: ["DB", "Dâmbovița"],
  Dolj: ["DJ", "Dolj"],
  Galati: ["GL", "Galați"],
  Giurgiu: ["GR", "Giurgiu"],
  Gorj: ["GJ", "Gorj"],
  Harghita: ["HR", "Harghita"],
  Hunedoara: ["HD", "Hunedoara"],
  Ialomita: ["IL", "Ialomița"],
  Iasi: ["IS", "Iași"],
  Ilfov: ["IF", "Ilfov"],
  Maramures: ["MM", "Maramureș"],
  Mehedinti: ["MH", "Mehedinți"],
  Mures: ["MS", "Mureș"],
  Neamt: ["NT", "Neamț"],
  Olt: ["OT", "Olt"],
  Prahova: ["PH", "Prahova"],
  Salaj: ["SJ", "Sălaj"],
  "Satu Mare": ["SM", "Satu Mare"],
  Sibiu: ["SB", "Sibiu"],
  Suceava: ["SV", "Suceava"],
  Teleorman: ["TR", "Teleorman"],
  Timis: ["TM", "Timiș"],
  Tulcea: ["TL", "Tulcea"],
  Valcea: ["VL", "Vâlcea"],
  Vaslui: ["VS", "Vaslui"],
  Vrancea: ["VN", "Vrancea"],
};

const raw = JSON.parse(readFileSync(SOURCE, "utf8"));

// Simplify topology-first so shared county borders stay coincident (no slivers/gaps).
// Drop ~92% of the densest vertices — plenty of fidelity for an 800px national map while
// shrinking the bundled geometry from ~1MB to ~100KB.
const presimplified = presimplify(topology({ counties: raw }));
const simplified = simplify(presimplified, quantile(presimplified, 0.08));
const geo = feature(simplified, simplified.objects.counties);

// Fit the whole country into the viewBox with a small inset margin.
const margin = 8;
const projection = geoMercator().fitExtent(
  [
    [margin, margin],
    [WIDTH - margin, HEIGHT - margin],
  ],
  geo,
);
const path = geoPath(projection);

// Trim float precision in the generated `d` strings — 1 decimal in an 800px space is
// visually lossless and keeps the bundled JSON tiny.
const round = (d) => d.replace(/-?\d+\.\d+/g, (n) => Number(n).toFixed(1));

const out = [];
const unmatched = [];

for (const feature of geo.features) {
  const entry = NAME_TO_JUDET[feature.properties.name];
  if (!entry) {
    unmatched.push(feature.properties.name);
    continue;
  }
  const [code, name] = entry;
  const d = path(feature);
  const [cx, cy] = path.centroid(feature);
  out.push({
    code,
    name,
    d: round(d),
    cx: Number(cx.toFixed(1)),
    cy: Number(cy.toFixed(1)),
  });
}

if (unmatched.length) {
  throw new Error(`Unmatched GeoJSON county names: ${unmatched.join(", ")}`);
}
if (out.length !== 42) {
  throw new Error(`Expected 42 județe, produced ${out.length}`);
}

out.sort((a, b) => a.name.localeCompare(b.name, "ro"));

const payload = { width: WIDTH, height: HEIGHT, judete: out };
writeFileSync(OUT, JSON.stringify(payload) + "\n");
console.log(`Wrote ${out.length} județe to ${OUT}`);

// Lightweight code+name list (no geometry) for slug routing + search on pages that don't
// render the map.
const LIST_OUT = join(here, "..", "src", "app", "_data", "judete-list.json");
const list = out.map(({ code, name }) => ({ code, name }));
writeFileSync(LIST_OUT, JSON.stringify(list) + "\n");
console.log(`Wrote ${list.length} județe (list) to ${LIST_OUT}`);
