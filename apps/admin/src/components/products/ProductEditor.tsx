"use client";

import Image from "next/image";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState, type ReactNode } from "react";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import { Icon } from "@/components/icons";
import {
  Alert,
  Button,
  Card,
  Input,
  PageHeader,
  PublishedBadge,
  fieldErrors,
} from "@/components/ui";
import type { CategoryOption } from "@/lib/catalog-options";
import { formatPrice } from "@/lib/format";
import {
  productDetailSchema,
  readProblem,
  skuFormSchema,
  type Collection,
  type ProductDetail,
  type Sku,
} from "@/lib/schemas";
import { TRASH_RETENTION_DAYS } from "@/lib/trash";
import { DeleteProductButton } from "./DeleteProductButton";
import { ProductForm } from "./ProductForm";

export type ProductTab = "dados" | "historico";

type Props = {
  initial: ProductDetail;
  categories: CategoryOption[];
  collections: Collection[];
  storeUrl: string;
  /** Aba ativa (?aba=historico). */
  tab?: ProductTab;
  /** Conteúdo da aba Histórico, renderizado no servidor. */
  history?: ReactNode;
  /** Quantas entradas o histórico tem (só conhecido na aba Histórico). */
  historyCount?: number | null;
};

type Feedback = { ok: boolean; text: string } | null;

/** Nome legível do SKU nas mensagens: "Areia · M". */
const skuLabel = (sku: Sku) => `${sku.color} · ${sku.size === "U" ? "Único" : sku.size}`;

