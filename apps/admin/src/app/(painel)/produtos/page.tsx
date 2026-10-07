import type { Metadata } from "next";
import Image from "next/image";
import Link from "next/link";
import { LinkButton, PageHeader, Pagination, PublishedBadge, Table } from "@/components/ui";
import { formatDateTime, formatInt, formatPrice } from "@/lib/format";
import { adminPage, productRowSchema } from "@/lib/schemas";
import { apiGetWithSession } from "@/lib/server-api";

export const metadata: Metadata = { title: "Produtos" };

const STATUS = [
  { value: "", label: "Todos" },
  { value: "active", label: "Publicados" },
  { value: "inactive", label: "Rascunhos" },
];

export default async function ProductsPage(props: PageProps<"/produtos">) {
  const params = await props.searchParams;
  const search = typeof params.busca === "string" ? params.busca : "";
  const status = typeof params.status === "string" ? params.status : "";
  const page = Math.max(1, Number(params.pagina) || 1);

  const query = new URLSearchParams({ page: String(page), pageSize: "20" });
  if (search) query.set("search", search);
  if (status) query.set("status", status);
  const data = (await apiGetWithSession(
    `/api/admin/catalog/products?${query}`,
    adminPage(productRowSchema),
  ))!;

  const href = (overrides: Record<string, string | number>) => {
    const next = new URLSearchParams({
      ...(search && { busca: search }),
      ...(status && { status }),
    });
    for (const [k, v] of Object.entries(overrides)) {
      if (v === "" || v === 1) next.delete(k);
      else next.set(k, String(v));
    }
    const qs = next.toString();
    return `/produtos${qs ? `?${qs}` : ""}`;
  };

  return (
    <>
      <PageHeader
        title="Produtos"
        description={`${formatInt(data.total)} no catálogo`}
        actions={<LinkButton href="/produtos/novo">Novo produto</LinkButton>}
      />

      <div className="mb-4 flex flex-wrap items-center gap-3">
        <form className="flex gap-2" action="/produtos">
          {status && <input type="hidden" name="status" value={status} />}
          <input
            name="busca"
            defaultValue={search}
            placeholder="Buscar por nome, slug ou SKU"
            aria-label="Buscar produtos"
            className="h-9 w-72 rounded-md border border-line bg-surface px-3 text-sm focus:border-ink focus:outline-none"
          />
        </form>
        <nav
          aria-label="Filtrar por situação"
          className="flex gap-1 rounded-md border border-line bg-surface p-1 text-sm"
        >
          {STATUS.map((s) => (
            <Link
              key={s.value}
              href={href({ status: s.value, pagina: 1 })}
              aria-current={s.value === status ? "true" : undefined}
              className={`rounded px-3 py-1 ${s.value === status ? "bg-ink text-paper" : "text-muted hover:text-ink"}`}
            >
              {s.label}
            </Link>
          ))}
        </nav>
      </div>

      <Table
        head={
          <tr>
            <th className="px-4 py-2.5 font-medium">Produto</th>
            <th className="px-4 py-2.5 font-medium">Categoria</th>
            <th className="px-4 py-2.5 font-medium">Situação</th>
            <th className="px-4 py-2.5 text-right font-medium">SKUs</th>
            <th className="px-4 py-2.5 text-right font-medium">A partir de</th>
            <th className="px-4 py-2.5 text-right font-medium">Disponível</th>
            <th className="px-4 py-2.5 font-medium">Atualizado</th>
          </tr>
        }
      >
        {data.items.length === 0 && (
          <tr>
            <td colSpan={7} className="px-4 py-10 text-center text-muted">
              Nenhum produto encontrado.
            </td>
          </tr>
        )}
        {data.items.map((p) => (
          <tr key={p.id} className="hover:bg-paper">
            <td className="px-4 py-2.5">
              <Link href={`/produtos/${p.id}`} className="flex items-center gap-3">
                <span className="relative h-11 w-8 shrink-0 overflow-hidden rounded bg-sand">
                  {p.imageUrl && (
                    <Image
                      src={p.imageUrl}
                      alt=""
                      fill
                      unoptimized
                      sizes="32px"
                      className="object-cover"
                    />
                  )}
                </span>
                <span>
                  <span className="block font-medium hover:text-accent">{p.name}</span>
                  <span className="block text-xs text-muted">{p.brand}</span>
                </span>
              </Link>
            </td>
            <td className="px-4 py-2.5 text-muted">{p.categoryName}</td>
            <td className="px-4 py-2.5">
              <PublishedBadge active={p.isActive} />
            </td>
            <td className="tabular px-4 py-2.5 text-right">{p.skuCount}</td>
            <td className="tabular px-4 py-2.5 text-right">
              {p.minPrice === null ? "—" : formatPrice(p.minPrice)}
            </td>
            <td
              className={`tabular px-4 py-2.5 text-right ${p.available === 0 && p.skuCount > 0 ? "font-medium text-sale" : ""}`}
            >
              {formatInt(p.available)}
            </td>
            <td className="px-4 py-2.5 text-xs text-muted">{formatDateTime(p.updatedAt)}</td>
          </tr>
        ))}
      </Table>

      <Pagination
        page={data.page}
        pageSize={data.pageSize}
        total={data.total}
        href={(p) => href({ pagina: p })}
      />
    </>
  );
}
