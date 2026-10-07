import "server-only";
import { redirect } from "next/navigation";
import { ApiError } from "./api";
import { meSchema, type Me } from "./schemas";
import { apiGetWithSession } from "./server-api";

/** Garante sessão de administrador; senão manda para o login. A API é quem autoriza de fato. */
export async function requireAdmin(): Promise<Me> {
  let me: Me | null = null;
  try {
    me = await apiGetWithSession("/api/identity/me", meSchema);
  } catch (error) {
    if (!(error instanceof ApiError && error.status === 401)) throw error;
  }

  if (!me) redirect("/entrar");
  if (!me.isAdmin) redirect("/entrar?erro=permissao");
  return me;
}
