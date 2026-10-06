import { Suspense } from "react";
import type { Facets, ProductListPage } from "@/lib/catalog";
import { ProductGrid } from "./ProductGrid";
import { ProductListing } from "./ProductListing";

export const PAGE_SIZE = 24;

type Props = {
  title: string;
  description?: string;
  scope: { category?: string; collection?: string };
  initial: ProductListPage;
  facets: Facets | null;
  children?: React.ReactNode;
};

/** Esqueleto de PLP compartilhado por categorias e coleções. */
export function CatalogListingPage({
  title,
  description,
  scope,
  initial,
  facets,
  children,
}: Props) {
  return (
    <div className="mx-auto max-w-6xl px-4 py-8">
      {children}
      <header className="mb-8 flex flex-wrap items-end justify-between gap-2">
        <div>
          <h1 className="font-display text-4xl">{title}</h1>
          {description && <p className="mt-2 max-w-2xl text-muted">{description}</p>}
        </div>
        {facets && <p className="text-sm text-muted">{facets.total} produtos</p>}
      </header>
      <h2 className="sr-only">Produtos</h2>
      {/* useSearchParams exige Suspense; o fallback é a primeira página já renderizada (SEO). */}
      <Suspense fallback={<ProductGrid products={initial.items} />}>
        <ProductListing scope={scope} initial={initial} facets={facets} pageSize={PAGE_SIZE} />
      </Suspense>
    </div>
  );
}
