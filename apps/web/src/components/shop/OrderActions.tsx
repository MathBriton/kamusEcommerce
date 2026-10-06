"use client";

import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { readProblem, type OrderStatus } from "@/lib/shop";
import { Alert } from "./ui";

type Props = {
  orderId: string;
  status: OrderStatus;
  canCancel: boolean;
  simulateFulfillment: boolean;
};

/** Ações do pedido e atualização automática enquanto o pagamento não é confirmado. */
export function OrderActions({ orderId, status, canCancel, simulateFulfillment }: Props) {
  const router = useRouter();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // O FakePay responde por webhook alguns segundos depois: recarrega a página até mudar o status.
  useEffect(() => {
    if (status !== "AwaitingPayment") return;
    const timer = setInterval(() => router.refresh(), 2000);
    return () => clearInterval(timer);
  }, [status, router]);

  async function post(path: string) {
    setBusy(true);
    setError(null);
    try {
      const response = await fetch(`/api/orders/${orderId}/${path}`, { method: "POST" });
      if (!response.ok) setError(await readProblem(response));
      router.refresh();
    } finally {
      setBusy(false);
    }
  }

  const canAdvance = simulateFulfillment && (status === "Paid" || status === "Shipped");
  if (!canCancel && !canAdvance && !error) return null;

  return (
    <div className="mb-8 flex flex-wrap items-center gap-4 text-sm">
      {error && <Alert>{error}</Alert>}
      {canCancel && (
        <button
          type="button"
          disabled={busy}
          onClick={() => post("cancel")}
          className="border border-line px-4 py-2 hover:border-sale hover:text-sale"
        >
          Cancelar pedido
        </button>
      )}
      {canAdvance && (
        <button
          type="button"
          disabled={busy}
          onClick={() => post("advance-fulfillment")}
          className="border border-line px-4 py-2 hover:border-ink"
        >
          {status === "Paid" ? "Simular envio" : "Simular entrega"} (demo)
        </button>
      )}
    </div>
  );
}
