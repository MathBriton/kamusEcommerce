"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { Alert, Button, Input } from "@/components/ui";
import { meSchema, readProblem } from "@/lib/schemas";

export function LoginForm({ denied }: { denied: boolean }) {
  const router = useRouter();
  const [message, setMessage] = useState<string | null>(
    denied ? "Sua conta não tem acesso ao backoffice." : null,
  );
  const [busy, setBusy] = useState(false);

  async function onSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    setBusy(true);
    setMessage(null);
    try {
      const response = await fetch("/api/identity/login", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ email: form.get("email"), password: form.get("password") }),
      });
      if (!response.ok) {
        setMessage(await readProblem(response));
        return;
      }
      const me = meSchema.parse(await response.json());
      if (!me.isAdmin) {
        await fetch("/api/identity/logout", { method: "POST" });
        setMessage("Sua conta não tem acesso ao backoffice.");
        return;
      }
      router.replace("/");
      router.refresh();
    } finally {
      setBusy(false);
    }
  }

  return (
    <form onSubmit={onSubmit} className="space-y-4">
      {message && <Alert>{message}</Alert>}
      <Input id="email" label="E-mail" type="email" autoComplete="username" required />
      <Input id="password" label="Senha" type="password" autoComplete="current-password" required />
      <Button type="submit" disabled={busy} className="w-full">
        {busy ? "Entrando…" : "Entrar"}
      </Button>
    </form>
  );
}
