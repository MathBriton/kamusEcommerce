import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ProductEditor } from "@/components/products/ProductEditor";
import { loadCatalogOptions } from "@/lib/catalog-options";
import { storeUrl } from "@/lib/env";
import { productDetailSchema } from "@/lib/schemas";
import { apiGetWithSession } from "@/lib/server-api";

export const metadata: Metadata = { title: "Editar produto" };

export default async function EditProductPage(props: PageProps<"/produtos/[id]">) {
  const { id } = await props.params;
  const [product, options] = await Promise.all([
    apiGetWithSession(`/api/admin/catalog/products/${encodeURIComponent(id)}`, productDetailSchema),
    loadCatalogOptions(),
  ]);
  if (!product) notFound();

  return (
    <>
      <Link href="/produtos" className="text-sm text-muted hover:text-ink">
        ← Produtos
      </Link>
      <ProductEditor initial={product} storeUrl={storeUrl} {...options} />
    </>
  );
}