/** Editor completo: dados, publicação, variações (SKU), preços, estoque e imagens por cor. */
export function ProductEditor({
  initial,
  categories,
  collections,
  storeUrl,
  tab = "dados",
  history,
  historyCount = null,
}: Props) {
  const router = useRouter();
  const [product, setProduct] = useState(initial);
  const [feedback, setFeedback] = useState<Feedback>(null);
  const [busy, setBusy] = useState(false);
  const [skuToDelete, setSkuToDelete] = useState<Sku | null>(null);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  /** Executa uma chamada que devolve o produto atualizado. */
  async function call(url: string, init: RequestInit, success: string) {
    setBusy(true);
    setFeedback(null);
    try {
      const response = await fetch(url, init);
      if (!response.ok) {
        setFeedback({ ok: false, text: await readProblem(response) });
        return false;
      }
      setProduct(await response.json());
      setFeedback({ ok: true, text: success });
      // Na aba Histórico, a nova entrada da auditoria aparece sem recarregar a página.
      if (tab === "historico") router.refresh();
      return true;
    } finally {
      setBusy(false);
    }
  }

  const json = (method: string, body: unknown): RequestInit => ({
    method,
    headers: { "content-type": "application/json" },
    body: JSON.stringify(body),
  });

  /** Ajuste de estoque é do módulo Inventory; depois recarrega o produto para mostrar o disponível. */
  async function adjustStock(sku: Sku, quantity: number) {
    setBusy(true);
    setFeedback(null);
    try {
      const response = await fetch(
        `/api/admin/inventory/skus/${sku.id}`,
        json("PUT", { quantity }),
      );
      if (!response.ok) {
        setFeedback({ ok: false, text: await readProblem(response) });
        return false;
      }
    } finally {
      setBusy(false);
    }
    return call(
      `/api/admin/catalog/products/${product.id}`,
      { method: "GET" },
      `Estoque de ${skuLabel(sku)} ajustado.`,
    );
  }

  /** SKU vai para a lixeira; a API devolve o produto atualizado ou 204 (aí recarrega). */
  async function deleteSku(sku: Sku) {
    setBusy(true);
    setDeleteError(null);
    try {
      const response = await fetch(`/api/admin/catalog/skus/${sku.id}`, { method: "DELETE" });
      if (!response.ok) {
        setDeleteError(await readProblem(response));
        return;
      }
      const body = response.status === 204 ? null : await response.json().catch(() => null);
      const updated = productDetailSchema.safeParse(body);
      if (updated.success) {
        setProduct(updated.data);
      } else {
        const reload = await fetch(`/api/admin/catalog/products/${product.id}`);
        if (reload.ok) setProduct(productDetailSchema.parse(await reload.json()));
      }
      setSkuToDelete(null);
      setFeedback({ ok: true, text: `${skuLabel(sku)} foi para a lixeira.` });
      router.refresh();
    } catch {
      setDeleteError("Não foi possível falar com o servidor. Tente novamente.");
    } finally {
      setBusy(false);
    }
  }

  const colors = [...new Map(product.skus.map((s) => [s.color, s.colorHex])).entries()];
  const tabClass = (active: boolean) =>
    `rounded px-3 py-1 ${active ? "bg-ink text-paper" : "text-muted hover:text-ink"}`;

  return (
    <div className="mt-3 space-y-6">
      <PageHeader
        title={product.name}
        description={
          <span className="flex items-center gap-3">
            <PublishedBadge active={product.isActive} />
            <span className="font-mono text-xs">/{product.path}</span>
          </span>
        }
        actions={
          <>
            {product.isActive && (
              <a
                href={`${storeUrl}/${product.path}`}
                target="_blank"
                rel="noreferrer"
                className="text-sm text-muted underline underline-offset-4 hover:text-ink"
              >
                Ver na loja ↗
              </a>
            )}
            {product.isActive ? (
              <Button
                variant="secondary"
                disabled={busy}
                onClick={() =>
                  call(
                    `/api/admin/catalog/products/${product.id}/unpublish`,
                    { method: "POST" },
                    "Produto voltou para rascunho.",
                  )
                }
              >
                Despublicar
              </Button>
            ) : (
              <Button
                disabled={busy}
                onClick={() =>
                  call(
                    `/api/admin/catalog/products/${product.id}/publish`,
                    { method: "POST" },
                    "Produto publicado na loja.",
                  )
                }
              >
                Publicar
              </Button>
            )}
            <DeleteProductButton
              product={{
                id: product.id,
                name: product.name,
                skuCount: product.skus.length,
                imageCount: product.images.length,
              }}
            />
          </>
        }
      />

      <nav
        aria-label="Seções do produto"
        className="flex w-fit gap-1 rounded-md border border-line bg-surface p-1 text-sm"
      >
        <Link
          href={`/produtos/${product.id}`}
          aria-current={tab === "dados" ? "page" : undefined}
          className={tabClass(tab === "dados")}
        >
          Dados e estoque
        </Link>
        <Link
          href={`/produtos/${product.id}?aba=historico`}
          aria-current={tab === "historico" ? "page" : undefined}
          className={tabClass(tab === "historico")}
        >
          Histórico
          {historyCount !== null && (
            <span className="tabular ml-1.5 opacity-75">{historyCount}</span>
          )}
        </Link>
      </nav>

      {feedback && <Alert tone={feedback.ok ? "success" : "error"}>{feedback.text}</Alert>}

      {tab === "historico" ? (
        history
      ) : (
        <div className="grid gap-6 lg:grid-cols-[1fr_380px]">
          <div className="min-w-0 space-y-6">
            <Card title="Variações e estoque">
              {product.skus.length === 0 ? (
                <p className="text-sm text-muted">
                  Nenhum SKU ainda. Adicione uma cor com seus tamanhos para poder publicar.
                </p>
              ) : (
                <SkuTable
                  skus={product.skus}
                  busy={busy}
                  call={call}
                  json={json}
                  adjustStock={adjustStock}
                  onDelete={(sku) => {
                    setDeleteError(null);
                    setSkuToDelete(sku);
                  }}
                />
              )}
            </Card>

            <Card title="Adicionar cor e tamanhos">
              <AddSkusForm
                busy={busy}
                onSubmit={(body) =>
                  call(
                    `/api/admin/catalog/products/${product.id}/skus`,
                    json("POST", body),
                    "Variações adicionadas.",
                  )
                }
              />
            </Card>

            <Card title="Imagens">
              {colors.length === 0 ? (
                <p className="text-sm text-muted">
                  As imagens são organizadas por cor: cadastre um SKU primeiro.
                </p>
              ) : (
                <div className="space-y-6">
                  {colors.map(([color, hex]) => (
                    <ColorImages
                      key={color}
                      color={color}
                      hex={hex}
                      images={product.images.filter((i) => i.color === color)}
                      busy={busy}
                      onUpload={(file) => {
                        const form = new FormData();
                        form.append("file", file);
                        form.append("color", color);
                        return call(
                          `/api/admin/catalog/products/${product.id}/images`,
                          { method: "POST", body: form },
                          "Imagem enviada.",
                        );
                      }}
                      onRemove={async (imageId) => {
                        const removed = await call(
                          `/api/admin/catalog/products/${product.id}/images/${imageId}`,
                          { method: "DELETE" },
                          "Imagem movida para a lixeira.",
                        );
                        if (removed) router.refresh();
                      }}
                    />
                  ))}
                </div>
              )}
            </Card>
          </div>

          <Card title="Dados gerais" className="h-fit">
            <ProductForm
              product={product}
              categories={categories}
              collections={collections}
              onSaved={setProduct}
            />
          </Card>
        </div>
      )}

      <ConfirmDialog
        open={skuToDelete !== null}
        title={skuToDelete ? `Excluir ${skuLabel(skuToDelete)}?` : "Excluir variação?"}
        confirmLabel="Mover para a lixeira"
        busyLabel="Movendo…"
        busy={busy}
        error={deleteError}
        onConfirm={() => skuToDelete && deleteSku(skuToDelete)}
        onClose={() => setSkuToDelete(null)}
      >
        <p>
          A variação sai da loja na hora e vai para a Lixeira, com o estoque que tem. Você pode
          restaurá-la em até {TRASH_RETENTION_DAYS} dias; pedidos que já têm este SKU não mudam.
        </p>
      </ConfirmDialog>
    </div>
  );
}

