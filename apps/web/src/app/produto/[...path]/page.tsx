import type { Metadata } from "next";
import { notFound, permanentRedirect } from "next/navigation";
import { Breadcrumb } from "@/components/catalog/Breadcrumb";
import { ProductPurchase } from "@/components/product/ProductPurchase";
import { getProduct, type ProductDetail } from "@/lib/catalog";
import { siteUrl } from "@/lib/env";

/** PDP: SSR a cada requisição, com disponibilidade de estoque sempre atual (ADR 0006). */
export const dynamic = "force-dynamic";

async function load(segments: string[]) {
  const path = segments.map(decodeURIComponent).join("/");
  const product = await getProduct(segments[segments.length - 1]);
  if (!product) return null;
  return { product, requestedPath: path };
}

function productJsonLd(product: ProductDetail) {
  const skus = product.colors.flatMap((c) => c.sizes.map((s) => ({ ...s, color: c.name })));
  const images = product.colors.flatMap((c) => c.images.map((i) => `${siteUrl}${i.url}`));
  const prices = skus.map((s) => s.salePrice ?? s.price);

  return {
    "@context": "https://schema.org",
    "@type": "Product",
    name: product.name,
    description: product.description,
    brand: { "@type": "Brand", name: product.brand },
    image: images,
    sku: skus[0]?.code,
    offers: {
      "@type": "AggregateOffer",
      priceCurrency: "BRL",
      lowPrice: Math.min(...prices),
      highPrice: Math.max(...prices),
      offerCount: skus.length,
      availability: skus.some((s) => s.available > 0)
        ? "https://schema.org/InStock"
        : "https://schema.org/OutOfStock",
      url: `${siteUrl}/${product.path}`,
    },
  };
}

export async function generateMetadata(props: PageProps<"/produto/[...path]">): Promise<Metadata> {
  const data = await load((await props.params).path);
  if (!data) return {};
  const { product } = data;
  const image = product.colors[0]?.images[0];

  return {
    title: product.name,
    description: product.description,
    alternates: { canonical: `/${product.path}` },
    openGraph: {
      type: "website",
      title: `${product.name} | Kamus`,
      description: product.description,
      url: `/${product.path}`,
      images: image ? [{ url: image.url, width: 600, height: 800, alt: image.alt }] : undefined,
    },
  };
}

export default async function ProductPage(props: PageProps<"/produto/[...path]">) {
  const data = await load((await props.params).path);
  if (!data) notFound();

  const { product, requestedPath } = data;
  // Produto mudou de categoria (ou URL incompleta): manda para a URL canônica.
  if (requestedPath !== product.path) permanentRedirect(`/${product.path}`);

  return (
    <div className="mx-auto max-w-6xl px-4 py-8">
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{
          __html: JSON.stringify(productJsonLd(product)).replace(/</g, "\\u003c"),
        }}
      />
      <Breadcrumb items={product.breadcrumb} current={product.name} />
      <ProductPurchase product={product} />
      <section className="mt-12 max-w-2xl border-t border-line pt-8">
        <h2 className="mb-3 font-display text-2xl">Sobre a peça</h2>
        <p className="leading-relaxed text-muted">{product.description}</p>
        {product.collection && (
          <p className="mt-4 text-sm">
            Coleção:{" "}
            <a href={`/colecoes/${product.collection.slug}`} className="text-accent underline">
              {product.collection.name}
            </a>
          </p>
        )}
      </section>
    </div>
  );
}
