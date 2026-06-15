import { API_BASE_URL } from "./config";

export type UtilityType = "Curent" | "Apa" | "Gaz" | "Internet";

export type Outage = {
  id: number;
  provider: string;
  type: UtilityType;
  judet: string;
  localitate: string;
  affectedArea: string;
  startsAt: string;
  endsAt: string | null;
  isPlanned: boolean;
  source: string;
  sourceUrl: string;
};

export type CountyCounts = { curent: number; apa: number; total: number };

export type CountySeverity = {
  code: string;
  name: string;
  covered: boolean;
  counts: CountyCounts;
};

export type SeverityResponse = {
  generatedAt: string;
  counties: CountySeverity[];
};

/** Server-side fetch of the per-județ severity (used by the ISR county page for coverage). */
export async function getSeverity(): Promise<SeverityResponse> {
  const res = await fetch(`${API_BASE_URL}/api/severity`, {
    next: { revalidate: 600 },
  });
  if (!res.ok) {
    throw new Error(`API returned ${res.status}`);
  }
  return res.json();
}

/** Server-side fetch of active outages for a județ (used by the ISR county page). */
export async function getActiveOutages(judetName: string): Promise<Outage[]> {
  const url = `${API_BASE_URL}/api/outages?judet=${encodeURIComponent(
    judetName,
  )}&active=true`;
  const res = await fetch(url, { next: { revalidate: 600 } });
  if (!res.ok) {
    throw new Error(`API returned ${res.status}`);
  }
  return res.json();
}
