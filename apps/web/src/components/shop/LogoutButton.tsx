"use client";

import { useRouter } from "next/navigation";
import { notifyAuthChanged } from "@/lib/shop";

export function LogoutButton() {
  const router = useRouter();

  async function logout() {
    await fetch("/api/identity/logout", { method: "POST" });
    notifyAuthChanged();
    router.replace("/");
    router.refresh();
  }

  return (
    <button
      type="button"
      onClick={logout}
      className="text-sm text-muted underline underline-offset-4 hover:text-ink"
    >
      Sair
    </button>
  );
}
