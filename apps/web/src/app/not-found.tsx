import Link from "next/link";

export default function NotFound() {
  return (
    <div className="mx-auto max-w-6xl px-4 py-24 text-center">
      <p className="mb-2 text-sm tracking-widest text-accent uppercase">Erro 404</p>
      <h1 className="mb-4 font-display text-4xl">Página não encontrada</h1>
      <p className="mb-8 text-muted">O endereço pode ter mudado ou o produto saiu de linha.</p>
      <Link href="/" className="bg-ink px-8 py-3 text-sm tracking-widest text-paper uppercase">
        Voltar para a loja
      </Link>
    </div>
  );
}
