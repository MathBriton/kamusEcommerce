/** Garante esquema na URL: plataformas às vezes entregam só "host:porta". */
const withScheme = (url: string) => (/^https?:\/\//.test(url) ? url : `http://${url}`);

/** URL da API usada pelo servidor Next.js (SSR, ISR, route handlers). */
export const apiUrl = withScheme(process.env.API_URL ?? "http://localhost:5080").replace(/\/$/, "");

/**
 * URL pública do storefront (canonical, sitemap, Open Graph). Lida em tempo de execução, no
 * servidor; RENDER_EXTERNAL_URL é preenchida automaticamente no Render.
 */
export const siteUrl = (
  process.env.SITE_URL ??
  process.env.RENDER_EXTERNAL_URL ??
  "http://localhost:3000"
).replace(/\/$/, "");
