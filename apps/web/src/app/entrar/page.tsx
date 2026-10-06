import type { Metadata } from "next";
import { Suspense } from "react";
import { AuthForm } from "@/components/shop/AuthForm";

export const metadata: Metadata = { title: "Entrar", robots: { index: false } };

export default function Page() {
  return (
    <div className="mx-auto max-w-sm px-4 py-16">
      <h1 className="mb-8 text-center font-display text-4xl">Entrar</h1>
      <Suspense>
        <AuthForm mode="login" />
      </Suspense>
    </div>
  );
}
