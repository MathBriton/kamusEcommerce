import Link from "next/link";
import type {
  ButtonHTMLAttributes,
  InputHTMLAttributes,
  ReactNode,
  SelectHTMLAttributes,
  TextareaHTMLAttributes,
} from "react";
import { ORDER_STATUS_LABEL, type OrderStatus } from "@/lib/schemas";

/** Componentes do backoffice: os mesmos tokens da loja, com densidade de ferramenta interna. */

type Variant = "primary" | "secondary" | "danger" | "ghost";

const VARIANT: Record<Variant, string> = {
  primary: "bg-ink text-paper hover:bg-accent disabled:hover:bg-ink",
  secondary: "border border-line bg-surface hover:border-ink",
  danger: "border border-sale/40 bg-surface text-sale hover:bg-sale hover:text-white",
  ghost: "text-muted hover:text-ink",
};

export function Button({
  variant = "primary",
  className = "",
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: Variant }) {
  return (
    <button
      type="button"
      {...props}
      className={`inline-flex h-9 items-center justify-center gap-2 rounded-md px-3.5 text-sm font-medium transition-colors disabled:cursor-not-allowed disabled:opacity-50 ${VARIANT[variant]} ${className}`}
    />
  );
}

export function LinkButton({
  href,
  children,
  variant = "primary",
}: {
  href: string;
  children: ReactNode;
  variant?: Variant;
}) {
  return (
    <Link
      href={href}
      className={`inline-flex h-9 items-center gap-2 rounded-md px-3.5 text-sm font-medium transition-colors ${VARIANT[variant]}`}
    >
      {children}
    </Link>
  );
}

const control =
  "h-9 w-full rounded-md border bg-surface px-3 text-sm focus:border-ink focus:outline-none";

type FieldProps = { label: string; error?: string; hint?: string; id: string };

export function Input({
  label,
  error,
  hint,
  id,
  className = "",
  ...props
}: InputHTMLAttributes<HTMLInputElement> & FieldProps) {
  return (
    <div className={className}>
      <label htmlFor={id} className="mb-1 block text-xs font-medium text-muted">
        {label}
      </label>
      <input
        id={id}
        name={id}
        aria-invalid={Boolean(error)}
        className={`${control} ${error ? "border-sale" : "border-line"}`}
        {...props}
      />
      {error ? (
        <p className="mt-1 text-xs text-sale">{error}</p>
      ) : hint ? (
        <p className="mt-1 text-xs text-muted">{hint}</p>
      ) : null}
    </div>
  );
}

export function Textarea({
  label,
  error,
  id,
  className = "",
  ...props
}: TextareaHTMLAttributes<HTMLTextAreaElement> & FieldProps) {
  return (
    <div className={className}>
      <label htmlFor={id} className="mb-1 block text-xs font-medium text-muted">
        {label}
      </label>
      <textarea
        id={id}
        name={id}
        aria-invalid={Boolean(error)}
        className={`w-full rounded-md border bg-surface px-3 py-2 text-sm focus:border-ink focus:outline-none ${error ? "border-sale" : "border-line"}`}
        {...props}
      />
      {error && <p className="mt-1 text-xs text-sale">{error}</p>}
    </div>
  );
}

export function Select({
  label,
  error,
  id,
  className = "",
  children,
  ...props
}: SelectHTMLAttributes<HTMLSelectElement> & FieldProps) {
  return (
    <div className={className}>
      <label htmlFor={id} className="mb-1 block text-xs font-medium text-muted">
        {label}
      </label>
      <select
        id={id}
        name={id}
        className={`${control} ${error ? "border-sale" : "border-line"}`}
        {...props}
      >
        {children}
      </select>
      {error && <p className="mt-1 text-xs text-sale">{error}</p>}
    </div>
  );
}

export function Card({
  title,
  actions,
  children,
  className = "",
}: {
  title?: string;
  actions?: ReactNode;
  children: ReactNode;
  className?: string;
}) {
  return (
    <section className={`rounded-lg border border-line bg-surface ${className}`}>
      {(title || actions) && (
        <header className="flex items-center justify-between gap-4 border-b border-line px-5 py-3">
          {title && <h2 className="text-sm font-semibold">{title}</h2>}
          {actions}
        </header>
      )}
      <div className="p-5">{children}</div>
    </section>
  );
}

