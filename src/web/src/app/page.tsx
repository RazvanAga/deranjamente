import RomaniaMap from "./_components/RomaniaMap";
import styles from "./page.module.css";

export default function Home() {
  return (
    <main className={styles.main}>
      <header className={styles.hero}>
        <h1 className={styles.title}>deranjamente.com</h1>
        <p className={styles.tagline}>
          Întreruperi de curent și apă în România — avarii și intervenții
          programate, pe județe.
        </p>
      </header>

      <section className={styles.mapSection}>
        <RomaniaMap />
      </section>

      <p className={styles.note}>
        Acoperim deocamdată județul <strong>Timiș</strong>. Extindem treptat în
        restul țării.
      </p>
    </main>
  );
}
