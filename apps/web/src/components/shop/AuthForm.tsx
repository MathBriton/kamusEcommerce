"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useState } from "react";
import { loginFormSchema, notifyAuthChanged, readProblem, registerFormSchema } from "@/lib/shop";
import { Alert, Field, PrimaryButton, fieldErrors } from "./ui";

type Mode = "login" | "register";

/** Só aceita redirecionar para caminhos internos (evita open redirect). */
function safeNext(next: string | null) {
  return next && next.startsWith("/") && !next.startsWith("//") ? next : "/conta";
}

export function AuthForm({ mode }: { mode: Mode }) {
  const router = useRouter();
  const params = useSearchParams();
  const next = safeNext(params.get("next"));
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [message, setMessage] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function onSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = Object.fromEntries(new FormData(event.currentTarget));
    const parsed = (mode === "login" ? loginFormSchema : registerFormSchema).safeParse(data);
    if (!parsed.success) {
      setErrors(fieldErrors(parsed.error.issues));
      return;
    }

    setErrors({});
    setMessage(null);
    setSubmitting(true);
    try {
      const response = await fetch(`/api/identity/${mode}`, {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(parsed.data),
      });
      if (!response.ok) {
        setMessage(await readProblem(response));
        return;
      }
      notifyAuthChanged();
      router.replace(next);
      router.refresh();
    } finally {
      setSubmitting(false);
    }
  }

  const isLogin = mode === "login";
  const otherHref = `${isLogin ? "/cadastro" : "/entrar"}${next !== "/conta" ? `?next=${encodeURIComponent(next)}` : ""}`;

  return (
    <form onSubmit={onSubmit} noValidate className="space-y-4">
      {message && <Alert>{message}</Alert>}
      {!isLogin && (
        <Field id="fullName" label="Nome completo" autoComplete="name" error={errors.fullName} />
      )}
      <Field id="email" label="E-mail" type="email" autoComplete="email" error={errors.email} />
      <Field
        id="password"
        label="Senha"
        type="password"
        autoComplete={isLogin ? "current-password" : "new-password"}
        error={errors.password}
      />
      <PrimaryButton type="submit" disabled={submitting}>
        {submitting ? "Aguarde…" : isLogin ? "Entrar" : "Criar conta"}
      </PrimaryButton>
      <p className="text-center text-sm text-muted">
        {isLogin ? "Ainda não tem conta? " : "Já tem conta? "}
        <Link href={otherHref} className="text-accent underline underline-offset-4">
          {isLogin ? "Cadastre-se" : "Entrar"}
        </Link>
      </p>
    </form>
  );
}
