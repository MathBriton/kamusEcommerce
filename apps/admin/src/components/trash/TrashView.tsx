"use client";

import Image from "next/image";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import { Icon } from "@/components/icons";
import { Alert, Button } from "@/components/ui";
import { formatDate, formatTime } from "@/lib/format";
import { readProblem, restoreResultSchema, type TrashItem, type TrashPage } from "@/lib/schemas";
import {
  TRASH_RETENTION_DAYS,
  TRASH_TABS,
  TRASH_TYPE_LABEL,
  purgeLabel,
  type TrashTab,
} from "@/lib/trash";

type Props = {
  items: TrashItem[];
  counts: TrashPage["counts"];
  tab: TrashTab;
  /** Instante da renderização no servidor: "em N dias" igual no servidor e no navegador. */
  now: number;
};

type Message = { ok: boolean; text: string } | null;

const tabHref = (value: string) => (value ? `/lixeira?tipo=${value}` : "/lixeira");

/** Lixeira: abas por tipo, restaurar e excluir de vez (com confirmação). */
export function TrashView({ items, counts, tab, now }: Props) {
  const router = useRouter();
  const [message, setMessage] = useState<Message>(null);
  const [busy, setBusy] = useState(false);
  const [toPurge, setToPurge] = useState<TrashItem | null>(null);
  const [purgeError, setPurgeError] = useState<string | null>(null);

  const total = counts.product + counts.sku + counts.image;
  const tabCount = (type: TrashTab["type"]) => (type === "all" ? total : counts[type]);

  async function restore(item: TrashItem) {
    setBusy(true);
    setMessage(null);
    try {
      const response = await fetch(`/api/admin/catalog/trash/${item.type}/${item.id}/restore`, {
        method: "POST",
      });
      if (!response.ok) {
        setMessage({ ok: false, text: await readProblem(response) });
        return;
      }
      const result = restoreResultSchema.safeParse(await response.json().catch(() => null));
      setMessage({
        ok: true,
        text: result.success
          ? result.data.message
          : `“${item.name}” foi ${item.type === "image" ? "restaurada" : "restaurado"}.`,
      });
      router.refresh();
    } catch {
      setMessage({ ok: false, text: "Não foi possível falar com o servidor. Tente novamente." });
    } finally {
      setBusy(false);
    }
  }

  async function purge(item: TrashItem) {
    setBusy(true);
    setPurgeError(null);
    try {
      const response = await fetch(`/api/admin/catalog/trash/${item.type}/${item.id}`, {
        method: "DELETE",
      });
      if (!response.ok) {
        setPurgeError(await readProblem(response));
        return;
      }
      setToPurge(null);
      setMessage({
        ok: true,
        text: `“${item.name}” foi ${item.type === "image" ? "excluída" : "excluído"} de vez.`,
      });
      router.refresh();
    } catch {
      setPurgeError("Não foi possível falar com o servidor. Tente novamente.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <>
      {message && (
        <div className="mb-4">
          <Alert tone={message.ok ? "success" : "error"}>{message.text}</Alert>
        </div>
      )}

      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <nav
          aria-label="Tipo de item"
          className="flex flex-wrap gap-1 rounded-md border border-line bg-surface p-1 text-sm"
        >
          {TRASH_TABS.map((t) => {
            const active = t.value === tab.value;
            return (
              <Link
                key={t.value}
                href={tabHref(t.value)}
                aria-current={active ? "true" : undefined}
                className={`rounded px-3 py-1 ${active ? "bg-ink text-paper" : "text-muted hover:text-ink"}`}
              >
                {t.label} <span className="tabular opacity-75">{tabCount(t.type)}</span>
              </Link>
            );
          })}
        </nav>
        <p className="flex items-center gap-2 text-xs text-muted">
          <Icon name="history" className="size-3.5" />
          Expurgo automático: itens com mais de {TRASH_RETENTION_DAYS} dias são apagados de vez.
        </p>
      </div>

      <div className="overflow-x-auto rounded-lg border border-line bg-surface">
        <table className="w-full min-w-[960px] text-sm">
          <thead className="border-b border-line bg-paper text-left text-xs font-medium text-muted">
            <tr>
              <th className="px-4 py-2.5 font-medium">Item</th>
              <th className="w-24 px-4 py-2.5 font-medium">Tipo</th>
              <th className="w-40 px-4 py-2.5 font-medium">Excluído por</th>
              <th className="w-32 px-4 py-2.5 font-medium">Excluído em</th>
              <th className="w-28 px-4 py-2.5 font-medium">Expurgo</th>
              <th className="px-4 py-2.5 text-right font-medium">Ações</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-line">
            {items.length === 0 && (
              <tr>
                <td colSpan={6} className="px-4 py-12 text-center text-muted">
                  <p className="font-medium text-ink">Nada por aqui.</p>
                  <p className="mt-1">
                    Itens excluídos aparecem nesta lista por {TRASH_RETENTION_DAYS} dias.
                  </p>
                </td>
              </tr>
            )}
            {items.map((item) => (
              <TrashRow
                key={`${item.type}-${item.id}`}
                item={item}
                now={now}
                busy={busy}
                onRestore={() => restore(item)}
                onPurge={() => {
                  setPurgeError(null);
                  setToPurge(item);
                }}
              />
            ))}
          </tbody>
        </table>
      </div>

      <ConfirmDialog
        open={toPurge !== null}
        title={toPurge ? `Excluir “${toPurge.name}” de vez?` : "Excluir de vez?"}
        confirmLabel="Excluir de vez"
        busy={busy}
        error={purgeError}
        onConfirm={() => toPurge && purge(toPurge)}
        onClose={() => setToPurge(null)}
      >
        <p>{toPurge && purgeDescription(toPurge)}</p>
        <p className="mt-2">
          Não dá para desfazer. A exclusão definitiva fica registrada na Atividade.
        </p>
      </ConfirmDialog>
    </>
  );
}

function purgeDescription(item: TrashItem) {
  if (item.type === "product") {
    return "O produto é apagado do banco, com os SKUs, as imagens e o estoque. Os arquivos das fotos também são removidos.";
  }
  if (item.type === "sku") return "A variação é apagada do banco, junto com o estoque dela.";
  return "A imagem é apagada do banco e o arquivo da foto é removido.";
}

function TrashRow({
  item,
  now,
  busy,
  onRestore,
  onPurge,
}: {
  item: TrashItem;
  now: number;
  busy: boolean;
  onRestore: () => void;
  onPurge: () => void;
}) {
  const purge = purgeLabel(item.purgeAt, now);
  return (
    <tr>
      <td className="px-4 py-3">
        <span className="flex items-center gap-3">
          <span className="relative flex h-11 w-8 shrink-0 items-center justify-center overflow-hidden rounded bg-sand text-muted">
            {item.imageUrl ? (
              <Image
                src={item.imageUrl}
                alt=""
                fill
                unoptimized
                sizes="32px"
                className="object-cover"
              />
            ) : (
              <Icon name="photo" />
            )}
          </span>
          <span className="min-w-0">
            <span className="block">{item.name}</span>
            {item.detail && (
              <span
                className={`block text-xs text-muted ${item.type === "sku" ? "font-mono" : ""}`}
                // O código do SKU leva parte do id do produto: muda a cada execução.
                data-volatile={item.type === "sku" || undefined}
              >
                {item.detail}
              </span>
            )}
          </span>
        </span>
      </td>
      <td className="px-4 py-3 text-muted">{TRASH_TYPE_LABEL[item.type]}</td>
      <td className="px-4 py-3">{item.deletedBy.name ?? "—"}</td>
      <td className="tabular px-4 py-3" data-volatile>
        <span className="block">{formatDate(item.deletedAt)}</span>
        <span className="block text-xs text-muted">{formatTime(item.deletedAt)}</span>
      </td>
      <td
        className={`px-4 py-3 ${purge.soon ? "font-semibold text-amber-900" : "text-muted"}`}
        data-volatile
      >
        {purge.label}
      </td>
      <td className="px-4 py-3">
        <span className="flex justify-end gap-2">
          <Button
            variant="secondary"
            disabled={busy}
            onClick={onRestore}
            aria-label={`Restaurar ${item.name}`}
            className="whitespace-nowrap"
          >
            <Icon name="restore" className="size-[15px]" />
            Restaurar
          </Button>
          <Button
            variant="danger"
            disabled={busy}
            onClick={onPurge}
            aria-label={`Excluir de vez ${item.name}`}
            className="whitespace-nowrap"
          >
            Excluir de vez
          </Button>
        </span>
      </td>
    </tr>
  );
}
