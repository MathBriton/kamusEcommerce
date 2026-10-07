import type { Metadata } from "next";
import { LoginForm } from "./LoginForm";

export const metadata: Metadata = { title: "Entrar" };

export default async function LoginPage(props: PageProps<"/entrar">) {
  const { erro } = await props.searchParams;

  return (
    <div className="flex min-h-full items-center justify-center px-4">
      <div className="w-full max-w-sm rounded-lg border border-line bg-surface p-8">
        <p className="font-display text-2xl tracking-[0.2em]">KAMUS</p>
        <p className="mb-6 text-sm text-muted">Backoffice</p>
        <LoginForm denied={erro === "permissao"} />
      </div>
    </div>
  );
}
