import type { ListingFilters } from "@/lib/catalog";

/** Filtros da PLP que vivem na URL (?tamanho=M&cor=Azul&preco=100-200&ordem=price_asc). */
export type ListingState = {
  sizes: string[];
  colors: string[];
  price?: { min?: number; max?: number };
  sort: string;
};

export const SORT_OPTIONS = [
  { value: "newest", label: "Lançamentos" },
  { value: "price_asc", label: "Menor preço" },
  { value: "price_desc", label: "Maior preço" },
  { value: "name", label: "Nome (A–Z)" },
] as const;

export const PRICE_RANGES = [
  { value: "0-100", label: "Até R$ 100" },
  { value: "100-200", label: "R$ 100 a R$ 200" },
  { value: "200-300", label: "R$ 200 a R$ 300" },
  { value: "300-", label: "Acima de R$ 300" },
] as const;

export function parseListingState(params: URLSearchParams): ListingState {
  const sort = params.get("ordem") ?? "newest";
  const priceParam = params.get("preco");
  let price: ListingState["price"];
  if (priceParam && /^\d*-\d*$/.test(priceParam)) {
    const [min, max] = priceParam.split("-");
    price = { min: min ? Number(min) : undefined, max: max ? Number(max) : undefined };
  }

  return {
    sizes: params.getAll("tamanho"),
    colors: params.getAll("cor"),
    price,
    sort: SORT_OPTIONS.some((o) => o.value === sort) ? sort : "newest",
  };
}

export function serializeListingState(state: ListingState): string {
  const params = new URLSearchParams();
  state.sizes.forEach((s) => params.append("tamanho", s));
  state.colors.forEach((c) => params.append("cor", c));
  if (state.price) params.set("preco", `${state.price.min ?? ""}-${state.price.max ?? ""}`);
  if (state.sort !== "newest") params.set("ordem", state.sort);
  return params.toString();
}

export const isDefaultState = (state: ListingState) =>
  state.sizes.length === 0 && state.colors.length === 0 && !state.price && state.sort === "newest";

export function toApiFilters(state: ListingState): ListingFilters {
  return {
    size: state.sizes,
    color: state.colors,
    minPrice: state.price?.min,
    maxPrice: state.price?.max,
    sort: state.sort,
  };
}
