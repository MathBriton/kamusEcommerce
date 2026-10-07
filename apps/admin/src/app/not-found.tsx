import Link from "next/link";

export default function NotFound() {
  return (
    <div className="flex min-h-full flex-col items-center justify-center gap-3 text-sm">
      <p className="text-2xl font-semibold">Não encontrado</p>
      <Link href="/" className="text-accent underline underline-offset-4">
        Voltar ao painel
      </Link>
    </div>
  );
}
