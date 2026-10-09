import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { TrashView } from "@/components/trash/TrashView";
import { Alert, PageHeader, Pagination } from "@/components/ui";
import { trashPageSchema } from "@/lib/schemas";
import { apiLoadWithSession } from "@/lib/server-api";
import { TRASH_RETENTION_DAYS, trashTab } from "@/lib/trash";

export const metadata: Metadata = { title: "Lixeira" };

const PAGE_SIZE = 50;

/** Instante da requisição (a página é dinâmica: renderiza a cada acesso). */
const requestTime = () => Date.now();

export default async function TrashPage(props: PageProps<"/lixeira">) {
  const params = await props.searchParams;
  const tab = trashTab(params.tipo);
  const page = Math.max(1, Number(params.pagina) || 1);

  const result = await apiLoadWithSession(
    `/api/admin/catalog/trash?type=${tab.type}&page=${page}&pageSize=${PAGE_SIZE}`,
    trashPageSchema,
  );

  const href = (nextPage: number) => {
    const next = new URLSearchParams();
    if (tab.value) next.set("tipo", tab.value);
    if (nextPage > 1) next.set("pagina", String(nextPage));
    const qs = next.toString();
    return `/lixeira${qs ? `?${qs}` : ""}`;
  };

  // Restaurar ou expurgar os últimos itens de uma página deixa a página vazia: volta para a última
  // página que ainda tem itens, em vez de mostrar "nada por aqui" com itens nas outras.
  if (result.ok && result.data.items.length === 0 && page > 1) {
    redirect(href(Math.max(1, Math.ceil(result.data.total / PAGE_SIZE))));
  }

  return (
    <>
      <PageHeader
        title="Lixeira"
        description={`Itens excluídos saem da loja e do backoffice, mas voltam intactos se restaurados em até ${TRASH_RETENTION_DAYS} dias.`}
      />

      {result.ok ? (
        <>
          <TrashView
            items={result.data.items}
            counts={result.data.counts}
            tab={tab}
            now={requestTime()}
          />
          {result.data.total > PAGE_SIZE && (
            <Pagination page={page} pageSize={PAGE_SIZE} total={result.data.total} href={href} />
          )}
        </>
      ) : (
        <Alert>{result.message}</Alert>
      )}

      <p className="mt-4 text-xs text-muted">
        Pedidos nunca vão para a lixeira: são cancelados, não excluídos. Excluir de vez também fica
        registrado na Atividade.
      </p>
    </>
  );
}
