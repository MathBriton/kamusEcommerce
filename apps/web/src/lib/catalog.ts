import { z } from "zod";
import { apiGet } from "./api";

export type CategoryNode = {
  id: string;
  name: string;
  slug: string;
  path: string;
  children: CategoryNode[];
};

export const categoryNodeSchema: z.ZodType<CategoryNode> = z.lazy(() =>
  z.object({
    id: z.string(),
    name: z.string(),
    slug: z.string(),
    path: z.string(),
    children: z.array(categoryNodeSchema),
  }),
);

const imageSchema = z.object({ url: z.string(), alt: z.string() });
const colorSchema = z.object({ name: z.string(), hex: z.string() });

export const collectionSchema = z.object({
  name: z.string(),
  slug: z.string(),
  description: z.string(),
});

export const productCardSchema = z.object({
  id: z.string(),
  slug: z.string(),
  name: z.string(),
  brand: z.string(),
  path: z.string(),
  price: z.number(),
  salePrice: z.number().nullable(),
  image: imageSchema.nullable(),
  colors: z.array(colorSchema),
});

export const productListPageSchema = z.object({
  items: z.array(productCardSchema),
  nextCursor: z.string().nullable(),
});

export const facetsSchema = z.object({
  total: z.number(),
  sizes: z.array(z.string()),
  colors: z.array(colorSchema),
  minPrice: z.number().nullable(),
  maxPrice: z.number().nullable(),
});

export const productDetailSchema = z.object({
  id: z.string(),
  slug: z.string(),
  name: z.string(),
  brand: z.string(),
  description: z.string(),
  path: z.string(),
  breadcrumb: z.array(z.object({ name: z.string(), path: z.string() })),
  collection: collectionSchema.nullable(),
  price: z.number(),
  salePrice: z.number().nullable(),
  colors: z.array(
    colorSchema.extend({
      images: z.array(imageSchema),
      sizes: z.array(
        z.object({
          skuId: z.string(),
          code: z.string(),
          size: z.string(),
          price: z.number(),
          salePrice: z.number().nullable(),
          available: z.number(),
        }),
      ),
    }),
  ),
  updatedAt: z.string(),
});

export const sitemapSchema = z.array(z.object({ path: z.string(), updatedAt: z.string() }));

export type ProductCard = z.infer<typeof productCardSchema>;
export type ProductListPage = z.infer<typeof productListPageSchema>;
export type Facets = z.infer<typeof facetsSchema>;
export type ProductDetail = z.infer<typeof productDetailSchema>;
export type Collection = z.infer<typeof collectionSchema>;

/** Tempo de revalidação (ISR) dos dados de catálogo, em segundos. */
export const CATALOG_REVALIDATE = 300;

const cached = { next: { revalidate: CATALOG_REVALIDATE, tags: ["catalog"] } };

export type ListingFilters = {
  category?: string;
  collection?: string;
  size?: string[];
  color?: string[];
  minPrice?: number;
  maxPrice?: number;
  sort?: string;
  cursor?: string;
  limit?: number;
};

export function listingQuery(filters: ListingFilters): string {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(filters)) {
    if (value === undefined || value === "") continue;
    for (const v of Array.isArray(value) ? value : [value]) params.append(key, String(v));
  }
  return params.toString();
}

export async function getCategoryTree(): Promise<CategoryNode[]> {
  return (await apiGet("/api/catalog/categories", z.array(categoryNodeSchema), cached)) ?? [];
}

/** Árvore de categorias sem lançar erro: o menu não pode derrubar a página. */
export async function getCategoryTreeSafe(): Promise<CategoryNode[]> {
  try {
    return await getCategoryTree();
  } catch {
    return [];
  }
}

export function flattenCategories(nodes: CategoryNode[]): CategoryNode[] {
  return nodes.flatMap((n) => [n, ...flattenCategories(n.children)]);
}

export function findCategory(nodes: CategoryNode[], path: string): CategoryNode | undefined {
  return flattenCategories(nodes).find((c) => c.path === path);
}

export async function getCollections() {
  return (await apiGet("/api/catalog/collections", z.array(collectionSchema), cached)) ?? [];
}

export async function listProducts(filters: ListingFilters) {
  const page = await apiGet(
    `/api/catalog/products?${listingQuery(filters)}`,
    productListPageSchema,
    cached,
  );
  return page ?? { items: [], nextCursor: null };
}

export async function getFacets(scope: { category?: string; collection?: string }) {
  return apiGet(`/api/catalog/facets?${listingQuery(scope)}`, facetsSchema, cached);
}

/** PDP: sempre fresco (SSR), porque a disponibilidade por tamanho muda a todo momento. */
export async function getProduct(slug: string) {
  return apiGet(`/api/catalog/products/${encodeURIComponent(slug)}`, productDetailSchema, {
    cache: "no-store",
  });
}

export async function getSitemapEntries() {
  return (
    (await apiGet("/api/catalog/sitemap", sitemapSchema, { next: { revalidate: 3600 } })) ?? []
  );
}
