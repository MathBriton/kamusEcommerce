import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { z } from "zod";
import { ProductHistory } from "@/components/audit/ProductHistory";
import { ProductEditor, type ProductTab } from "@/components/products/ProductEditor";
import { loadCatalogOptions } from "@/lib/catalog-options";
import { storeUrl } from "@/lib/env";
import { auditEntrySchema, productDetailSchema } from "@/lib/schemas";
import { apiGetWithSession, apiLoadWithSession } from "@/lib/server-api";

export const metadata: Metadata = { title: "Editar produto" };

/** Quantas entradas do histórico a aba mostra (as mais recentes). */
const HISTORY_LIMIT = 200;

export default async function EditProductPage(props: PageProps<"/produtos/[id]">) {
  const { id } = await props.params;
  const { aba } = await props.searchParams;
  const tab: ProductTab = aba === "historico" ? "historico" : "dados";
  const productId = encodeURIComponent(id);

  // Cada aba busca só o que mostra: opções do formulário em "Dados", auditoria em "Histórico".
  const [product, options, history] = await Promise.all([
    apiGetWithSession(`/api/admin/catalog/products/${productId}`, productDetailSchema),
    tab === "dados" ? loadCatalogOptions() : { categories: [], collections: [] },
    tab === "historico"
      ? apiLoadWithSession(
          `/api/admin/audit/subjects/Product/${productId}?limit=${HISTORY_LIMIT}`,
          z.array(auditEntrySchema),
        )
      : null,
  ]);
  if (!product) {
    // Fora do catálogo (na lixeira ou excluído de vez), o produto ainda tem histórico: os links
    // da Atividade continuam levando a algum lugar útil.
    if (tab === "historico" && history?.ok && history.data.length > 0) {
      const name = history.data.find((e) => e.subjectLabel)?.subjectLabel ?? "Produto";
      return (
        <>
          <Link href="/produtos" className="text-sm text-muted hover:text-ink">
            ← Produtos
          </Link>
          <div className="mt-4 mb-6">
            <h1 className="text-2xl font-semibold tracking-tight">{name}</h1>
            <p className="mt-2 rounded-md border border-line bg-surface px-3 py-2 text-sm text-muted">
              Este produto não está mais no catálogo: foi para a lixeira ou foi excluído de vez.{" "}
              <Link href="/lixeira" className="text-accent underline underline-offset-4">
                Ver lixeira
              </Link>
            </p>
          </div>
          <ProductHistory result={history} limit={HISTORY_LIMIT} />
        </>
      );
    }
    notFound();
  }

  return (
    <>
      <Link href="/produtos" className="text-sm text-muted hover:text-ink">
        ← Produtos
      </Link>
      <ProductEditor
        key={tab}
        initial={product}
        storeUrl={storeUrl}
        {...options}
        tab={tab}
        historyCount={history?.ok ? history.data.length : null}
        history={history && <ProductHistory result={history} limit={HISTORY_LIMIT} />}
      />
    </>
  );
}
