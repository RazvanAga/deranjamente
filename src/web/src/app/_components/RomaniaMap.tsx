"use client";

import { useEffect, useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import geo from "@/app/_data/judete-geo.json";
import { PUBLIC_API_BASE_URL } from "@/lib/config";
import { judetSlug, fold } from "@/lib/judete";
import {
  type Category,
  severityFor,
  NOT_COVERED_FILL,
  NOT_COVERED_LABEL,
  SEVERITY_LEGEND,
} from "@/lib/severity";
import type { CountySeverity, SeverityResponse } from "@/lib/api";
import styles from "./RomaniaMap.module.css";

const CATEGORIES: { key: Category; label: string }[] = [
  { key: "toate", label: "Toate" },
  { key: "curent", label: "Curent" },
  { key: "apa", label: "Apă" },
];

function countFor(c: CountySeverity, category: Category): number {
  if (category === "curent") return c.counts.curent;
  if (category === "apa") return c.counts.apa;
  return c.counts.total;
}

export default function RomaniaMap() {
  const router = useRouter();
  const [data, setData] = useState<CountySeverity[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [category, setCategory] = useState<Category>("toate");
  const [hovered, setHovered] = useState<string | null>(null);
  const [query, setQuery] = useState("");

  // Severity is fetched client-side (never cached) so the map always reflects current data.
  useEffect(() => {
    let cancelled = false;
    fetch(`${PUBLIC_API_BASE_URL}/api/severity`, { cache: "no-store" })
      .then((r) => {
        if (!r.ok) throw new Error(`API ${r.status}`);
        return r.json() as Promise<SeverityResponse>;
      })
      .then((res) => {
        if (!cancelled) setData(res.counties);
      })
      .catch((e) => {
        if (!cancelled) setError(e instanceof Error ? e.message : "Eroare");
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const byCode = useMemo(() => {
    const m = new Map<string, CountySeverity>();
    for (const c of data ?? []) m.set(c.code, c);
    return m;
  }, [data]);

  const searchMatches = useMemo(() => {
    const q = fold(query.trim());
    if (!q) return [];
    return geo.judete
      .filter((j) => fold(j.name).includes(q))
      .slice(0, 6)
      .map((j) => ({ ...j, sev: byCode.get(j.code) }));
  }, [query, byCode]);

  const goTo = (name: string) => router.push(`/curent/${judetSlug(name)}`);

  const hoveredCounty = hovered ? byCode.get(hovered) : null;
  const hoveredGeo = hovered ? geo.judete.find((j) => j.code === hovered) : null;

  return (
    <div className={styles.wrap}>
      <div className={styles.controls}>
        <div className={styles.tabs} role="tablist" aria-label="Tip utilitate">
          {CATEGORIES.map((c) => (
            <button
              key={c.key}
              role="tab"
              aria-selected={category === c.key}
              className={category === c.key ? styles.tabActive : styles.tab}
              onClick={() => setCategory(c.key)}
            >
              {c.label}
            </button>
          ))}
        </div>

        <div className={styles.search}>
          <input
            type="search"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Caută un județ…"
            aria-label="Caută un județ"
            className={styles.searchInput}
          />
          {searchMatches.length > 0 && (
            <ul className={styles.searchResults}>
              {searchMatches.map((m) => {
                const n = m.sev ? countFor(m.sev, category) : 0;
                return (
                  <li key={m.code}>
                    {m.sev?.covered ? (
                      <Link
                        href={`/curent/${judetSlug(m.name)}`}
                        className={styles.searchHit}
                      >
                        <span>{m.name}</span>
                        <span className={styles.searchCount}>
                          {n} {n === 1 ? "întrerupere" : "întreruperi"}
                        </span>
                      </Link>
                    ) : (
                      <span className={styles.searchMiss}>
                        <span>{m.name}</span>
                        <span className={styles.searchCount}>
                          {NOT_COVERED_LABEL}
                        </span>
                      </span>
                    )}
                  </li>
                );
              })}
            </ul>
          )}
        </div>
      </div>

      {error && (
        <p className={styles.error}>Nu am putut încărca harta ({error}).</p>
      )}

      <div className={styles.mapBox}>
        <svg
          viewBox={`0 0 ${geo.width} ${geo.height}`}
          className={styles.map}
          role="img"
          aria-label="Harta județelor României cu nivelul întreruperilor"
        >
          <defs>
            <pattern
              id="not-covered"
              width="6"
              height="6"
              patternUnits="userSpaceOnUse"
              patternTransform="rotate(45)"
            >
              <rect width="6" height="6" fill="#eef1f3" />
              <line x1="0" y1="0" x2="0" y2="6" stroke="#d2d8dd" strokeWidth="1.5" />
            </pattern>
          </defs>

          {geo.judete.map((j) => {
            const sev = byCode.get(j.code);
            const covered = sev?.covered ?? false;
            const count = sev ? countFor(sev, category) : 0;
            const fill = !data
              ? "#eef1f3"
              : covered
                ? severityFor(count).fill
                : NOT_COVERED_FILL;
            const interactive = covered;
            return (
              <path
                key={j.code}
                d={j.d}
                fill={fill}
                className={
                  interactive ? styles.countyInteractive : styles.county
                }
                tabIndex={interactive ? 0 : -1}
                role={interactive ? "button" : undefined}
                aria-label={
                  covered
                    ? `${j.name}: ${count} întreruperi active`
                    : `${j.name}: ${NOT_COVERED_LABEL}`
                }
                onMouseEnter={() => setHovered(j.code)}
                onMouseLeave={() => setHovered((h) => (h === j.code ? null : h))}
                onFocus={() => setHovered(j.code)}
                onClick={interactive ? () => goTo(j.name) : undefined}
                onKeyDown={
                  interactive
                    ? (e) => {
                        if (e.key === "Enter" || e.key === " ") {
                          e.preventDefault();
                          goTo(j.name);
                        }
                      }
                    : undefined
                }
              >
                <title>
                  {covered
                    ? `${j.name} — ${count} întreruperi active`
                    : `${j.name} — ${NOT_COVERED_LABEL}`}
                </title>
              </path>
            );
          })}
        </svg>

        <p className={styles.caption} aria-live="polite">
          {hoveredGeo ? (
            hoveredCounty?.covered ? (
              <>
                <strong>{hoveredGeo.name}</strong> —{" "}
                {countFor(hoveredCounty, category)} întreruperi active
              </>
            ) : (
              <>
                <strong>{hoveredGeo.name}</strong> — {NOT_COVERED_LABEL}
              </>
            )
          ) : (
            "Treci cu mouse-ul peste un județ; apasă pe unul acoperit pentru detalii."
          )}
        </p>
      </div>

      <ul className={styles.legend}>
        {SEVERITY_LEGEND.map((s) => (
          <li key={s.key}>
            <span className={styles.swatch} style={{ background: s.fill }} />
            {s.label}
          </li>
        ))}
        <li>
          <span
            className={styles.swatch}
            style={{
              backgroundColor: "#eef1f3",
              backgroundImage:
                "repeating-linear-gradient(45deg, transparent 0 3px, #d2d8dd 3px 4.5px)",
            }}
          />
          {NOT_COVERED_LABEL}
        </li>
      </ul>
    </div>
  );
}
