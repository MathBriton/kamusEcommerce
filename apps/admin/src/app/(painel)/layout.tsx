import { unstable_rethrow } from "next/navigation";
import { Sidebar } from "@/components/Sidebar";
import { requireAdmin } from "@/lib/auth";
import { storeUrl } from "@/lib/env";
import { trashPageSchema } from "@/lib/schemas";
import { apiGetWithSession } from "@/lib/server-api";

/** Total de itens na lixeira para o selo do menu. Falha aqui não derruba o painel: só some o selo. */
async function loadTrashCount(): Promise<number | null> {
  try {
    const trash = await apiGetWithSession(
      "/api/admin/catalog/trash?type=all&page=1&pageSize=1",
      trashPageSchema,
    );
    return trash ? trash.total : null;
  } catch (error) {
    unstable_rethrow(error);
    return null;
  }
}

/** Todas as telas do painel exigem administrador (verificação no servidor a cada requisição). */
export default async function PanelLayout({ children }: LayoutProps<"/">) {
  const [me, trashCount] = await Promise.all([requireAdmin(), loadTrashCount()]);

  return (
    <div className="flex min-h-full">
      <Sidebar name={me.fullName} storeUrl={storeUrl} trashCount={trashCount} />
      <main className="min-w-0 flex-1 px-8 py-8">
        <div className="mx-auto max-w-6xl">{children}</div>
      </main>
    </div>
  );
}
