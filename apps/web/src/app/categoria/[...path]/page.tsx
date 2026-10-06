import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { Breadcrumb } from "@/components/catalog/Breadcrumb";
import { CatalogListingPage, PAGE_SIZE } from "@/components/catalog/CatalogListingPage";
import {
  findCategory,
  flattenCategories,
  getCategoryTree,
  getFacets,
  listProducts,
} from "@/lib/catalog";

/** PLP: ISR. A página é gerada uma vez e revalidada em segundo plano (ADR 0006). */
export const revalidate = 300;
export const dynamicParams = true;

export async function generateStaticParams() {
  try {
    const tree = await getCategoryTree();
    return flattenCategories(tree).map((c) => ({ path: c.path.split("/") }));
  } catch {
    // API fora do ar durante o build (ex.: build da imagem Docker): gera sob demanda.
    return [];
  }
}

async function load(segments: string[]) {
  const path = segments.map(decodeURIComponent).join("/");
  const tree = await getCategoryTree();
  const category = findCategory(tree, path);
  if (!category) return null;

  const breadcrumb = flattenCategories(tree)
    .filter((c) => path.startsWith(`${c.path}/`))
    .sort((a, b) => a.path.length - b.path.length);

  return { category, breadcrumb, tree };
}

export async function generateMetadata(
  props: PageProps<"/categoria/[...path]">,
): Promise<Metadata> {
  const { path } = await props.params;
  const data = await load(path);
  if (!data) return {};

  const title = [data.category.name, ...data.breadcrumb.map((b) => b.name).reverse()].join(" ");
  return {
    title,
    description: `Compre ${title.toLowerCase()} na Kamus: peças com design brasileiro, frete para todo o país.`,
    alternates: { canonical: `/${data.category.path}` },
    openGraph: { title: `${title} | Kamus`, url: `/${data.category.path}` },
  };
}

export default async function CategoryPage(props: PageProps<"/categoria/[...path]">) {
  const { path } = await props.params;
  const data = await load(path);
  if (!data) notFound();

  const scope = { category: data.category.path };
  const [initial, facets] = await Promise.all([
    listProducts({ ...scope, limit: PAGE_SIZE }),
    getFacets(scope),
  ]);

  return (
    <CatalogListingPage title={data.category.name} scope={scope} initial={initial} facets={facets}>
      <Breadcrumb items={data.breadcrumb} current={data.category.name} />
      {data.category.children.length > 0 && (
        <nav aria-label="Subcategorias" className="mb-6 flex flex-wrap gap-2">
          {data.category.children.map((child) => (
            <a
              key={child.id}
              href={`/${child.path}`}
              className="border border-line bg-surface px-3 py-1.5 text-sm hover:border-ink"
            >
              {child.name}
            </a>
          ))}
        </nav>
      )}
    </CatalogListingPage>
  );
}
