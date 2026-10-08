"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { ConfirmDialog, ConfirmList } from "@/components/ConfirmDialog";
import { Icon } from "@/components/icons";
import { Button } from "@/components/ui";
import { readProblem } from "@/lib/schemas";
import { TRASH_RETENTION_DAYS, cascadeSummary } from "@/lib/trash";

type Props = {
  product: { id: string; name: string; skuCount: number; imageCount?: number };
  /** "button" no topo do editor; "icon" na linha da lista de produtos. */
  variant?: "button" | "icon";
  /** Para onde ir depois de excluir; padrão: lista de produtos com o aviso de sucesso. */
  redirectTo?: string;
};

/** Botão "Excluir" + confirmação: o produto (com SKUs e imagens) vai para a lixeira. */
export function DeleteProductButton({ product, variant = "button", redirectTo }: Props) {
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function openDialog() {
    setError(null);
    setOpen(true);
  }

  async function remove() {
    setBusy(true);
    setError(null);
    try {
      const response = await fetch(`/api/admin/catalog/products/${product.id}`, {
        method: "DELETE",
      });
      if (!response.ok) {
        setError(await readProblem(response));
        return;
      }
      setOpen(false);
      router.push(redirectTo ?? `/produtos?excluido=${encodeURIComponent(product.name)}`);
      // O layout (contador da lixeira) não é refeito numa navegação: refresh atualiza tudo.
      router.refresh();
    } catch {
      setError("Não foi possível falar com o servidor. Tente novamente.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <>
      {variant === "icon" ? (
        <button
          type="button"
          aria-label={`Excluir ${product.name}`}
          title="Excluir"
          onClick={openDialog}
          className="flex size-9 items-center justify-center rounded-md text-muted transition-colors hover:bg-sale/10 hover:text-sale"
        >
          <Icon name="trash" />
        </button>
      ) : (
        <Button variant="danger" onClick={openDialog}>
          <Icon name="trash" className="size-[15px]" />
          Excluir
        </Button>
      )}

      <ConfirmDialog
        open={open}
        title={`Excluir “${product.name}”?`}
        confirmLabel="Mover para a lixeira"
        busyLabel="Movendo…"
        busy={busy}
        error={error}
        onConfirm={remove}
        onClose={() => setOpen(false)}
      >
        <p>
          O produto sai da loja e do backoffice na hora e vai para a Lixeira. Você pode restaurá-lo
          em até {TRASH_RETENTION_DAYS} dias; depois disso ele é apagado de vez.
        </p>
        <ConfirmList
          items={[
            cascadeSummary(product.skuCount, product.imageCount),
            "Pedidos que já têm este produto não mudam.",
            "A exclusão fica registrada na Atividade com o seu nome.",
          ]}
        />
      </ConfirmDialog>
    </>
  );
}
