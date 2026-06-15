import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { getActiveOutages, getSeverity } from "@/lib/api";
import { judetBySlug } from "@/lib/judete";
import CountyOutages from "./CountyOutages";
import styles from "./county.module.css";

// SSG/ISR: prerender covered județe at build, revalidate every ~10 minutes for SEO freshness
// (e.g. "pană curent Timiș"). Non-covered județe are valid pages too, rendered on demand.
export const revalidate = 600;

export async function generateStaticParams() {
  // The API isn't reachable during the (separate) web image build, so we don't prerender at
  // build time. With dynamicParams (default true), each județ page is generated on its first
  // request and then cached/revalidated as ISR — same SEO benefit, no build-time API coupling.
  return [];
}

// Only the 42 real județe are valid; anything else is a 404 rather than an on-demand render.
export const dynamicParams = true;

type Params = { params: Promise<{ judet: string }> };

export async function generateMetadata({ params }: Params): Promise<Metadata> {
  const { judet: slug } = await params;
  const judet = judetBySlug(slug);
  if (!judet) return { title: "Județ inexistent — deranjamente.com" };
  return {
    title: `Întreruperi curent și apă în ${judet.name} — deranjamente.com`,
    description: `Avarii și intervenții programate la curent și apă în județul ${judet.name}: pană curent ${judet.name}, întrerupere apă ${judet.name}, în timp real.`,
  };
}

export default async function CountyPage({ params }: Params) {
  const { judet: slug } = await params;
  const judet = judetBySlug(slug);
  if (!judet) {
    notFound();
  }

  let outages: Awaited<ReturnType<typeof getActiveOutages>> = [];
  let covered = false;
  let loadFailed = false;
  try {
    const [fetchedOutages, severity] = await Promise.all([
      getActiveOutages(judet.name),
      getSeverity(),
    ]);
    outages = fetchedOutages;
    covered = severity.counties.find((c) => c.code === judet.code)?.covered ?? false;
  } catch {
    loadFailed = true;
  }

  return (
    <main className={styles.main}>
      <nav className={styles.breadcrumb}>
        <Link href="/">← Harta României</Link>
      </nav>

      <header className={styles.header}>
        <h1 className={styles.title}>
          Întreruperi în județul {judet.name}
        </h1>
        <p className={styles.sub}>
          Curent și apă — avarii și intervenții programate active.
        </p>
      </header>

      {loadFailed ? (
        <div className={styles.notice}>
          <strong>Datele nu sunt disponibile momentan.</strong>
          <p>Reîncearcă în câteva minute.</p>
        </div>
      ) : !covered ? (
        <div className={styles.notice}>
          <strong>Încă nu acoperim județul {judet.name}.</strong>
          <p>
            Extindem treptat în toată țara. Deocamdată avem date pentru{" "}
            <Link href="/curent/timis" className={styles.link}>
              Timiș
            </Link>
            .
          </p>
        </div>
      ) : (
        <CountyOutages outages={outages} judetName={judet.name} />
      )}
    </main>
  );
}
