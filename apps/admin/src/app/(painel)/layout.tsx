import { Sidebar } from "@/components/Sidebar";
import { requireAdmin } from "@/lib/auth";
import { storeUrl } from "@/lib/env";

/** Todas as telas do painel exigem administrador (verificação no servidor a cada requisição). */
export default async function PanelLayout({ children }: LayoutProps<"/">) {
  const me = await requireAdmin();

  return (
    <div className="flex min-h-full">
      <Sidebar name={me.fullName} storeUrl={storeUrl} />
      <main className="min-w-0 flex-1 px-8 py-8">
        <div className="mx-auto max-w-6xl">{children}</div>
      </main>
    </div>
  );
}
