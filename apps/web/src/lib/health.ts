import { z } from "zod";
import { apiUrl } from "./env";

export const healthSchema = z.object({
  status: z.enum(["Healthy", "Degraded", "Unhealthy"]),
  totalDurationMs: z.number(),
  checks: z.array(
    z.object({
      name: z.string(),
      status: z.enum(["Healthy", "Degraded", "Unhealthy"]),
      durationMs: z.number(),
      description: z.string().nullish(),
    }),
  ),
});

export type Health = z.infer<typeof healthSchema>;

export type HealthResult = { ok: true; health: Health } | { ok: false; error: string };

/** Consulta o /health da API. Nunca lança: a home precisa renderizar mesmo com a API fora do ar. */
export async function getHealth(): Promise<HealthResult> {
  try {
    const response = await fetch(`${apiUrl}/health`, {
      cache: "no-store",
      signal: AbortSignal.timeout(3000),
    });
    const parsed = healthSchema.safeParse(await response.json());
    if (!parsed.success) {
      return { ok: false, error: "Resposta inesperada da API" };
    }
    return { ok: true, health: parsed.data };
  } catch {
    return { ok: false, error: "API indisponível" };
  }
}
