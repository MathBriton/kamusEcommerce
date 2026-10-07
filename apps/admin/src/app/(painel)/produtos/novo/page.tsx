import type { Metadata } from "next";
import Link from "next/link";
import { NewProduct } from "@/components/products/NewProduct";
import { Card, PageHeader } from "@/components/ui";
import { loadCatalogOptions } from "@/lib/catalog-options";

export const metadata: Metadata = { title: "Novo produto" };

export default async function NewProductPage() {
  const { categories, collections } = await loadCatalogOptions();

  return (
    <>
      <Link href="/produtos" className="text-sm text-muted hover:text-ink">
        ← Produtos
      </Link>
      <div className="mt-3">
        <PageHeader
          title="Novo produto"
          description="O produto nasce como rascunho. Depois de criar, cadastre SKUs, estoque e imagens e publique."
        />
      </div>
      <div className="max-w-2xl">
        <Card>
          <NewProduct categories={categories} collections={collections} />
        </Card>
      </div>
    </>
  );
}
