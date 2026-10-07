import type { z } from "zod";
import { apiUrl } from "./env";

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
  ) {
    super(message);
  }
}

type FetchOptions = RequestInit & { next?: { revalidate?: number | false; tags?: string[] } };

/**
 * GET na API .NET a partir do servidor Next.js, validando a resposta com Zod.
 * Retorna `null` em 404 para que a página decida chamar `notFound()`.
 */
export async function apiGet<T extends z.ZodType>(
  path: string,
  schema: T,
  init?: FetchOptions,
): Promise<z.infer<T> | null> {
  const response = await fetch(`${apiUrl}${path}`, {
    ...init,
    headers: { accept: "application/json", ...init?.headers },
  });

  if (response.status === 404) return null;
  if (!response.ok) throw new ApiError(response.status, `GET ${path} → ${response.status}`);

  return schema.parse(await response.json());
}