export function PageHeader({
  title,
  description,
  actions,
}: {
  title: string;
  description?: ReactNode;
  actions?: ReactNode;
}) {
  return (
    <div className="mb-6 flex flex-wrap items-end justify-between gap-4">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">{title}</h1>
        {description && <p className="mt-1 text-sm text-muted">{description}</p>}
      </div>
      {actions && <div className="flex items-center gap-2">{actions}</div>}
    </div>
  );
}

const STATUS_STYLE: Record<OrderStatus, string> = {
  Created: "bg-sand text-ink",
  AwaitingPayment: "bg-amber-100 text-amber-900",
  Paid: "bg-emerald-100 text-emerald-900",
  Shipped: "bg-sky-100 text-sky-900",
  Delivered: "bg-emerald-200 text-emerald-950",
  Cancelled: "bg-stone-200 text-stone-700",
  PaymentFailed: "bg-red-100 text-red-900",
};

export function StatusBadge({ status }: { status: OrderStatus }) {
  return (
    <span
      className={`inline-block rounded px-2 py-0.5 text-xs font-medium whitespace-nowrap ${STATUS_STYLE[status]}`}
    >
      {ORDER_STATUS_LABEL[status]}
    </span>
  );
}

export function PublishedBadge({ active }: { active: boolean }) {
  return active ? (
    <span className="inline-flex items-center gap-1.5 text-xs font-medium text-emerald-800">
      <span className="size-1.5 rounded-full bg-emerald-600" aria-hidden />
      Publicado
    </span>
  ) : (
    <span className="inline-flex items-center gap-1.5 text-xs font-medium text-muted">
      <span className="size-1.5 rounded-full bg-stone-400" aria-hidden />
      Rascunho
    </span>
  );
}

export function Alert({
  children,
  tone = "error",
}: {
  children: ReactNode;
  tone?: "error" | "success";
}) {
  const style =
    tone === "error"
      ? "border-sale/30 bg-sale/5 text-sale"
      : "border-emerald-300 bg-emerald-50 text-emerald-900";
  return (
    <p
      role={tone === "error" ? "alert" : "status"}
      className={`rounded-md border px-3 py-2 text-sm ${style}`}
    >
      {children}
    </p>
  );
}

/** Tabela densa: cabeçalho discreto, linhas com hairline. */
export function Table({ head, children }: { head: ReactNode; children: ReactNode }) {
  return (
    <div className="overflow-x-auto rounded-lg border border-line bg-surface">
      <table className="w-full text-sm">
        <thead className="border-b border-line bg-paper text-left text-xs font-medium text-muted">
          {head}
        </thead>
        <tbody className="divide-y divide-line">{children}</tbody>
      </table>
    </div>
  );
}

export function Pagination({
  page,
  pageSize,
  total,
  href,
}: {
  page: number;
  pageSize: number;
  total: number;
  href: (page: number) => string;
}) {
  const pages = Math.max(1, Math.ceil(total / pageSize));
  const from = total === 0 ? 0 : (page - 1) * pageSize + 1;
  const to = Math.min(total, page * pageSize);
  return (
    <nav
      aria-label="Paginação"
      className="mt-4 flex items-center justify-between text-sm text-muted"
    >
      <span className="tabular">
        {from}–{to} de {total}
      </span>
      <div className="flex gap-2">
        {page > 1 ? (
          <LinkButton variant="secondary" href={href(page - 1)}>
            Anterior
          </LinkButton>
        ) : null}
        {page < pages ? (
          <LinkButton variant="secondary" href={href(page + 1)}>
            Próxima
          </LinkButton>
        ) : null}
      </div>
    </nav>
  );
}

export function fieldErrors(issues: { path: PropertyKey[]; message: string }[]) {
  const errors: Record<string, string> = {};
  for (const issue of issues) errors[String(issue.path[0])] ??= issue.message;
  return errors;
}
