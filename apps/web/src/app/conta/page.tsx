import type { Metadata } from "next";
import Image from "next/image";
import Link from "next/link";
import { redirect } from "next/navigation";
import { z } from "zod";
import { LogoutButton } from "@/components/shop/LogoutButton";
import { OrderStatusBadge } from "@/components/shop/OrderStatusBadge";
import { ApiError } from "@/lib/api";
import { formatPrice } from "@/lib/format";
import { apiGetWithSession } from "@/lib/server-api";
import { meSchema, orderSummarySchema } from "@/lib/shop";

export const metadata: Metadata = { title: "Minha conta", robots: { index: false } };

const dateFormat = new Intl.DateTimeFormat("pt-BR", { dateStyle: "medium" });

export default async function AccountPage() {
  let me, orders;
  try {
    [me, orders] = await Promise.all([
      apiGetWithSession("/api/identity/me", meSchema),
      apiGetWithSession("/api/orders", z.array(orderSummarySchema)),
    ]);
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) redirect("/entrar?next=/conta");
    throw error;
  }
  if (!me) redirect("/entrar?next=/conta");

  return (
    <div className="mx-auto max-w-4xl px-4 py-10">
      <div className="mb-10 flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="font-display text-4xl">Minha conta</h1>
          <p className="mt-2 text-muted">
            {me.fullName} · {me.email}
          </p>
        </div>
        <LogoutButton />
      </div>

      <h2 className="mb-4 font-display text-2xl">Pedidos</h2>
      {!orders || orders.length === 0 ? (
        <p className="text-muted">
          Você ainda não fez pedidos.{" "}
          <Link href="/" className="text-accent underline underline-offset-4">
            Ver a vitrine
          </Link>
        </p>
      ) : (
        <ul className="divide-y divide-line border-y border-line">
          {orders.map((order) => (
            <li key={order.id}>
              <Link
                href={`/conta/pedidos/${order.id}`}
                className="flex items-center gap-4 py-4 hover:bg-surface"
              >
                <div className="relative h-16 w-12 shrink-0 bg-sand">
                  {order.imageUrl && (
                    <Image
                      src={order.imageUrl}
                      alt=""
                      fill
                      unoptimized
                      sizes="48px"
                      className="object-cover"
                    />
                  )}
                </div>
                <div className="flex-1 text-sm">
                  <p className="font-medium">Pedido {order.number}</p>
                  <p className="text-muted">
                    <span data-volatile>{dateFormat.format(new Date(order.createdAt))}</span> ·{" "}
                    {order.itemCount} item(ns)
                  </p>
                </div>
                <OrderStatusBadge status={order.status} />
                <p className="w-28 text-right text-sm">{formatPrice(order.total)}</p>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
