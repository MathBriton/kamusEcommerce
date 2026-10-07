/** Garante esquema na URL: plataformas às vezes entregam só "host:porta". */
const withScheme = (url: string) => (/^https?:\/\//.test(url) ? url : `http://${url}`);

/** URL da API usada pelo servidor do backoffice (Server Components e BFF). */
export const apiUrl = withScheme(process.env.API_URL ?? "http://localhost:5080").replace(/\/$/, "");

/** Loja pública, para os links "ver na loja". */
export const storeUrl = (process.env.STORE_URL ?? "http://localhost:3000").replace(/\/$/, "");
