import "server-only";
import { cookies } from "next/headers";
import { unstable_rethrow } from "next/navigation";
import type { z } from "zod";
import { apiGet } from "./api";
import { apiUrl } from "./env";
import { readProblem } from "./schemas";

/**
 * GET autenticado a partir de Server Components: repassa os cookies do navegador para a API.
 * Usar apenas em páginas dinâmicas (ler cookies torna a rota dinâmica).
 */
export async function apiGetWithSession<T extends z.ZodType>(path: string, schema: T) {
  const cookie = (await cookies()).toString();
  return apiGet(path, schema, { cache: "no-store", headers: { cookie } });
}

export type Loaded<T> = { ok: true; data: T } | { ok: false; status: number; message: string };

/**
 * Como `apiGetWithSession`, mas sem derrubar a página: falhas da API viram uma mensagem para o
 * usuário (o "detail"/"title" do problem+json), que a tela mostra num Alert.
 */
export async function apiLoadWithSession<T extends z.ZodType>(
  path: string,
  schema: T,
): Promise<Loaded<z.infer<T>>> {
  const cookie = (await cookies()).toString();
  let response: Response;
  try {
    response = await fetch(`${apiUrl}${path}`, {
      cache: "no-store",
      headers: { accept: "application/json", cookie },
    });
  } catch (error) {
    unstable_rethrow(error);
    console.error(`GET ${path} falhou`, error);
    return { ok: false, status: 0, message: "Não foi possível falar com a API. Tente novamente." };
  }

  if (!response.ok) {
    return { ok: false, status: response.status, message: await readProblem(response) };
  }

  const parsed = schema.safeParse(await response.json().catch(() => undefined));
  if (!parsed.success) {
    console.error(`GET ${path}: resposta fora do contrato`, parsed.error.issues);
    return {
      ok: false,
      status: response.status,
      message: "A API respondeu num formato inesperado. Tente novamente.",
    };
  }
  return { ok: true, data: parsed.data };
}
