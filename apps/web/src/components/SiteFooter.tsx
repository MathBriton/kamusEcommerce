import Link from "next/link";

export function SiteFooter() {
  return (
    <footer className="mt-16 border-t border-line bg-sand/40">
      <div className="mx-auto flex max-w-6xl flex-wrap justify-between gap-4 px-4 py-8 text-sm text-muted">
        <p>Kamus é uma loja fictícia, criada para estudo de arquitetura e system design.</p>
        <nav aria-label="Rodapé" className="flex gap-4">
          <Link href="/colecoes/nova-colecao" className="hover:text-ink">
            Nova coleção
          </Link>
          <Link href="/colecoes/outlet" className="hover:text-ink">
            Outlet
          </Link>
          <Link href="/status" className="hover:text-ink">
            Status
          </Link>
        </nav>
      </div>
    </footer>
  );
}
