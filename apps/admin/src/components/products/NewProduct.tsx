"use client";

import { useRouter } from "next/navigation";
import type { CategoryOption } from "@/lib/catalog-options";
import type { Collection } from "@/lib/schemas";
import { ProductForm } from "./ProductForm";

export function NewProduct({
  categories,
  collections,
}: {
  categories: CategoryOption[];
  collections: Collection[];
}) {
  const router = useRouter();
  return (
    <ProductForm
      categories={categories}
      collections={collections}
      onSaved={(p) => router.push(`/produtos/${p.id}?novo=1`)}
    />
  );
}
