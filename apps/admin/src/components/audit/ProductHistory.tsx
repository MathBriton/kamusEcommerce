import Link from "next/link";
import { Alert, Card } from "@/components/ui";
import { AUDIT_TONE_DOT, actionLabel, actionTone } from "@/lib/audit";
import { formatDate, formatInt, formatTime } from "@/lib/format";
import type { AuditEntry } from "@/lib/schemas";
import type { Loaded } from "@/lib/server-api";
import { ActorBadge } from "./badges";
import { ChangeGrid } from "./Changes";

/** Aba Histórico do produto: linha do tempo da auditoria (produto, SKUs, imagens e estoque). */
export function ProductHistory({ result, limit }: { result: Loaded<AuditEntry[]>; limit: number }) {
  if (!result.ok) {
    return (
      <Card title="Histórico de alterações">
        <Alert>{result.message}</Alert>
      </Card>
    );
  }

  const entries = result.data;
  const created = entries.find((e) => e.entityType === "Product" && e.action === "created");
  const latest = entries[0];

  return (
    <div className="grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_300px]">
      <Card
        title="Histórico de alterações"
        actions={<span className="text-xs text-muted">Mais recentes primeiro</span>}
      >
        {entries.length === 0 ? (
          <p className="text-sm text-muted">
            Nenhuma alteração registrada ainda. Produtos cadastrados antes da auditoria ganham
            histórico a partir da próxima mudança.
          </p>
        ) : (
          <ol className="pt-1 pl-3 text-sm">
            {entries.map((entry) => (
              <TimelineItem key={entry.id} entry={entry} />
            ))}
          </ol>
        )}
        {entries.length >= limit && (
          <p className="mt-4 text-xs text-muted">
            Mostrando as {formatInt(limit)} alterações mais recentes. As anteriores estão na
            Atividade.
          </p>
        )}
      </Card>

      <div className="space-y-6">
        <Card title="Resumo">
          <dl className="grid grid-cols-[104px_minmax(0,1fr)] gap-x-3 gap-y-2.5 text-sm">
            <dt className="text-muted">Criado</dt>
            <dd>{created ? <When entry={created} /> : <span className="text-muted">—</span>}</dd>
            <dt className="text-muted">Última alteração</dt>
            <dd>{latest ? <When entry={latest} /> : <span className="text-muted">—</span>}</dd>
            <dt className="text-muted">Alterações</dt>
            <dd className="tabular">{formatInt(entries.length)}</dd>
          </dl>
          <Link
            href="/atividade?modulo=catalog"
            className="mt-4 inline-block text-sm text-accent underline-offset-4 hover:underline"
          >
            Ver na Atividade →
          </Link>
        </Card>
        <p className="rounded-lg border border-line bg-surface px-4 py-3 text-xs leading-relaxed text-muted">
          O histórico mostra só os campos que mudaram. Alterações de estoque aparecem aqui e na
          Atividade, mesmo vindo do módulo Estoque.
        </p>
      </div>
    </div>
  );
}

function When({ entry }: { entry: AuditEntry }) {
  return (
    <>
      <span className="tabular whitespace-nowrap" data-volatile>
        {formatDate(entry.occurredAt)} {formatTime(entry.occurredAt)}
      </span>
      <span className="block text-xs text-muted">{entry.actor.name}</span>
    </>
  );
}

function TimelineItem({ entry }: { entry: AuditEntry }) {
  const title = actionLabel(entry.entityType, entry.action);
  return (
    <li className="relative border-l border-line pb-6 pl-5 last:border-transparent last:pb-0">
      <span
        aria-hidden
        className={`absolute top-1 -left-[5px] size-[9px] rounded-full ring-3 ring-surface ${AUDIT_TONE_DOT[actionTone(entry.action)]}`}
      />
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <p className="font-medium">
          {title}
          {entry.detail && <span className="font-normal text-muted"> · {entry.detail}</span>}
        </p>
        <p className="tabular text-xs text-muted" data-volatile>
          {formatDate(entry.occurredAt)} · {formatTime(entry.occurredAt)}
        </p>
      </div>
      <p className="mt-1 mb-2 flex flex-wrap items-center gap-2 text-xs text-muted">
        <ActorBadge kind={entry.actor.kind} />
        <span>{entry.actor.name}</span>
      </p>
      {entry.changes.length > 0 && <ChangeGrid changes={entry.changes} />}
    </li>
  );
}
