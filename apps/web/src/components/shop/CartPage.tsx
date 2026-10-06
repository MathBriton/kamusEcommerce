"use client";

import Image from "next/image";
import Link from "next/link";
import { useEffect, useState } from "react";
import { Price } from "@/components/catalog/Price";
import { formatPrice } from "@/lib/format";
import { cartSchema, notifyCartChanged, readProblem, type Cart, type CartItem } from "@/lib/shop";
import { Alert } from "./ui";

const ISSUE_MESSAGE: Record<NonNullable<CartItem["issue"]>, (item: CartItem) => string> = {
  unavailable: () => "Este produto não está mais disponível.",
  out_of_stock: () => "Esgotou. Remova o item para continuar.",
  insufficient_stock: (i) => `Só restam ${i.available} unidade(s). Ajuste a quantidade.`,
  price_changed: (i) =>
    `O preço mudou de ${formatPrice(i.priceWhenAdded)} para ${formatPrice(i.unitPrice)}.`,
};

export function CartPage() {
  const [cart, setCart] = useState<Cart | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null);

  useEffect(() => {
    fetch("/api/cart")
      .then((r) => r.json())
      .then((data) => setCart(cartSchema.parse(data)))
      .catch(() => setError("Não foi possível carregar a sacola."));
  }, []);

  async function mutate(skuId: string, request: Promise<Response>) {
    setBusy(skuId);
    setError(null);
    try {
      const response = await request;
      if (!response.ok) {
        setError(await readProblem(response));
        return;
      }
      setCart(cartSchema.parse(await response.json()));
      notifyCartChanged();
    } finally {
      setBusy(null);
    }
  }

  const setQuantity = (skuId: string, quantity: number) =>
    mutate(
      skuId,
      fetch(`/api/cart/items/${skuId}`, {
        method: "PUT",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ quantity }),
      }),
    );

  const remove = (skuId: string) =>
    mutate(skuId, fetch(`/api/cart/items/${skuId}`, { method: "DELETE" }));

  if (!cart) {
    return error ? <Alert>{error}</Alert> : <p className="text-muted">Carregando…</p>;
  }

  if (cart.items.length === 0) {
    return (
      <div className="py-16 text-center">
        <p className="mb-6 text-muted">Sua sacola está vazia.</p>
        <Link href="/" className="bg-ink px-8 py-3 text-sm tracking-widest text-paper uppercase">
          Continuar comprando
        </Link>
      </div>
    );
  }

  return (
    <div className="grid gap-10 lg:grid-cols-[1fr_320px]">
      <div>
        {error && <Alert>{error}</Alert>}
        <ul className="divide-y divide-line border-y border-line">
          {cart.items.map((item) => (
            <li key={item.skuId} className="flex gap-4 py-5">
              <div className="relative h-32 w-24 shrink-0 bg-sand">
                {item.imageUrl && (
                  <Image
                    src={item.imageUrl}
                    alt={item.productName}
                    fill
                    unoptimized
                    sizes="96px"
                    className="object-cover"
                  />
                )}
              </div>
              <div className="flex flex-1 flex-col gap-1 text-sm">
                <Link href={`/${item.productPath}`} className="font-medium hover:text-accent">
                  {item.productName}
                </Link>
                <p className="text-muted">
                  {item.color} · Tamanho {item.size === "U" ? "único" : item.size}
                </p>
                <Price
                  price={item.listPrice}
                  salePrice={item.unitPrice < item.listPrice ? item.unitPrice : null}
                />
                {item.issue && (
                  <p className={item.issue === "price_changed" ? "text-accent" : "text-sale"}>
                    {ISSUE_MESSAGE[item.issue](item)}
                  </p>
                )}
                <div className="mt-auto flex items-center gap-4 pt-2">
                  <label className="flex items-center gap-2">
                    <span className="sr-only">Quantidade de {item.productName}</span>
                    <select
                      value={item.quantity}
                      disabled={busy === item.skuId || item.issue === "unavailable"}
                      onChange={(e) => setQuantity(item.skuId, Number(e.target.value))}
                      className="border border-line bg-surface px-2 py-1"
                    >
                      {Array.from({ length: Math.max(10, item.quantity) }, (_, i) => i + 1).map(
                        (n) => (
                          <option key={n} value={n}>
                            {n}
                          </option>
                        ),
                      )}
                    </select>
                  </label>
                  <button
                    type="button"
                    onClick={() => remove(item.skuId)}
                    disabled={busy === item.skuId}
                    className="text-muted underline underline-offset-4 hover:text-sale"
                  >
                    Remover
                  </button>
                </div>
              </div>
              <p className="text-sm font-medium">{formatPrice(item.lineTotal)}</p>
            </li>
          ))}
        </ul>
      </div>

      <aside className="h-fit space-y-4 bg-surface p-6">
        <h2 className="font-display text-2xl">Resumo</h2>
        <dl className="space-y-2 text-sm">
          <div className="flex justify-between">
            <dt>Subtotal ({cart.itemCount} itens)</dt>
            <dd>{formatPrice(cart.subtotal)}</dd>
          </div>
          <div className="flex justify-between text-muted">
            <dt>Frete</dt>
            <dd>calculado no checkout</dd>
          </div>
        </dl>
        {cart.hasIssues ? (
          <p className="text-sm text-sale">Resolva os itens indisponíveis para continuar.</p>
        ) : (
          <Link
            href="/checkout"
            className="block bg-ink py-4 text-center text-sm tracking-widest text-paper uppercase hover:bg-accent"
          >
            Finalizar compra
          </Link>
        )}
      </aside>
    </div>
  );
}
