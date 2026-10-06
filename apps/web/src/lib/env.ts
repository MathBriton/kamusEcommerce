/** URL da API usada pelo servidor Next.js (SSR, ISR, route handlers). */
export const apiUrl = (process.env.API_URL ?? "http://localhost:5080").replace(/\/$/, "");

/** URL pública do storefront, usada em canonical, sitemap e Open Graph. */
export const siteUrl = (process.env.NEXT_PUBLIC_SITE_URL ?? "http://localhost:3000").replace(
  /\/$/,
  "",
);
