"use client";

import Link from "next/link";
import { Fragment, useState } from "react";
import { Icon } from "@/components/icons";
import {
  ACTOR_KIND_DESCRIPTION,
  entityTypeLabel,
  moduleLabel,
  subjectLink,
  subjectName,
} from "@/lib/audit";
import { formatDate, formatTime } from "@/lib/format";
import type { AuditEntry } from "@/lib/schemas";
import { ActionBadge } from "./badges";
import { ChangeInline, ChangeTable } from "./Changes";

/** Quantas alterações a linha mostra antes de "+N"; o detalhe expandido mostra todas. */
const INLINE_CHANGES = 2;

/** Tabela da Atividade: uma linha por registro, com detalhe expansível (antes → depois, IP…). */
export function ActivityTable({ entries }: { entries: AuditEntry[] }) {
  const [expanded, setExpanded] = useState<ReadonlySet<string>>(new Set());

  const toggle = (id: string) =>
    setExpanded((current) => {
      const next = new Set(current);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });

  return (
    <div className="overflow-x-auto rounded-lg border border-line bg-surface">
      <table className="w-full min-w-[960px] text-sm">
        <thead className="border-b border-line bg-paper text-left text-xs font-medium text-muted">
          <tr>
            <th className="w-28 px-4 py-2.5 font-medium">Quando</th>
            <th className="w-48 px-4 py-2.5 font-medium">Quem</th>
            <th className="w-44 px-4 py-2.5 font-medium">Ação</th>
            <th className="px-4 py-2.5 font-medium">Item</th>
            <th className="px-4 py-2.5 font-medium">Alterações</th>
            <th className="w-12 px-2 py-2.5">
              <span className="sr-only">Detalhes</span>
            </th>
          </tr>
        </thead>
        <tbody className="divide-y divide-line">
          {entries.length === 0 && (
            <tr>
              <td colSpan={6} className="px-4 py-10 text-center text-muted">
                Nenhum registro com esses filtros.
              </td>
            </tr>
          )}
          {entries.map((entry) => {
            const open = expanded.has(entry.id);
            const detailId = `atividade-${entry.id}`;
            const extra = entry.changes.length - INLINE_CHANGES;
            return (
              <Fragment key={entry.id}>
                <tr
                  onClick={() => toggle(entry.id)}
                  className={`cursor-pointer align-top ${open ? "border-dashed bg-paper" : "hover:bg-paper"}`}
                >
                  <td className="tabular px-4 py-3" data-volatile>
                    <span className="block">{formatTime(entry.occurredAt)}</span>
                    <span className="block text-xs text-muted">{formatDate(entry.occurredAt)}</span>
                  </td>
                  <td className="px-4 py-3">
                    <span className="block">{entry.actor.name}</span>
                    <span className="block text-xs text-muted">
                      {ACTOR_KIND_DESCRIPTION[entry.actor.kind]}
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    <ActionBadge entityType={entry.entityType} action={entry.action} />
                  </td>
                  <td className="px-4 py-3">
                    <span
                      className="block"
                      data-volatile={entry.subjectLabel === null || undefined}
                    >
                      {subjectName(entry)}
                    </span>
                    <span className="block text-xs text-muted">
                      {moduleLabel(entry.module)} ·{" "}
                      {entry.detail ?? entityTypeLabel(entry.entityType)}
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    {entry.changes.length === 0 ? (
                      <span className="text-muted">—</span>
                    ) : (
                      <span className="flex flex-col gap-1">
                        {entry.changes.slice(0, INLINE_CHANGES).map((change, i) => (
                          <ChangeInline key={i} change={change} />
                        ))}
                        {extra > 0 && (
                          <span className="text-xs text-muted">
                            +{extra} {extra === 1 ? "alteração" : "alterações"}
                          </span>
                        )}
                      </span>
                    )}
                  </td>
                  <td className="px-2 py-2 text-right">
                    <button
                      type="button"
                      aria-expanded={open}
                      aria-controls={open ? detailId : undefined}
                      aria-label={`Detalhes: ${entry.actor.name}, ${subjectName(entry)}`}
                      onClick={(event) => {
                        event.stopPropagation();
                        toggle(entry.id);
                      }}
                      className="inline-flex size-8 items-center justify-center rounded-md text-muted hover:bg-sand hover:text-ink"
                    >
                      <Icon name="chevronDown" className={`size-4 ${open ? "rotate-180" : ""}`} />
                    </button>
                  </td>
                </tr>
                {open && (
                  <tr id={detailId} className="bg-paper">
                    <td colSpan={6} className="px-4 pt-4 pb-5">
                      <EntryDetail entry={entry} />
                    </td>
                  </tr>
                )}
              </Fragment>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

function EntryDetail({ entry }: { entry: AuditEntry }) {
  const link = subjectLink(entry);
  return (
    <div className="flex flex-wrap gap-6 lg:pl-28">
      <div className="min-w-0 flex-[2_1_420px]">
        <p className="mb-2 text-xs font-semibold text-muted">Antes → depois</p>
        {entry.changes.length === 0 ? (
          <p className="text-sm text-muted">Nenhum campo acompanhado mudou.</p>
        ) : (
          <ChangeTable changes={entry.changes} />
        )}
      </div>
      <dl className="grid flex-[1_1_260px] grid-cols-[96px_minmax(0,1fr)] content-start gap-x-3 gap-y-1.5 text-[13px]">
        <dt className="text-muted">E-mail</dt>
        <dd>{entry.actor.email ?? "—"}</dd>
        <dt className="text-muted">Entidade</dt>
        <dd className="font-mono text-xs break-all" data-volatile>
          {entry.entityType} {entry.entityId}
        </dd>
        <dt className="text-muted">IP</dt>
        <dd className="tabular" data-volatile>
          {entry.ipAddress ?? "—"}
        </dd>
        <dt className="text-muted">Requisição</dt>
        <dd className="font-mono text-xs break-all" data-volatile>
          {entry.correlationId ?? "—"}
        </dd>
        {link && (
          <>
            <dt className="sr-only">Item</dt>
            <dd className="col-start-2 mt-1">
              <Link href={link.href} className="text-accent underline-offset-4 hover:underline">
                {link.label} →
              </Link>
            </dd>
          </>
        )}
      </dl>
    </div>
  );
}
