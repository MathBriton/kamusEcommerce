import type { Metadata } from "next";
import Link from "next/link";
import { PageHeader, Pagination, StatusBadge, Table } from "@/components/ui";
import { formatDateTime, formatInt, formatPrice } from "@/lib/format";
import { ORDER_STATUS_LABEL, adminPage, orderRowSchema, orderStatusSchema } from "@/lib/schemas";
import { apiGetWithSession } from "@/lib/server-api";

export const metadata: Metadata = { title: "Pedidos" };

/** Abas na ordem do fluxo operacional: o que precisa de ação aparece primeiro. */
const TABS = [
  "",
  "Paid",
  "Shipped",
  "AwaitingPayment",
  "Delivered",
  "Cancelled",
  "PaymentFailed",
] as const;

export default async function OrdersPage(props: PageProps<"/pedidos">) {
  const params = await props.searchParams;
  const search = typeof params.busca === "string" ? params.busca : "";
  const parsed = orderStatusSchema.safeParse(params.status);
  const status = parsed.success ? parsed.data : "";
  const page = Math.max(1, Number(params.pagina) || 1);

  const query = new URLSearchParams({ page: String(page), pageSize: "25" });
  if (status) query.set("status", status);
  if (search) query.set("search", search);
  const data = (await apiGetWithSession(`/api/admin/orders?${query}`, adminPage(orderRowSchema)))!;

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
    return `/pedidos${qs ? `?${qs}` : ""}`;
  };

  return (
    <>
      <PageHeader
        title="Pedidos"
        description={`${formatInt(data.total)} ${status ? `com status "${ORDER_STATUS_LABEL[status]}"` : "no total"}`}
      />

      <div className="mb-4 flex flex-wrap items-center gap-3">
        <nav
          aria-label="Filtrar por status"
          className="flex flex-wrap gap-1 rounded-md border border-line bg-surface p-1 text-sm"
        >
          {TABS.map((t) => (
            <Link
              key={t}
              href={href({ status: t, pagina: 1 })}
              aria-current={t === status ? "true" : undefined}
              className={`rounded px-3 py-1 ${t === status ? "bg-ink text-paper" : "text-muted hover:text-ink"}`}
            >
              {t ? ORDER_STATUS_LABEL[t] : "Todos"}
            </Link>
          ))}
        </nav>
        <form action="/pedidos">
          {status && <input type="hidden" name="status" value={status} />}
          <input
            name="busca"
            defaultValue={search}
            placeholder="Número ou destinatário"
            aria-label="Buscar pedidos"
            className="h-9 w-60 rounded-md border border-line bg-surface px-3 text-sm focus:border-ink focus:outline-none"
          />
        </form>
      </div>

      <Table
        head={
          <tr>
            <th className="px-4 py-2.5 font-medium">Pedido</th>
            <th className="px-4 py-2.5 font-medium">Data</th>
            <th className="px-4 py-2.5 font-medium">Destinatário</th>
            <th className="px-4 py-2.5 font-medium">Status</th>
            <th className="px-4 py-2.5 text-right font-medium">Itens</th>
            <th className="px-4 py-2.5 text-right font-medium">Total</th>
          </tr>
        }
      >
        {data.items.length === 0 && (
          <tr>
            <td colSpan={6} className="px-4 py-10 text-center text-muted">
              Nenhum pedido encontrado.
            </td>
          </tr>
        )}
        {data.items.map((o) => (
          <tr key={o.id} className="hover:bg-paper">
            <td className="px-4 py-2.5">
              <Link href={`/pedidos/${o.id}`} className="font-medium hover:text-accent">
                {o.number}
              </Link>
            </td>
            <td className="px-4 py-2.5 text-muted">{formatDateTime(o.createdAt)}</td>
            <td className="px-4 py-2.5">
              {o.recipientName}
              <span className="block text-xs text-muted">
                {o.city}/{o.state}
              </span>
            </td>
            <td className="px-4 py-2.5">
              <StatusBadge status={o.status} />
            </td>
            <td className="tabular px-4 py-2.5 text-right">{o.itemCount}</td>
            <td className="tabular px-4 py-2.5 text-right font-medium">{formatPrice(o.total)}</td>
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
