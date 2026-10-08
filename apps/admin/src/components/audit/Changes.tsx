import { changeValue, isVolatileValue } from "@/lib/audit";
import type { AuditChange } from "@/lib/schemas";

/** Valor antigo: riscado e discreto. */
function Before({ change }: { change: AuditChange }) {
  return (
    <span
      className="text-muted line-through decoration-stone-400"
      data-volatile={isVolatileValue(change.field, change.before) || undefined}
    >
      {changeValue(change.before)}
    </span>
  );
}

/** Valor novo: em destaque. */
function After({ change }: { change: AuditChange }) {
  return (
    <span
      className="font-medium"
      data-volatile={isVolatileValue(change.field, change.after) || undefined}
    >
      {changeValue(change.after)}
    </span>
  );
}

function Arrow() {
  return (
    <>
      <span aria-hidden className="text-muted">
        →
      </span>
      <span className="sr-only">para</span>
    </>
  );
}

/** "Campo: antes → depois" em linha (coluna Alterações da Atividade). */
export function ChangeInline({ change }: { change: AuditChange }) {
  return (
    <span className="flex flex-wrap items-baseline gap-x-1.5 text-[13px]">
      <span className="text-muted">{change.field}:</span>
      <Before change={change} />
      <Arrow />
      <After change={change} />
    </span>
  );
}

/** Caixa com uma linha por campo (linha do tempo do produto). */
export function ChangeGrid({ changes }: { changes: AuditChange[] }) {
  return (
    <dl className="space-y-1 rounded-md border border-line bg-paper px-3 py-2 text-[13px]">
      {changes.map((change, i) => (
        <div key={i} className="grid gap-x-3 sm:grid-cols-[150px_minmax(0,1fr)]">
          <dt className="text-muted">{change.field}</dt>
          <dd className="flex flex-wrap items-baseline gap-x-1.5">
            <Before change={change} />
            <Arrow />
            <After change={change} />
          </dd>
        </div>
      ))}
    </dl>
  );
}

/** Tabela Campo | Antes | Depois (detalhe expandido da Atividade). */
export function ChangeTable({ changes }: { changes: AuditChange[] }) {
  return (
    <table className="w-full rounded-md border border-line bg-surface text-[13px]">
      <thead className="text-left text-xs text-muted">
        <tr className="border-b border-line">
          <th className="w-36 px-3 py-2 font-normal">Campo</th>
          <th className="px-3 py-2 font-normal">Antes</th>
          <th className="px-3 py-2 font-normal">Depois</th>
        </tr>
      </thead>
      <tbody className="divide-y divide-line">
        {changes.map((change, i) => (
          <tr key={i}>
            <td className="px-3 py-2 text-muted">{change.field}</td>
            <td
              className="px-3 py-2 break-words text-muted"
              data-volatile={isVolatileValue(change.field, change.before) || undefined}
            >
              {changeValue(change.before)}
            </td>
            <td
              className="px-3 py-2 font-medium break-words"
              data-volatile={isVolatileValue(change.field, change.after) || undefined}
            >
              {changeValue(change.after)}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
