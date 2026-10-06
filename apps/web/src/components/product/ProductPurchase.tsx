"use client";

import Image from "next/image";
import Link from "next/link";
import { useState } from "react";
import { Price } from "@/components/catalog/Price";
import type { ProductDetail } from "@/lib/catalog";
import { notifyCartChanged, readProblem } from "@/lib/shop";

type Props = { product: ProductDetail };

const sizeLabel = (size: string) => (size === "U" ? "Único" : size);

/** Galeria por cor, seletor de tamanho com disponibilidade e preço "de/por" do SKU escolhido. */
export function ProductPurchase({ product }: Props) {
  const firstInStock = product.colors.find((c) => c.sizes.some((s) => s.available > 0));
  const [colorName, setColorName] = useState((firstInStock ?? product.colors[0]).name);
  const [skuId, setSkuId] = useState<string | null>(null);
  const [imageIndex, setImageIndex] = useState(0);
  const [adding, setAdding] = useState(false);
  const [feedback, setFeedback] = useState<{ ok: boolean; message: string } | null>(null);

  const color = product.colors.find((c) => c.name === colorName) ?? product.colors[0];
  const sku = color.sizes.find((s) => s.skuId === skuId) ?? null;
  const image = color.images[imageIndex] ?? color.images[0];
  const soldOut = color.sizes.every((s) => s.available === 0);

  function selectColor(name: string) {
    setColorName(name);
    setSkuId(null);
    setImageIndex(0);
    setFeedback(null);
  }

  async function addToCart() {
    if (!sku) return;
    setAdding(true);
    setFeedback(null);
    try {
      const response = await fetch("/api/cart/items", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ skuId: sku.skuId, quantity: 1 }),
      });
      if (response.ok) {
        setFeedback({ ok: true, message: "Adicionado à sacola." });
        notifyCartChanged();
      } else {
        setFeedback({ ok: false, message: await readProblem(response) });
      }
    } finally {
      setAdding(false);
    }
  }

  return (
    <div className="grid gap-8 md:grid-cols-2">
      <div>
        <div className="relative aspect-[3/4] overflow-hidden bg-sand">
          {image && (
            <Image
              src={image.url}
              alt={image.alt}
              fill
              unoptimized
              priority
              sizes="(min-width: 768px) 50vw, 100vw"
              className="object-cover"
            />
          )}
        </div>
        {color.images.length > 1 && (
          <ul className="mt-3 flex gap-3" aria-label="Imagens do produto">
            {color.images.map((img, index) => (
              <li key={img.url}>
                <button
                  type="button"
                  onClick={() => setImageIndex(index)}
                  aria-label={`Ver imagem ${index + 1}`}
                  aria-current={index === imageIndex}
                  className={`relative block h-24 w-18 overflow-hidden border ${index === imageIndex ? "border-ink" : "border-transparent"}`}
                >
                  <Image
                    src={img.url}
                    alt=""
                    fill
                    unoptimized
                    sizes="72px"
                    className="object-cover"
                  />
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>

      <div className="space-y-6">
        <div>
          <p className="mb-1 text-sm tracking-widest text-muted uppercase">{product.brand}</p>
          <h1 className="font-display text-4xl leading-tight">{product.name}</h1>
        </div>

        <Price
          price={sku?.price ?? product.price}
          salePrice={sku ? sku.salePrice : product.salePrice}
          size="lg"
        />

        <fieldset>
          <legend className="mb-2 text-sm">
            Cor: <span className="font-medium">{color.name}</span>
          </legend>
          <div className="flex flex-wrap gap-3">
            {product.colors.map((c) => (
              <button
                key={c.name}
                type="button"
                onClick={() => selectColor(c.name)}
                aria-pressed={c.name === color.name}
                aria-label={c.name}
                title={c.name}
                className={`size-11 rounded-full border-2 p-1 ${c.name === color.name ? "border-ink" : "border-transparent"}`}
              >
                <span
                  className="block size-full rounded-full border border-black/15"
                  style={{ backgroundColor: c.hex }}
                />
              </button>
            ))}
          </div>
        </fieldset>

        <fieldset>
          <legend className="mb-2 text-sm">Tamanho</legend>
          <div className="flex flex-wrap gap-2">
            {color.sizes.map((s) => {
              const unavailable = s.available === 0;
              const selected = s.skuId === skuId;
              return (
                <button
                  key={s.skuId}
                  type="button"
                  disabled={unavailable}
                  onClick={() => {
                    setSkuId(s.skuId);
                    setFeedback(null);
                  }}
                  aria-pressed={selected}
                  aria-label={`${sizeLabel(s.size)}${unavailable ? " (esgotado)" : ""}`}
                  className={`min-w-12 border px-3 py-2.5 text-sm ${
                    selected
                      ? "border-ink bg-ink text-paper"
                      : unavailable
                        ? "cursor-not-allowed border-line text-muted line-through"
                        : "border-line bg-surface hover:border-ink"
                  }`}
                >
                  {sizeLabel(s.size)}
                </button>
              );
            })}
          </div>
          <p className="mt-2 min-h-5 text-sm text-muted" aria-live="polite">
            {soldOut
              ? "Esgotado nesta cor."
              : sku && sku.available <= 3
                ? `Últimas ${sku.available} unidades!`
                : ""}
          </p>
        </fieldset>

        <button
          type="button"
          disabled={!sku || adding}
          onClick={addToCart}
          className="w-full bg-ink py-4 text-sm tracking-widest text-paper uppercase transition-colors hover:bg-accent disabled:opacity-50 disabled:hover:bg-ink"
        >
          {!sku ? "Selecione um tamanho" : adding ? "Adicionando…" : "Adicionar à sacola"}
        </button>
        {feedback && (
          <p role="status" className={`text-sm ${feedback.ok ? "text-ink" : "text-sale"}`}>
            {feedback.message}{" "}
            {feedback.ok && (
              <Link href="/carrinho" className="text-accent underline underline-offset-4">
                Ver sacola
              </Link>
            )}
          </p>
        )}
      </div>
    </div>
  );
}