type CallFn = (url: string, init: RequestInit, success: string) => Promise<boolean>;
type JsonFn = (method: string, body: unknown) => RequestInit;

type AdjustFn = (sku: Sku, quantity: number) => Promise<boolean>;
type DeleteFn = (sku: Sku) => void;

function SkuTable({
  skus,
  busy,
  call,
  json,
  adjustStock,
  onDelete,
}: {
  skus: Sku[];
  busy: boolean;
  call: CallFn;
  json: JsonFn;
  adjustStock: AdjustFn;
  onDelete: DeleteFn;
}) {
  return (
    <div className="-mx-5 -my-5 overflow-x-auto">
      <table className="w-full text-sm">
        <thead className="border-b border-line bg-paper text-left text-xs text-muted">
          <tr>
            <th className="py-2 pr-3 pl-4 font-medium">SKU</th>
            <th className="px-3 py-2 font-medium">Preço</th>
            <th className="px-3 py-2 font-medium">Promocional</th>
            <th className="px-3 py-2 font-medium">Físico</th>
            <th className="px-3 py-2 text-right font-medium">Reservado</th>
            <th className="px-3 py-2 text-right font-medium">Disponível</th>
            <th className="w-10 px-1 py-2">
              <span className="sr-only">Ações</span>
            </th>
          </tr>
        </thead>
        <tbody className="divide-y divide-line">
          {skus.map((sku) => (
            <SkuRow
              key={`${sku.id}-${sku.quantity}-${sku.price}-${sku.salePrice}`}
              sku={sku}
              busy={busy}
              call={call}
              json={json}
              adjustStock={adjustStock}
              onDelete={onDelete}
            />
          ))}
        </tbody>
      </table>
    </div>
  );
}

