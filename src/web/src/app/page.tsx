type Outage = {
  id: number;
  provider: string;
  type: string;
  judet: string;
  localitate: string;
  affectedArea: string;
  startsAt: string;
  endsAt: string | null;
  isPlanned: boolean;
  source: string;
  sourceUrl: string;
};

// Server-side base URL. In docker-compose the web container reaches the api by service name;
// locally it falls back to the dev API port.
const API_BASE_URL = process.env.API_BASE_URL ?? "http://localhost:5123";

async function getOutages(judet: string): Promise<Outage[]> {
  const res = await fetch(
    `${API_BASE_URL}/api/outages?judet=${encodeURIComponent(judet)}`,
    { cache: "no-store" },
  );
  if (!res.ok) {
    throw new Error(`API returned ${res.status}`);
  }
  return res.json();
}

function formatWindow(startsAt: string, endsAt: string | null): string {
  const fmt = (iso: string) =>
    new Date(iso).toLocaleString("ro-RO", {
      day: "2-digit",
      month: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
    });
  return endsAt ? `${fmt(startsAt)} – ${fmt(endsAt)}` : `${fmt(startsAt)} – (în desfășurare)`;
}

export default async function Home() {
  const judet = "Timiș";
  let outages: Outage[] = [];
  let error: string | null = null;

  try {
    outages = await getOutages(judet);
  } catch (e) {
    error = e instanceof Error ? e.message : "Eroare necunoscută";
  }

  return (
    <main style={{ maxWidth: 720, margin: "0 auto", padding: "2rem 1rem", fontFamily: "system-ui, sans-serif" }}>
      <h1>deranjamente.com</h1>
      <p>Întreruperi de utilități — județul {judet}</p>

      {error && <p style={{ color: "crimson" }}>Nu am putut încărca datele: {error}</p>}

      {!error && outages.length === 0 && <p>Nicio întrerupere activă.</p>}

      <ul style={{ listStyle: "none", padding: 0 }}>
        {outages.map((o) => (
          <li
            key={o.id}
            style={{ border: "1px solid #ddd", borderRadius: 8, padding: "1rem", marginBottom: "1rem" }}
          >
            <div style={{ display: "flex", justifyContent: "space-between", gap: "1rem" }}>
              <strong>{o.localitate}</strong>
              <span>{o.isPlanned ? "Intervenție programată" : "Avarie"}</span>
            </div>
            <div style={{ color: "#555" }}>
              {o.provider} · {o.type}
            </div>
            <div>{o.affectedArea}</div>
            <div style={{ color: "#555" }}>{formatWindow(o.startsAt, o.endsAt)}</div>
            <a href={o.sourceUrl} target="_blank" rel="noreferrer">
              Sursă oficială
            </a>
          </li>
        ))}
      </ul>
    </main>
  );
}
