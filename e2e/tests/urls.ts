export const urls = {
  store: process.env.STORE_URL ?? "http://localhost:3000",
  admin: process.env.ADMIN_URL ?? "http://localhost:3001",
  api: process.env.API_URL ?? "http://localhost:5080",
};

export const admin = { email: "admin@kamus.dev", password: "admin-kamus-123" };

/** Caminho da captura de referência dentro de apps/ (ver snapshotPathTemplate). */
export const design = (app: "web" | "admin", ...path: string[]) => [app, "design", ...path];