function SkuRow({
  sku,
  busy,
  call,
  json,
  adjustStock,
  onDelete,
}: {
  sku: Sku;
  busy: boolean;
  call: CallFn;
  json: JsonFn;
  adjustStock: AdjustFn;
  onDelete: DeleteFn;
}) {
  const [price, setPrice] = useState(String(sku.price));
  const [sale, setSale] = useState(sku.salePrice === null ? "" : String(sku.salePrice));
  const [stock, setStock] = useState(String(sku.quantity));
  const priceChanged =
    Number(price) !== sku.price || (sale === "" ? null : Number(sale)) !== sku.salePrice;
  const stockChanged = Number(stock) !== sku.quantity;
  const cell =
    "h-8 w-20 rounded border border-line bg-surface px-2 text-sm tabular focus:border-ink focus:outline-none";

  return (
    <tr>
      <td className="px-3 py-2">
        <span className="flex items-center gap-2">
          <span
            className="size-3 rounded-full border border-black/15"
            style={{ backgroundColor: sku.colorHex }}
            aria-hidden
          />
          <span className="min-w-0">
            <span className="block font-medium whitespace-nowrap">
              {sku.color} · {sku.size === "U" ? "Único" : sku.size}
            </span>
            <span
              data-volatile
              className="block max-w-32 truncate font-mono text-[11px] text-muted"
              title={sku.code}
            >
              {sku.code}
            </span>
          </span>
        </span>
      </td>
      <td className="px-3 py-2">
        <input
          aria-label={`Preço de ${sku.code}`}
          inputMode="decimal"
          className={cell}
          value={price}
          onChange={(e) => setPrice(e.target.value)}
        />
      </td>
      <td className="px-3 py-2">
        <span className="flex items-center gap-2">
          <input
            aria-label={`Preço promocional de ${sku.code}`}
            inputMode="decimal"
            placeholder="—"
            className={cell}
            value={sale}
            onChange={(e) => setSale(e.target.value)}
          />
          {priceChanged && (
            <Button
              variant="secondary"
              disabled={busy}
              className="h-8"
              onClick={() =>
                call(
                  `/api/admin/catalog/skus/${sku.id}/prices`,
                  json("PUT", {
                    price: Number(price.replace(",", ".")),
                    salePrice: sale === "" ? null : Number(sale.replace(",", ".")),
                  }),
                  `Preço de ${skuLabel(sku)} atualizado.`,
                )
              }
            >
              Salvar
            </Button>
          )}
        </span>
        {sku.salePrice !== null && !priceChanged && (
          <span className="mt-0.5 block text-[11px] text-muted">de {formatPrice(sku.price)}</span>
        )}
      </td>
      <td className="px-3 py-2">
        <span className="flex items-center gap-2">
          <input
            aria-label={`Estoque físico de ${sku.code}`}
            inputMode="numeric"
            className={`${cell} w-16`}
            value={stock}
            onChange={(e) => setStock(e.target.value)}
          />
          {stockChanged && (
            <Button
              variant="secondary"
              disabled={busy}
              className="h-8"
              onClick={async () => {
                if (!(await adjustStock(sku, Number(stock)))) setStock(String(sku.quantity));
              }}
            >
              Salvar
            </Button>
          )}
        </span>
      </td>
      <td className="tabular px-3 py-2 text-right text-muted">{sku.reserved}</td>
      <td
        className={`tabular px-3 py-2 text-right font-medium ${sku.available === 0 ? "text-sale" : ""}`}
      >
        {sku.available}
      </td>
      <td className="px-1 py-2 text-right">
        <button
          type="button"
          aria-label={`Excluir ${skuLabel(sku)}`}
          title="Excluir variação"
          disabled={busy}
          onClick={() => onDelete(sku)}
          className="inline-flex size-8 items-center justify-center rounded-md text-muted transition-colors hover:bg-sale/10 hover:text-sale disabled:cursor-not-allowed disabled:opacity-50"
        >
          <Icon name="trash" />
        </button>
      </td>
    </tr>
  );
}

