"use client";

import { useEffect, useMemo, useState } from "react";
import { useSearchParams } from "next/navigation";
import {
  listingQuery,
  productListPageSchema,
  type Facets,
  type ProductCard,
  type ProductListPage,
} from "@/lib/catalog";
import { ListingFilters } from "./ListingFilters";
import { ProductGrid } from "./ProductGrid";
import {
  isDefaultState,
  parseListingState,
  serializeListingState,
  toApiFilters,
  type ListingState,
} from "./listing-state";

type Props = {
  scope: { category?: string; collection?: string };
  initial: ProductListPage;
  facets: Facets | null;
  pageSize: number;
};

type Loaded = { key: string; items: ProductCard[]; cursor: string | null };

async function fetchPage(query: string): Promise<ProductListPage> {
  const response = await fetch(`/api/catalog/products?${query}`);
  if (!response.ok) throw new Error(`Erro ${response.status}`);
  return productListPageSchema.parse(await response.json());
}

/**
 * Listagem interativa da PLP. A primeira página sem filtros vem pronta do servidor (ISR);
 * filtros, ordenação e "carregar mais" consultam a API pelo BFF e mantêm o estado na URL.
 */
export function ProductListing({ scope, initial, facets, pageSize }: Props) {
  const searchParams = useSearchParams();
  const state = useMemo(() => parseListingState(new URLSearchParams(searchParams)), [searchParams]);
  const stateKey = serializeListingState(state);
  const isDefault = isDefaultState(state);

  // Resultado já carregado para um conjunto de filtros (identificado por `key`).
  const [loaded, setLoaded] = useState<Loaded>({
    key: "",
    items: initial.items,
    cursor: initial.nextCursor,
  });
  const [loadingMore, setLoadingMore] = useState(false);
  const [error, setError] = useState<{ key: string; message: string } | null>(null);

  const isCurrent = loaded.key === stateKey;
  const view =
    isCurrent || !isDefault ? loaded : { items: initial.items, cursor: initial.nextCursor };
  const fetchingFilters = !isCurrent && !isDefault && error?.key !== stateKey;

  useEffect(() => {
    if (isCurrent || isDefault) return;

    let cancelled = false;
    const current = parseListingState(new URLSearchParams(stateKey));
    fetchPage(listingQuery({ ...scope, ...toApiFilters(current), limit: pageSize }))
      .then(
        (page) =>
          !cancelled && setLoaded({ key: stateKey, items: page.items, cursor: page.nextCursor }),
      )
      .catch(
        () =>
          !cancelled &&
          setError({ key: stateKey, message: "Não foi possível carregar os produtos." }),
      );

    return () => {
      cancelled = true;
    };
    // scope e pageSize são estáveis por página; os filtros são representados por stateKey
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [stateKey, isCurrent, isDefault]);

  function update(next: ListingState) {
    const query = serializeListingState(next);
    window.history.pushState(null, "", query ? `?${query}` : window.location.pathname);
  }

  async function loadMore() {
    if (!view.cursor) return;
    setLoadingMore(true);
    try {
      const page = await fetchPage(
        listingQuery({ ...scope, ...toApiFilters(state), limit: pageSize, cursor: view.cursor }),
      );
      setLoaded({ key: stateKey, items: [...view.items, ...page.items], cursor: page.nextCursor });
    } catch {
      setError({ key: stateKey, message: "Não foi possível carregar mais produtos." });
    } finally {
      setLoadingMore(false);
    }
  }

  const loading = fetchingFilters || loadingMore;
  const errorMessage = error?.key === stateKey ? error.message : null;

  return (
    <div className="gap-8 lg:grid lg:grid-cols-[220px_1fr]">
      <ListingFilters facets={facets} state={state} onChange={update} />
      <div aria-busy={loading}>
        {errorMessage && (
          <p role="alert" className="mb-4 text-sm text-sale">
            {errorMessage}
          </p>
        )}
        <div className={fetchingFilters ? "opacity-60 transition-opacity" : undefined}>
          <ProductGrid products={view.items} />
        </div>
        {view.cursor && !fetchingFilters && (
          <div className="mt-10 text-center">
            <button
              type="button"
              onClick={loadMore}
              disabled={loadingMore}
              className="border border-ink px-8 py-3 text-sm tracking-wide uppercase hover:bg-ink hover:text-paper disabled:opacity-50"
            >
              {loadingMore ? "Carregando…" : "Carregar mais"}
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
