"use client";

import { useState } from "react";
import { Alert, Button, Input, Select, Textarea, fieldErrors } from "@/components/ui";
import { productFormSchema, readProblem, type Collection, type ProductDetail } from "@/lib/schemas";
import type { CategoryOption } from "@/lib/catalog-options";

type Props = {
  product?: ProductDetail;
  categories: CategoryOption[];
  collections: Collection[];
  onSaved: (product: ProductDetail) => void;
};

/** Dados gerais do produto (criação e edição). */
export function ProductForm({ product, categories, collections, onSaved }: Props) {
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null);
  const [busy, setBusy] = useState(false);

  async function onSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const parsed = productFormSchema.safeParse(
      Object.fromEntries(new FormData(event.currentTarget)),
    );
    if (!parsed.success) {
      setErrors(fieldErrors(parsed.error.issues));
      return;
    }

    setErrors({});
    setBusy(true);
    setMessage(null);
    try {
      const response = await fetch(
        product ? `/api/admin/catalog/products/${product.id}` : "/api/admin/catalog/products",
        {
          method: product ? "PUT" : "POST",
          headers: { "content-type": "application/json" },
          body: JSON.stringify(parsed.data),
        },
      );
      if (!response.ok) {
        setMessage({ ok: false, text: await readProblem(response) });
        return;
      }
      setMessage({ ok: true, text: "Salvo." });
      onSaved(await response.json());
    } finally {
      setBusy(false);
    }
  }

  return (
    <form onSubmit={onSubmit} noValidate className="space-y-4">
      {message && <Alert tone={message.ok ? "success" : "error"}>{message.text}</Alert>}
      <Input id="name" label="Nome" defaultValue={product?.name} error={errors.name} />
      <div className="grid gap-4 sm:grid-cols-2">
        <Input
          id="brand"
          label="Marca"
          defaultValue={product?.brand ?? "Kamus"}
          error={errors.brand}
        />
        <Select
          id="collectionId"
          label="Coleção (opcional)"
          defaultValue={product?.collectionId ?? ""}
        >
          <option value="">Nenhuma</option>
          {collections.map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
            </option>
          ))}
        </Select>
      </div>
      <Select
        id="categoryId"
        label="Categoria"
        defaultValue={product?.categoryId ?? ""}
        error={errors.categoryId}
      >
        <option value="">Escolha…</option>
        {categories.map((c) => (
          <option key={c.id} value={c.id}>
            {c.label}
          </option>
        ))}
      </Select>
      <Textarea
        id="description"
        label="Descrição"
        rows={5}
        defaultValue={product?.description}
        error={errors.description}
      />
      <div className="flex justify-end">
        <Button type="submit" disabled={busy}>
          {busy ? "Salvando…" : product ? "Salvar alterações" : "Criar rascunho"}
        </Button>
      </div>
    </form>
  );
}
