// Severity buckets — absolute count thresholds with fixed colors (PRD: absolute, not relative,
// so a county's color means the same thing regardless of its neighbours). Recalibrating these
// against real data is a flagged follow-up before launch.

export type Category = "toate" | "curent" | "apa";

export type SeverityState = {
  /** Stable key for the bucket. */
  key: "covered-zero" | "low" | "medium" | "high" | "severe";
  /** Fill color for the choropleth. */
  fill: string;
  /** Short Romanian label for legend/tooltip. */
  label: string;
};

export const NOT_COVERED_FILL = "url(#not-covered)";
export const NOT_COVERED_LABEL = "Extindem curând";

const BUCKETS: { min: number; state: SeverityState }[] = [
  { min: 31, state: { key: "severe", fill: "#991b1b", label: "30+ active" } },
  { min: 16, state: { key: "high", fill: "#e23b3b", label: "16–30 active" } },
  { min: 6, state: { key: "medium", fill: "#f59e2c", label: "6–15 active" } },
  { min: 1, state: { key: "low", fill: "#f4cf3a", label: "1–5 active" } },
  { min: 0, state: { key: "covered-zero", fill: "#dfe7ec", label: "Fără întreruperi" } },
];

/** Map an active-outage count to its severity bucket (covered counties only). */
export function severityFor(count: number): SeverityState {
  return BUCKETS.find((b) => count >= b.min)!.state;
}

/** Legend entries in increasing-severity order, for the map key. */
export const SEVERITY_LEGEND: SeverityState[] = [...BUCKETS]
  .reverse()
  .map((b) => b.state);
