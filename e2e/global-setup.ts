import { urls } from "./tests/urls";

/** Espera API, loja e backoffice responderem (os containers sobem em paralelo). */
export default async function globalSetup() {
  const targets = [`${urls.api}/health`, `${urls.store}/`, `${urls.admin}/entrar`];
  const deadline = Date.now() + 180_000;

  for (const target of targets) {
    for (;;) {
      try {
        const response = await fetch(target, { redirect: "manual" });
        if (response.status < 500) break;
      } catch {
        // ainda subindo
      }
      if (Date.now() > deadline) throw new Error(`Timeout esperando ${target}`);
      await new Promise((r) => setTimeout(r, 2000));
    }
  }
}
