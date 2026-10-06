"use client";

import Image from "next/image";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { formatPrice } from "@/lib/format";
import {
  TEST_CARDS,
  UFS,
  addressFormSchema,
  notifyCartChanged,
  placedOrderSchema,
  readProblem,
  shippingQuoteSchema,
  type Checkout,
  type ShippingQuote,
} from "@/lib/shop";
import { Alert, Field, PrimaryButton, SelectField, fieldErrors } from "./ui";

type Props = { checkout: Checkout; customerName: string };

export function CheckoutForm({ checkout, customerName }: Props) {
  const router = useRouter();
  const [state, setState] = useState("");
  const [quote, setQuote] = useState<ShippingQuote | null>(null);
  const [card, setCard] = useState<string>(TEST_CARDS[0].number);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [message, setMessage] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!state) return;
    let cancelled = false;
    fetch(`/api/checkout/shipping?state=${state}&subtotal=${checkout.subtotal}`)
      .then((r) => r.json())
      .then((data) => !cancelled && setQuote(shippingQuoteSchema.parse(data)))
      .catch(() => !cancelled && setQuote(null));
    return () => {
      cancelled = true;
    };
  }, [state, checkout.subtotal]);

  const currentQuote = state && quote?.state === state ? quote : null;
  const total = checkout.subtotal + (currentQuote?.cost ?? 0);
  const priceChanged = checkout.items.some((i) => i.issue === "price_changed");

  async function onSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const parsed = addressFormSchema.safeParse(
      Object.fromEntries(new FormData(event.currentTarget)),
    );
    if (!parsed.success) {
      setErrors(fieldErrors(parsed.error.issues));
      return;
    }
    if (!currentQuote) return;

    setErrors({});
    setMessage(null);
    setSubmitting(true);
    try {
      const response = await fetch("/api/orders", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({
          address: { ...parsed.data, complement: parsed.data.complement || null },
          cardNumber: card,
          expectedTotal: total,
        }),
      });

      if (!response.ok) {
        setMessage(await readProblem(response));
        // preço ou estoque mudou: recarrega o resumo com os valores atuais
        if (response.status === 409) router.refresh();
        return;
      }

      const order = placedOrderSchema.parse(await response.json());
      notifyCartChanged();
      router.push(`/conta/pedidos/${order.id}?novo=1`);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form onSubmit={onSubmit} noValidate className="grid gap-10 lg:grid-cols-[1fr_360px]">
      <div className="space-y-10">
        {message && <Alert>{message}</Alert>}

        <fieldset className="space-y-4">
          <legend className="mb-4 font-display text-2xl">Endereço de entrega</legend>
          <Field
            id="recipientName"
            label="Quem vai receber"
            defaultValue={customerName}
            autoComplete="name"
            error={errors.recipientName}
          />
          <div className="grid gap-4 sm:grid-cols-[180px_1fr]">
            <Field
              id="postalCode"
              label="CEP"
              inputMode="numeric"
              autoComplete="postal-code"
              placeholder="00000-000"
              error={errors.postalCode}
            />
            <Field id="street" label="Rua" autoComplete="address-line1" error={errors.street} />
          </div>
          <div className="grid gap-4 sm:grid-cols-[140px_1fr]">
            <Field id="number" label="Número" error={errors.number} />
            <Field id="complement" label="Complemento (opcional)" autoComplete="address-line2" />
          </div>
          <div className="grid gap-4 sm:grid-cols-[1fr_1fr_120px]">
            <Field id="district" label="Bairro" error={errors.district} />
            <Field id="city" label="Cidade" autoComplete="address-level2" error={errors.city} />
            <SelectField
              id="state"
              label="UF"
              value={state}
              onChange={(e) => setState(e.target.value)}
              error={errors.state}
            >
              <option value="">–</option>
              {UFS.map((uf) => (
                <option key={uf} value={uf}>
                  {uf}
                </option>
              ))}
            </SelectField>
          </div>
        </fieldset>

        <fieldset>
          <legend className="mb-2 font-display text-2xl">Pagamento</legend>
          <p className="mb-4 text-sm text-muted">
            Ambiente de demonstração: o FakePay simula um gateway real. Escolha um cartão de teste.
          </p>
          <ul className="space-y-2">
            {TEST_CARDS.map((c) => (
              <li key={c.number}>
                <label
                  className={`flex cursor-pointer items-center gap-3 border px-4 py-3 text-sm ${card === c.number ? "border-ink bg-surface" : "border-line"}`}
                >
                  <input
                    type="radio"
                    name="card"
                    value={c.number}
                    checked={card === c.number}
                    onChange={() => setCard(c.number)}
                    className="accent-ink"
                  />
                  <span className="font-mono">{c.number.replace(/(\d{4})(?=\d)/g, "$1 ")}</span>
                  <span className="text-muted">— {c.label}</span>
                </label>
              </li>
            ))}
          </ul>
        </fieldset>
      </div>

      <aside className="h-fit space-y-4 bg-surface p-6">
        <h2 className="font-display text-2xl">Resumo do pedido</h2>
        <ul className="space-y-3">
          {checkout.items.map((item) => (
            <li key={item.skuId} className="flex gap-3 text-sm">
              <div className="relative h-16 w-12 shrink-0 bg-sand">
                {item.imageUrl && (
                  <Image
                    src={item.imageUrl}
                    alt=""
                    fill
                    unoptimized
                    sizes="48px"
                    className="object-cover"
                  />
                )}
              </div>
              <div className="flex-1">
                <p>{item.productName}</p>
                <p className="text-muted">
                  {item.color} · {item.size === "U" ? "Único" : item.size} · {item.quantity}x
                </p>
              </div>
              <p>{formatPrice(item.lineTotal)}</p>
            </li>
          ))}
        </ul>
        {priceChanged && (
          <p className="text-sm text-accent">
            Alguns preços mudaram desde que você adicionou os itens.
          </p>
        )}
        <dl className="space-y-2 border-t border-line pt-4 text-sm">
          <div className="flex justify-between">
            <dt>Subtotal</dt>
            <dd>{formatPrice(checkout.subtotal)}</dd>
          </div>
          <div className="flex justify-between">
            <dt>
              Frete
              {currentQuote
                ? ` (${currentQuote.region}, até ${currentQuote.estimatedDays} dias úteis)`
                : ""}
            </dt>
            <dd>
              {currentQuote
                ? currentQuote.cost === 0
                  ? "Grátis"
                  : formatPrice(currentQuote.cost)
                : "selecione a UF"}
            </dd>
          </div>
          <div className="flex justify-between border-t border-line pt-2 text-base font-medium">
            <dt>Total</dt>
            <dd>{formatPrice(total)}</dd>
          </div>
        </dl>
        {checkout.subtotal < checkout.freeShippingThreshold && (
          <p className="text-xs text-muted">
            Frete grátis em compras a partir de {formatPrice(checkout.freeShippingThreshold)}.
          </p>
        )}
        <PrimaryButton type="submit" disabled={submitting || !currentQuote}>
          {submitting ? "Processando…" : "Pagar"}
        </PrimaryButton>
      </aside>
    </form>
  );
}