function AddSkusForm({
  busy,
  onSubmit,
}: {
  busy: boolean;
  onSubmit: (body: unknown) => Promise<boolean>;
}) {
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [hex, setHex] = useState("#1f1f1f");

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    const parsed = skuFormSchema.safeParse({
      ...Object.fromEntries(new FormData(form)),
      colorHex: hex,
    });
    if (!parsed.success) {
      setErrors(fieldErrors(parsed.error.issues));
      return;
    }
    setErrors({});
    if (await onSubmit(parsed.data)) form.reset();
  }

  return (
    <form onSubmit={submit} noValidate className="grid gap-4 sm:grid-cols-6">
      <Input
        id="color"
        label="Cor"
        placeholder="Terracota"
        className="sm:col-span-2"
        error={errors.color}
      />
      <div className="sm:col-span-1">
        <label htmlFor="colorHex" className="mb-1 block text-xs font-medium text-muted">
          Amostra
        </label>
        <input
          id="colorHex"
          type="color"
          value={hex}
          onChange={(e) => setHex(e.target.value)}
          className="h-9 w-full cursor-pointer rounded-md border border-line bg-surface p-1"
        />
      </div>
      <Input
        id="sizes"
        label="Tamanhos"
        placeholder="P M G GG"
        hint="Separados por espaço"
        className="sm:col-span-3"
        error={errors.sizes}
      />
      <Input
        id="price"
        label="Preço (R$)"
        inputMode="decimal"
        placeholder="199.90"
        className="sm:col-span-2"
        error={errors.price}
      />
      <Input
        id="salePrice"
        label="Promocional (opcional)"
        inputMode="decimal"
        className="sm:col-span-2"
        error={errors.salePrice}
      />
      <Input
        id="initialStock"
        label="Estoque por tamanho"
        inputMode="numeric"
        defaultValue="10"
        className="sm:col-span-2"
        error={errors.initialStock}
      />
      <div className="flex justify-end sm:col-span-6">
        <Button type="submit" disabled={busy}>
          Adicionar variações
        </Button>
      </div>
    </form>
  );
}

function ColorImages({
  color,
  hex,
  images,
  busy,
  onUpload,
  onRemove,
}: {
  color: string;
  hex: string;
  images: ProductDetail["images"];
  busy: boolean;
  onUpload: (file: File) => Promise<boolean>;
  onRemove: (imageId: string) => void;
}) {
  return (
    <div>
      <p className="mb-2 flex items-center gap-2 text-sm font-medium">
        <span
          className="size-3 rounded-full border border-black/15"
          style={{ backgroundColor: hex }}
          aria-hidden
        />
        {color}
        <span className="text-xs font-normal text-muted">({images.length})</span>
      </p>
      <div className="flex flex-wrap gap-3">
        {images.map((image) => (
          <figure
            key={image.id}
            className="group relative h-32 w-24 overflow-hidden rounded-md border border-line bg-sand"
          >
            <Image
              src={image.url}
              alt={image.alt}
              fill
              unoptimized
              sizes="96px"
              className="object-cover"
            />
            <button
              type="button"
              disabled={busy}
              onClick={() => onRemove(image.id)}
              className="absolute inset-x-1 bottom-1 rounded bg-surface/95 py-1 text-xs text-sale opacity-0 transition-opacity group-hover:opacity-100 focus:opacity-100"
            >
              Remover
            </button>
          </figure>
        ))}
        <label className="flex h-32 w-24 cursor-pointer flex-col items-center justify-center gap-1 rounded-md border border-dashed border-line text-center text-xs text-muted hover:border-ink hover:text-ink">
          <span className="text-lg" aria-hidden>
            +
          </span>
          Enviar foto
          <input
            type="file"
            accept="image/jpeg,image/png,image/webp"
            className="sr-only"
            disabled={busy}
            onChange={async (e) => {
              const file = e.target.files?.[0];
              if (file) await onUpload(file);
              e.target.value = "";
            }}
          />
        </label>
      </div>
      <p className="mt-2 text-xs text-muted">JPEG, PNG ou WebP, até 5 MB.</p>
    </div>
  );
}
