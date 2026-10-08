"use client";

import { useEffect, useId, useRef, type ReactNode } from "react";
import { Icon } from "./icons";
import { Alert, Button } from "./ui";

type Props = {
  open: boolean;
  title: string;
  /** Explicação do que acontece; vira a descrição acessível do diálogo. */
  children: ReactNode;
  confirmLabel: string;
  busyLabel?: string;
  busy?: boolean;
  error?: string | null;
  onConfirm: () => void;
  onClose: () => void;
};

/**
 * Confirmação de exclusão em `<dialog>` modal nativo: o navegador prende o foco dentro dele,
 * deixa o resto da página inerte e fecha com Esc. O foco começa em "Cancelar" (a opção segura).
 */
export function ConfirmDialog({
  open,
  title,
  children,
  confirmLabel,
  busyLabel = "Excluindo…",
  busy = false,
  error,
  onConfirm,
  onClose,
}: Props) {
  const ref = useRef<HTMLDialogElement>(null);
  const titleId = useId();
  const descriptionId = useId();

  useEffect(() => {
    const dialog = ref.current;
    if (!dialog) return;
    if (open && !dialog.open) {
      dialog.showModal();
      dialog.querySelector<HTMLElement>("[data-autofocus]")?.focus();
    } else if (!open && dialog.open) {
      dialog.close();
    }
  }, [open]);

  return (
    <dialog
      ref={ref}
      aria-labelledby={titleId}
      aria-describedby={descriptionId}
      onClose={onClose}
      onCancel={(event) => {
        // Esc no meio da exclusão não fecha: a resposta ainda vai chegar.
        if (busy) event.preventDefault();
      }}
      className="m-auto w-[480px] max-w-[calc(100%-2rem)] rounded-[10px] border border-line bg-surface p-0 text-left text-sm font-normal text-ink shadow-[0_24px_48px_rgba(28,25,23,0.18)] backdrop:bg-ink/45"
    >
      <div className="px-6 pt-6 pb-5">
        <div className="mb-4 flex size-10 items-center justify-center rounded-full bg-red-100 text-sale">
          <Icon name="trash" className="size-5" />
        </div>
        <h2 id={titleId} className="text-lg font-semibold tracking-tight">
          {title}
        </h2>
        <div id={descriptionId} className="mt-2 leading-relaxed text-muted">
          {children}
        </div>
        {error && (
          <div className="mt-4">
            <Alert>{error}</Alert>
          </div>
        )}
      </div>
      <div className="flex flex-wrap justify-end gap-2 rounded-b-[10px] border-t border-line bg-paper px-6 py-4">
        <Button variant="secondary" data-autofocus onClick={onClose} disabled={busy}>
          Cancelar
        </Button>
        <Button variant="destructive" onClick={onConfirm} disabled={busy}>
          {busy ? busyLabel : confirmLabel}
        </Button>
      </div>
    </dialog>
  );
}

/** Lista "o que acontece" do diálogo, com marcas de verificação. */
export function ConfirmList({ items }: { items: ReactNode[] }) {
  return (
    <ul className="mt-4 space-y-2 rounded-md border border-line bg-paper px-3.5 py-3 text-[13px] text-ink">
      {items.map((item, i) => (
        <li key={i} className="flex gap-2">
          <span className="mt-px text-emerald-800">
            <Icon name="check" strokeWidth={2} />
          </span>
          <span>{item}</span>
        </li>
      ))}
    </ul>
  );
}
