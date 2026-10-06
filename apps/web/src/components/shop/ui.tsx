import type { InputHTMLAttributes, SelectHTMLAttributes } from "react";

/** Campos de formulário com rótulo e mensagem de erro acessíveis. */
export function Field({
  label,
  error,
  id,
  className,
  ...props
}: InputHTMLAttributes<HTMLInputElement> & { label: string; error?: string; id: string }) {
  return (
    <div className={className}>
      <label htmlFor={id} className="mb-1 block text-sm">
        {label}
      </label>
      <input
        id={id}
        name={id}
        aria-invalid={Boolean(error)}
        aria-describedby={error ? `${id}-error` : undefined}
        className={`w-full border bg-surface px-3 py-2.5 ${error ? "border-sale" : "border-line"} focus:border-ink focus:outline-none`}
        {...props}
      />
      {error && (
        <p id={`${id}-error`} className="mt-1 text-xs text-sale">
          {error}
        </p>
      )}
    </div>
  );
}

export function SelectField({
  label,
  error,
  id,
  className,
  children,
  ...props
}: SelectHTMLAttributes<HTMLSelectElement> & { label: string; error?: string; id: string }) {
  return (
    <div className={className}>
      <label htmlFor={id} className="mb-1 block text-sm">
        {label}
      </label>
      <select
        id={id}
        name={id}
        aria-invalid={Boolean(error)}
        className={`w-full border bg-surface px-3 py-2.5 ${error ? "border-sale" : "border-line"}`}
        {...props}
      >
        {children}
      </select>
      {error && <p className="mt-1 text-xs text-sale">{error}</p>}
    </div>
  );
}

export function PrimaryButton(props: React.ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <button
      {...props}
      className={`w-full bg-ink py-4 text-sm tracking-widest text-paper uppercase transition-colors hover:bg-accent disabled:opacity-50 disabled:hover:bg-ink ${props.className ?? ""}`}
    />
  );
}

export function Alert({ children }: { children: React.ReactNode }) {
  return (
    <p role="alert" className="border border-sale/30 bg-sale/5 px-4 py-3 text-sm text-sale">
      {children}
    </p>
  );
}

/** Converte os erros do Zod em um mapa campo → primeira mensagem. */
export function fieldErrors(issues: { path: PropertyKey[]; message: string }[]) {
  const errors: Record<string, string> = {};
  for (const issue of issues) {
    const key = String(issue.path[0]);
    errors[key] ??= issue.message;
  }
  return errors;
}
