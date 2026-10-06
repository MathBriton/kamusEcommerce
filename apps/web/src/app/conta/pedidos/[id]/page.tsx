import type { Metadata } from "next";
import Image from "next/image";
import Link from "next/link";
import { notFound, redirect } from "next/navigation";
import { OrderActions } from "@/components/shop/OrderActions";
import { OrderStatusBadge } from "@/components/shop/OrderStatusBadge";
import { ApiError } from "@/lib/api";
import { formatPrice } from "@/lib/format";
import { apiGetWithSession } from "@/lib/server-api";
import { ORDER_STATUS_LABEL, orderDetailSchema } from "@/lib/shop";

export const metadata: Metadata = { title: "Pedido", robots: { index: false } };

const dateTime = new Intl.DateTimeFormat("pt-BR", { dateStyle: "short", timeStyle: "short" });

export default async function OrderPage(props: PageProps<"/conta/pedidos/[id]">) {
  const { id } = await props.params;
  const { novo } = await props.searchParams;

  let order;
  try {
    order = await apiGetWithSession(`/api/orders/${encodeURIComponent(id)}`, orderDetailSchema);
  } catch (error) {
    if (error instanceof ApiError && error.status === 401)
      redirect(`/entrar?next=/conta/pedidos/${id}`);
    if (error instanceof ApiError && error.status === 400) notFound();
    throw error;
  }
  if (!order) notFound();

  const simulateFulfillment = process.env.ENABLE_FULFILLMENT_SIMULATION === "true";

  return (
    <div className="mx-auto max-w-4xl px-4 py-10">
      <Link href="/conta" className="text-sm text-muted hover:text-ink">
        ← Minha conta
      </Link>

      {novo && order.status === "AwaitingPayment" && (
        <p className="mt-6 bg-sand px-4 py-3 text-sm">
          Recebemos seu pedido! Estamos aguardando a confirmação do pagamento…
        </p>
      )}

      <div className="mt-6 mb-8 flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="font-display text-4xl">Pedido {order.number}</h1>
          <p className="mt-1 text-sm text-muted">
            Feito em {dateTime.format(new Date(order.createdAt))}
          </p>
        </div>
        <OrderStatusBadge status={order.status} />
      </div>

      <OrderActions
        orderId={order.id}
        status={order.status}
        canCancel={order.canCancel}
        simulateFulfillment={simulateFulfillment}
      />

      <div className="grid gap-10 md:grid-cols-[1fr_280px]">
        <section>
          <h2 className="mb-4 font-display text-2xl">Itens</h2>
          <ul className="divide-y divide-line border-y border-line">
            {order.items.map((item) => (
              <li key={item.skuId} className="flex gap-4 py-4 text-sm">
                <div className="relative h-20 w-16 shrink-0 bg-sand">
                  {item.imageUrl && (
                    <Image
                      src={item.imageUrl}
                      alt=""
                      fill
                      unoptimized
                      sizes="64px"
                      className="object-cover"
                    />
                  )}
                </div>
                <div className="flex-1">
                  <p className="font-medium">{item.productName}</p>
                  <p className="text-muted">
                    {item.color} · {item.size === "U" ? "Único" : item.size} · {item.quantity}x{" "}
                    {formatPrice(item.unitPrice)}
                  </p>
                  <p className="text-xs text-muted">{item.skuCode}</p>
                </div>
                <p>{formatPrice(item.lineTotal)}</p>
              </li>
            ))}
          </ul>
          <dl className="mt-4 space-y-1 text-sm">
            <div className="flex justify-between">
              <dt>Subtotal</dt>
              <dd>{formatPrice(order.subtotal)}</dd>
            </div>
            <div className="flex justify-between">
              <dt>Frete ({order.shipping.region})</dt>
              <dd>{order.shipping.cost === 0 ? "Grátis" : formatPrice(order.shipping.cost)}</dd>
            </div>
            <div className="flex justify-between text-base font-medium">
              <dt>Total</dt>
              <dd>{formatPrice(order.total)}</dd>
            </div>
          </dl>
        </section>

        <aside className="space-y-8 text-sm">
          <section>
            <h2 className="mb-2 font-display text-xl">Entrega</h2>
            <address className="leading-relaxed text-muted not-italic">
              {order.address.recipientName}
              <br />
              {order.address.street}, {order.address.number}
              {order.address.complement ? ` – ${order.address.complement}` : ""}
              <br />
              {order.address.district} · {order.address.city}/{order.address.state}
              <br />
              CEP {order.address.postalCode.replace(/(\d{5})(\d{3})/, "$1-$2")}
            </address>
            <p className="mt-2">
              Prazo: até {order.shipping.estimatedDays} dias úteis após o pagamento.
            </p>
          </section>

          <section>
            <h2 className="mb-2 font-display text-xl">Acompanhamento</h2>
            <ol className="space-y-3 border-l border-line pl-4">
              {order.history.map((h, index) => (
                <li key={index}>
                  <p className="font-medium">{ORDER_STATUS_LABEL[h.status]}</p>
                  <p className="text-xs text-muted">{dateTime.format(new Date(h.at))}</p>
                  {h.note && <p className="text-xs text-muted">{h.note}</p>}
                </li>
              ))}
            </ol>
          </section>
        </aside>
      </div>
    </div>
  );
}
