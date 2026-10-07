"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { Alert, Button, Input } from "@/components/ui";
import { readProblem, type OrderStatus } from "@/lib/schemas";

type Props = { orderId: string; status: OrderStatus; trackingCode: string | null };

/** Ações disponíveis conforme o estado do pedido (a API valida as transições). */
export function OrderActions({ orderId, status, trackingCode }: Props) {
  const router = useRouter();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [cancelling, setCancelling] = useState(false);

  async function post(path: string, body?: unknown) {
    setBusy(true);
    setError(null);
    try {
      const response = await fetch(`/api/admin/orders/${orderId}/${path}`, {
        method: "POST",
        headers: body ? { "content-type": "application/json" } : undefined,
        body: body ? JSON.stringify(body) : undefined,
      });
      if (!response.ok) {
        setError(await readProblem(response));
        return;
      }
      setCancelling(false);
      router.refresh();
    } finally {
      setBusy(false);
    }
  }

  const canCancel = status === "Created" || status === "AwaitingPayment";

  return (
    <div className="space-y-4 text-sm">
      {error && <Alert>{error}</Alert>}

      {status === "Paid" && (
        <form
          onSubmit={(e) => {
            e.preventDefault();
            post("ship", { trackingCode: new FormData(e.currentTarget).get("trackingCode") });
          }}
          className="space-y-3"
        >
          <Input
            id="trackingCode"
            label="Código de rastreio"
            placeholder="BR123456789BR"
            required
          />
          <Button type="submit" disabled={busy} className="w-full">
            Despachar pedido
          </Button>
        </form>
      )}

      {status === "Shipped" && (
        <>
          <p>
            Rastreio: <span className="font-mono font-medium">{trackingCode ?? "—"}</span>
          </p>
          <Button disabled={busy} onClick={() => post("deliver")} className="w-full">
            Confirmar entrega
          </Button>
        </>
      )}

      {status === "AwaitingPayment" && (
        <p className="text-muted">
          Aguardando o retorno do gateway de pagamento. O estoque está reservado.
        </p>
      )}
      {status === "Delivered" && (
        <p className="text-muted">
          Pedido concluído{trackingCode ? ` · rastreio ${trackingCode}` : ""}.
        </p>
      )}
      {(status === "Cancelled" || status === "PaymentFailed") && (
        <p className="text-muted">Pedido encerrado, sem ações disponíveis.</p>
      )}

      {canCancel &&
        (cancelling ? (
          <form
            onSubmit={(e) => {
              e.preventDefault();
              post("cancel", { reason: new FormData(e.currentTarget).get("reason") });
            }}
            className="space-y-3 border-t border-line pt-4"
          >
            <Input id="reason" label="Motivo do cancelamento" required />
            <div className="flex gap-2">
              <Button type="submit" variant="danger" disabled={busy}>
                Cancelar pedido
              </Button>
              <Button variant="ghost" onClick={() => setCancelling(false)}>
                Voltar
              </Button>
            </div>
          </form>
        ) : (
          <Button variant="danger" onClick={() => setCancelling(true)} className="w-full">
            Cancelar pedido…
          </Button>
        ))}
    </div>
  );
}
