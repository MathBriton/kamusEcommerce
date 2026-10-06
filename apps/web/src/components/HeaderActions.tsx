"use client";

import Link from "next/link";
import { useEffect, useState } from "react";

/**
 * Conta e sacola no cabeçalho. Carregadas no cliente para que o layout continue estático
 * (ler cookies no servidor tornaria todas as páginas dinâmicas, inclusive as PLPs em ISR).
 */
export function HeaderActions() {
  const [count, setCount] = useState<number | null>(null);
  const [signedIn, setSignedIn] = useState<boolean | null>(null);

  useEffect(() => {
    const loadCart = () =>
      fetch("/api/cart/count")
        .then((r) => (r.ok ? r.json() : { count: 0 }))
        .then((data: { count: number }) => setCount(data.count))
        .catch(() => setCount(null));
    const loadAuth = () =>
      fetch("/api/identity/session")
        .then((r) => r.json())
        .then((data: { authenticated: boolean }) => setSignedIn(data.authenticated))
        .catch(() => setSignedIn(false));

    const onCart = () => void loadCart();
    const onAuth = () => {
      void loadAuth();
      void loadCart();
    };

    onAuth();
    window.addEventListener("kamus:cart-changed", onCart);
    window.addEventListener("kamus:auth-changed", onAuth);
    return () => {
      window.removeEventListener("kamus:cart-changed", onCart);
      window.removeEventListener("kamus:auth-changed", onAuth);
    };
  }, []);

  return (
    <div className="flex items-center gap-4 text-sm">
      <Link
        href={signedIn ? "/conta" : "/entrar"}
        className="hidden whitespace-nowrap hover:text-accent sm:block"
      >
        {signedIn ? "Minha conta" : "Entrar"}
      </Link>
      <Link
        href="/carrinho"
        className="flex items-center gap-1.5 whitespace-nowrap hover:text-accent"
      >
        <svg
          aria-hidden
          viewBox="0 0 24 24"
          className="size-5"
          fill="none"
          stroke="currentColor"
          strokeWidth="1.6"
        >
          <path d="M6 8h12l-1 12H7L6 8Z" />
          <path d="M9 8V6a3 3 0 0 1 6 0v2" />
        </svg>
        <span>Sacola</span>
        {count !== null && count > 0 && (
          <span
            className="min-w-5 rounded-full bg-accent px-1.5 text-center text-xs text-white"
            aria-label={`${count} itens`}
          >
            {count}
          </span>
        )}
      </Link>
    </div>
  );
}
