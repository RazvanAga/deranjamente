// API base URLs.
//
// - `API_BASE_URL` is used for server-side fetches (county page SSG/ISR). In docker-compose
//   the web container reaches the api by service name; locally it falls back to the dev port.
// - `PUBLIC_API_BASE_URL` is used for fetches that run in the browser (the homepage map polls
//   severity client-side), so it must be a URL the visitor's browser can reach. `NEXT_PUBLIC_`
//   vars are inlined into the client bundle at build time.

export const API_BASE_URL = process.env.API_BASE_URL ?? "http://localhost:5123";

export const PUBLIC_API_BASE_URL =
  process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5123";
