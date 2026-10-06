import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { Breadcrumb } from "@/components/catalog/Breadcrumb";
import { CatalogListingPage, PAGE_SIZE } from "@/components/catalog/CatalogListingPage";
import { getCollections, getFacets, listProducts } from "@/lib/catalog";

export const revalidate = 300;

export async function generateStaticParams() {
  try {
    return (await getCollections()).map((c) => ({ slug: c.slug }));
  } catch {
    return [];
  }
}

async function load(slug: string) {
  return (await getCollections()).find((c) => c.slug === slug) ?? null;
}

export async function generateMetadata(props: PageProps<"/colecoes/[slug]">): Promise<Metadata> {
  const collection = await load((await props.params).slug);
  if (!collection) return {};
  return {
    title: collection.name,
    description: collection.description,
    alternates: { canonical: `/colecoes/${collection.slug}` },
  };
}

export default async function CollectionPage(props: PageProps<"/colecoes/[slug]">) {
  const collection = await load((await props.params).slug);
  if (!collection) notFound();

  const scope = { collection: collection.slug };
  const [initial, facets] = await Promise.all([
    listProducts({ ...scope, limit: PAGE_SIZE }),
    getFacets(scope),
  ]);

  return (
    <CatalogListingPage
      title={collection.name}
      description={collection.description}
      scope={scope}
      initial={initial}
      facets={facets}
    >
      <Breadcrumb items={[]} current={collection.name} />
    </CatalogListingPage>
  );
}
