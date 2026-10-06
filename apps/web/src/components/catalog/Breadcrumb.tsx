import Link from "next/link";

type Item = { name: string; path: string };

export function Breadcrumb({ items, current }: { items: Item[]; current?: string }) {
  return (
    <nav aria-label="Navegação estrutural" className="mb-6 text-sm text-muted">
      <ol className="flex flex-wrap items-center gap-1">
        <li>
          <Link href="/" className="hover:text-ink">
            Início
          </Link>
        </li>
        {items.map((item) => (
          <li key={item.path} className="flex items-center gap-1">
            <span aria-hidden>›</span>
            <Link href={`/${item.path}`} className="hover:text-ink">
              {item.name}
            </Link>
          </li>
        ))}
        {current && (
          <li className="flex items-center gap-1 text-ink" aria-current="page">
            <span aria-hidden>›</span>
            {current}
          </li>
        )}
      </ol>
    </nav>
  );
}
