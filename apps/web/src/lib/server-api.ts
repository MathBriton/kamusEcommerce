import "server-only";
import { cookies } from "next/headers";
import type { z } from "zod";
import { apiGet } from "./api";

/**
 * GET autenticado a partir de Server Components: repassa os cookies do navegador para a API.
 * Usar apenas em páginas dinâmicas (ler cookies torna a rota dinâmica).
 */
export async function apiGetWithSession<T extends z.ZodType>(path: string, schema: T) {
  const cookie = (await cookies()).toString();
  return apiGet(path, schema, { cache: "no-store", headers: { cookie } });
}
