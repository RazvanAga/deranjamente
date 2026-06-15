"use client";

import { useMemo, useState } from "react";
import type { Outage } from "@/lib/api";
import { fold } from "@/lib/judete";
import type { Category } from "@/lib/severity";
import styles from "./county.module.css";

const CATEGORIES: { key: Category; label: string }[] = [
  { key: "toate", label: "Toate" },
  { key: "curent", label: "Curent" },
  { key: "apa", label: "Apă" },
];

function matchesCategory(o: Outage, category: Category): boolean {
  if (category === "curent") return o.type === "Curent";
  if (category === "apa") return o.type === "Apa";
  return true;
}

function isOngoing(o: Outage, now: number): boolean {
  const start = new Date(o.startsAt).getTime();
  if (start > now) return false;
  if (!o.endsAt) return true;
  return new Date(o.endsAt).getTime() >= now;
}

const typeLabel = (t: Outage["type"]) =>
  t === "Curent" ? "Curent" : t === "Apa" ? "Apă" : t;

function formatWindow(startsAt: string, endsAt: string | null): string {
  const fmt = (iso: string) =>
    new Date(iso).toLocaleString("ro-RO", {
      day: "2-digit",
      month: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
    });
  return endsAt
    ? `${fmt(startsAt)} – ${fmt(endsAt)}`
    : `${fmt(startsAt)} – (fără oră de final)`;
}

export default function CountyOutages({
  outages,
  judetName,
}: {
  outages: Outage[];
  judetName: string;
}) {
  const [category, setCategory] = useState<Category>("toate");
  const [query, setQuery] = useState("");
  // Pin "now" at mount so render stays pure; a page revisit/ISR refresh re-evaluates it.
  const [now] = useState(() => Date.now());

  const visible = useMemo(() => {
    const q = fold(query.trim());
    return outages
      .filter((o) => matchesCategory(o, category))
      .filter(
        (o) =>
          !q ||
          fold(o.localitate).includes(q) ||
          fold(o.affectedArea).includes(q),
      )
      .sort((a, b) => {
        // Ongoing first, then by start time ascending.
        const ao = isOngoing(a, now) ? 0 : 1;
        const bo = isOngoing(b, now) ? 0 : 1;
        if (ao !== bo) return ao - bo;
        return new Date(a.startsAt).getTime() - new Date(b.startsAt).getTime();
      });
  }, [outages, category, query, now]);

  return (
    <div className={styles.list}>
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
        <input
          type="search"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="Caută o localitate sau stradă…"
          aria-label="Caută o localitate sau stradă"
          className={styles.searchInput}
        />
      </div>

      <p className={styles.count}>
        {visible.length}{" "}
        {visible.length === 1
          ? "întrerupere activă"
          : "întreruperi active"}{" "}
        în {judetName}
      </p>

      {visible.length === 0 ? (
        <p className={styles.empty}>Nicio întrerupere activă pentru acest filtru.</p>
      ) : (
        <ul className={styles.cards}>
          {visible.map((o) => {
            const ongoing = isOngoing(o, now);
            return (
              <li key={o.id} className={styles.card}>
                <div className={styles.cardTop}>
                  <span className={styles.localitate}>{o.localitate}</span>
                  <span
                    className={ongoing ? styles.badgeNow : styles.badgePlanned}
                  >
                    {ongoing
                      ? "În desfășurare acum"
                      : `Programat — ${formatWindow(o.startsAt, null)}`}
                  </span>
                </div>

                <div className={styles.meta}>
                  <span className={styles.typeChip} data-type={o.type}>
                    {typeLabel(o.type)}
                  </span>
                  <span>{o.isPlanned ? "Intervenție programată" : "Avarie"}</span>
                </div>

                {o.affectedArea && (
                  <p className={styles.area}>{o.affectedArea}</p>
                )}

                <div className={styles.cardBottom}>
                  <span className={styles.window}>
                    {formatWindow(o.startsAt, o.endsAt)}
                  </span>
                  <a
                    href={o.sourceUrl}
                    target="_blank"
                    rel="noreferrer"
                    className={styles.source}
                  >
                    Sursă: {o.provider} ↗
                  </a>
                </div>
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
