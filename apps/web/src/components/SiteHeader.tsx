import Link from "next/link";
import { getCategoryTreeSafe } from "@/lib/catalog";

/** Cabeçalho com o menu de categorias (árvore vinda da API, em cache ISR). */
export async function SiteHeader() {
  const tree = await getCategoryTreeSafe();

  return (
    <header className="border-b border-line bg-surface">
      <div className="mx-auto flex h-16 max-w-6xl items-center gap-4 px-4 sm:gap-8">
        <Link href="/" className="font-display text-xl tracking-[0.2em] sm:text-2xl">
          KAMUS
        </Link>
        <nav aria-label="Categorias" className="min-w-0 flex-1">
          <ul className="flex gap-4 overflow-x-auto text-xs tracking-wide uppercase sm:gap-6 sm:overflow-visible sm:text-sm">
            {tree.map((department) => (
              <li key={department.id} className="group relative">
                <Link
                  href={`/${department.path}`}
                  className="block py-5 whitespace-nowrap hover:text-accent"
                >
                  {department.name}
                </Link>
                {department.children.length > 0 && (
                  <div className="invisible absolute top-full left-0 z-20 hidden min-w-64 border border-line bg-surface p-5 normal-case opacity-0 shadow-lg transition group-focus-within:visible group-focus-within:opacity-100 group-hover:visible group-hover:opacity-100 sm:block">
                    <ul className="space-y-2">
                      {department.children.map((category) => (
                        <li key={category.id}>
                          <Link
                            href={`/${category.path}`}
                            className="font-medium hover:text-accent"
                          >
                            {category.name}
                          </Link>
                          {category.children.length > 0 && (
                            <ul className="mt-1 ml-3 space-y-1 text-muted">
                              {category.children.map((leaf) => (
                                <li key={leaf.id}>
                                  <Link href={`/${leaf.path}`} className="hover:text-accent">
                                    {leaf.name}
                                  </Link>
                                </li>
                              ))}
                            </ul>
                          )}
                        </li>
                      ))}
                    </ul>
                  </div>
                )}
              </li>
            ))}
          </ul>
        </nav>
      </div>
    </header>
  );
}
