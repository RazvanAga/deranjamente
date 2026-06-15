import judeteList from "@/app/_data/judete-list.json";

export type Judet = { code: string; name: string };

export const JUDETE: Judet[] = judeteList;

/**
 * Fold a Romanian string to a diacritic-free, lowercase ASCII form. Used for slugs and for
 * accent-insensitive search ("Timiș" and "timis" both match).
 */
export function fold(value: string): string {
  return value
    .normalize("NFD")
    .replace(/[̀-ͯ]/g, "") // strip combining accents (ă→a, â→a, î→i, etc.)
    .replace(/[șş]/gi, "s")
    .replace(/[țţ]/gi, "t")
    .toLowerCase();
}

/** URL slug for a județ, e.g. "Caraș-Severin" → "caras-severin". */
export function judetSlug(name: string): string {
  return fold(name)
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "");
}

const BY_SLUG = new Map(JUDETE.map((j) => [judetSlug(j.name), j]));

/** Resolve a URL slug back to its județ, or undefined if it isn't a real county. */
export function judetBySlug(slug: string): Judet | undefined {
  return BY_SLUG.get(slug.toLowerCase());
}
