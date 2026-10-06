import Link from "next/link";
import { ProductGrid } from "@/components/catalog/ProductGrid";
import { getCategoryTreeSafe, listProducts, type ProductListPage } from "@/lib/catalog";

/**
 * Home: renderizada por requisição, mas com os dados do catálogo em cache (revalidate 300s).
 * Assim um build sem API (ex.: imagem Docker) não congela uma home vazia.
 */
export const dynamic = "force-dynamic";

const TILE_COLORS = ["bg-accent", "bg-ink", "bg-accent-dark"];

async function safeList(filters: Parameters<typeof listProducts>[0]): Promise<ProductListPage> {
  try {
    return await listProducts(filters);
  } catch {
    return { items: [], nextCursor: null };
  }
}

export default async function Home() {
  const [tree, news, outlet] = await Promise.all([
    getCategoryTreeSafe(),
    safeList({ sort: "newest", limit: 8 }),
    safeList({ collection: "outlet", sort: "price_asc", limit: 4 }),
  ]);

  return (
    <>
      <section className="bg-sand">
        <div className="mx-auto grid max-w-6xl gap-8 px-4 py-20 md:grid-cols-2 md:items-center">
          <div>
            <p className="mb-3 text-sm tracking-widest text-accent uppercase">Nova coleção</p>
            <h1 className="mb-4 font-display text-5xl leading-tight">
              Moda com sotaque brasileiro.
            </h1>
            <p className="mb-8 text-lg text-muted">
              Linho, tons terrosos e cortes amplos para dias quentes.
            </p>
            <Link
              href="/colecoes/nova-colecao"
              className="inline-block bg-ink px-8 py-3 text-sm tracking-widest text-paper uppercase hover:bg-accent"
            >
              Ver coleção
            </Link>
          </div>
          <ul className="grid grid-cols-3 gap-3">
            {tree.map((department, index) => (
              <li key={department.id}>
                <Link
                  href={`/${department.path}`}
                  className={`flex aspect-[3/4] items-end p-4 font-display text-lg text-paper transition-opacity hover:opacity-90 sm:text-xl ${TILE_COLORS[index % TILE_COLORS.length]}`}
                >
                  {department.name}
                </Link>
              </li>
            ))}
          </ul>
        </div>
      </section>

      <section className="mx-auto max-w-6xl px-4 py-14">
        <div className="mb-6 flex items-baseline justify-between">
          <h2 className="font-display text-3xl">Novidades</h2>
          <Link
            href="/colecoes/nova-colecao"
            className="text-sm text-accent underline-offset-4 hover:underline"
          >
            Ver tudo
          </Link>
        </div>
        <ProductGrid products={news.items} />
      </section>

      {outlet.items.length > 0 && (
        <section className="mx-auto max-w-6xl px-4 pb-14">
          <div className="mb-6 flex items-baseline justify-between">
            <h2 className="font-display text-3xl">Outlet</h2>
            <Link
              href="/colecoes/outlet"
              className="text-sm text-accent underline-offset-4 hover:underline"
            >
              Ver tudo
            </Link>
          </div>
          <ProductGrid products={outlet.items} priorityCount={0} />
        </section>
      )}
    </>
  );
}
