import type { Metadata } from "next";
import Image from "next/image";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ActorBadge } from "@/components/audit/badges";
import { OrderActions } from "@/components/orders/OrderActions";
import { Card, PageHeader, StatusBadge } from "@/components/ui";
import { formatDateTime, formatPrice } from "@/lib/format";
import { ORDER_STATUS_LABEL, orderDetailSchema } from "@/lib/schemas";
import { apiGetWithSession } from "@/lib/server-api";

export const metadata: Metadata = { title: "Pedido" };

export default async function OrderPage(props: PageProps<"/pedidos/[id]">) {
  const { id } = await props.params;
  const data = await apiGetWithSession(
    `/api/admin/orders/${encodeURIComponent(id)}`,
    orderDetailSchema,
  );
  if (!data) notFound();
  const { order } = data;

  return (
    <>
      <Link href="/pedidos" className="text-sm text-muted hover:text-ink">
        ← Pedidos
      </Link>
      <div className="mt-3">
        <PageHeader
          title={`Pedido ${order.number}`}
          description={
            <span className="flex items-center gap-3">
              <StatusBadge status={order.status} /> Criado em{" "}
              <span data-volatile>{formatDateTime(order.createdAt)}</span>
            </span>
          }
        />
      </div>

      <div className="grid gap-6 lg:grid-cols-[1fr_340px]">
        <div className="space-y-6">
          <Card title={`Itens (${order.items.reduce((n, i) => n + i.quantity, 0)})`}>
            <ul className="-my-2 divide-y divide-line text-sm">
              {order.items.map((item) => (
                <li key={item.skuId} className="flex items-center gap-3 py-2.5">
                  <div className="relative h-14 w-10 shrink-0 overflow-hidden rounded bg-sand">
                    {item.imageUrl && (
                      <Image
                        src={item.imageUrl}
                        alt=""
                        fill
                        unoptimized
                        sizes="40px"
                        className="object-cover"
                      />
                    )}
                  </div>
                  <div className="flex-1">
                    <p className="font-medium">{item.productName}</p>
                    <p className="text-xs text-muted">
                      {item.color} · {item.size === "U" ? "Único" : item.size} ·{" "}
                      <span className="font-mono">{item.skuCode}</span>
                    </p>
                  </div>
                  <p className="tabular text-right">
                    {item.quantity} × {formatPrice(item.unitPrice)}
                    <span className="block font-medium">{formatPrice(item.lineTotal)}</span>
                  </p>
                </li>
              ))}
            </ul>
            <dl className="tabular mt-4 space-y-1 border-t border-line pt-3 text-sm">
              <div className="flex justify-between">
                <dt className="text-muted">Subtotal</dt>
                <dd>{formatPrice(order.subtotal)}</dd>
              </div>
              <div className="flex justify-between">
                <dt className="text-muted">Frete ({order.shipping.region})</dt>
                <dd>{order.shipping.cost === 0 ? "Grátis" : formatPrice(order.shipping.cost)}</dd>
              </div>
              <div className="flex justify-between font-semibold">
                <dt>Total</dt>
                <dd>{formatPrice(order.total)}</dd>
              </div>
            </dl>
          </Card>

          <Card
            title="Histórico"
            actions={
              <Link
                href="/atividade?modulo=orders"
                className="text-xs text-accent underline-offset-4 hover:underline"
              >
                Ver na Atividade →
              </Link>
            }
          >
            <ol className="space-y-3 border-l border-line pl-4 text-sm">
              {order.history.map((h, i) => (
                <li key={i}>
                  <p className="font-medium">{ORDER_STATUS_LABEL[h.status]}</p>
                  <p data-volatile className="text-xs text-muted">
                    {formatDateTime(h.at)}
                    {h.note ? ` · ${h.note}` : ""}
                  </p>
                  {/* Quem fez a mudança (R12); históricos anteriores à auditoria não têm ator. */}
                  {(h.actorKind || h.actorName) && (
                    <p className="mt-1 flex flex-wrap items-center gap-2 text-[13px]">
                      {h.actorKind && <ActorBadge kind={h.actorKind} />}
                      {h.actorName && <span>{h.actorName}</span>}
                    </p>
                  )}
                </li>
              ))}
            </ol>
          </Card>
        </div>

        <div className="space-y-6">
          <Card title="Ações">
            <OrderActions
              orderId={order.id}
              status={order.status}
              trackingCode={order.trackingCode}
            />
          </Card>

          <Card title="Cliente">
            <p className="text-sm font-medium">{data.customerName ?? "—"}</p>
            <p className="text-sm text-muted">{data.customerEmail ?? "—"}</p>
          </Card>

          <Card title="Entrega">
            <address className="text-sm leading-relaxed not-italic">
              {order.address.recipientName}
              <br />
              {order.address.street}, {order.address.number}
              {order.address.complement ? ` – ${order.address.complement}` : ""}
              <br />
              {order.address.district} · {order.address.city}/{order.address.state}
              <br />
              CEP {order.address.postalCode.replace(/(\d{5})(\d{3})/, "$1-$2")}
            </address>
            <p className="mt-2 text-xs text-muted">
              Prazo: até {order.shipping.estimatedDays} dias úteis
            </p>
          </Card>

          {data.paymentId && (
            <Card title="Pagamento">
              <p className="font-mono text-xs break-all text-muted" data-volatile>
                {data.paymentId}
              </p>
            </Card>
          )}
        </div>
      </div>
    </>
  );
}
