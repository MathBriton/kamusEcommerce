import { formatInt } from "@/lib/format";
import { ORDER_STATUS_LABEL, type OrderStatus } from "@/lib/schemas";

/**
 * Pedidos por status: barras horizontais de uma só cor (magnitude, não identidade).
 * As cores de status ficam reservadas para os selos; o rótulo de texto identifica cada barra.
 */
export function StatusBars({ data }: { data: { status: OrderStatus; count: number }[] }) {
  const max = Math.max(1, ...data.map((d) => d.count));

  return (
    <ul className="space-y-2.5">
      {data.map((d) => (
        <li
          key={d.status}
          className="group grid grid-cols-[150px_1fr] items-center gap-3 text-sm"
          title={`${ORDER_STATUS_LABEL[d.status]}: ${d.count}`}
        >
          <span className="truncate text-muted">{ORDER_STATUS_LABEL[d.status]}</span>
          <span className="flex min-w-0 items-center gap-2">
            {/* Trilho com largura própria: a barra é uma fração dele; o valor fica na ponta. */}
            <span className="flex-1" aria-hidden>
              <span
                className="block h-3 rounded-r bg-ink transition-opacity group-hover:opacity-80"
                style={{ width: `${(d.count / max) * 100}%`, minWidth: d.count > 0 ? 3 : 0 }}
              />
            </span>
            <span className="tabular w-8 text-right text-xs font-medium">{formatInt(d.count)}</span>
          </span>
        </li>
      ))}
    </ul>
  );
}
