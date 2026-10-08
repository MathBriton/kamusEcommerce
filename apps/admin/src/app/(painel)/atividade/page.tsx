import type { Metadata } from "next";
import Link from "next/link";
import { z } from "zod";
import { ActivityTable } from "@/components/audit/ActivityTable";
import { Icon } from "@/components/icons";
import {
  Alert,
  Button,
  Input,
  PageHeader,
  Pagination,
  Select,
  buttonClassName,
} from "@/components/ui";
import { AUDIT_ACTIONS, AUDIT_MODULES, isAuditAction } from "@/lib/audit";
import { adminPage, auditActorOptionSchema, auditEntrySchema } from "@/lib/schemas";
import { apiLoadWithSession } from "@/lib/server-api";

export const metadata: Metadata = { title: "Atividade" };

const PAGE_SIZE = 20;
const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;

/** Grupos do filtro "Usuário", na ordem em que mais se procura. */
const ACTOR_GROUPS = [
  { kind: "Admin", label: "Equipe" },
  { kind: "System", label: "Sistema" },
  { kind: "Customer", label: "Clientes" },
] as const;

export default async function ActivityPage(props: PageProps<"/atividade">) {
  const params = await props.searchParams;
  const text = (key: string) => {
    const value = params[key];
    return typeof value === "string" ? value.trim() : "";
  };

  // Filtros na URL em português (como o resto do painel); só valores válidos seguem para a API.
  const actor = text("usuario");
  const moduleFilter = AUDIT_MODULES.some((m) => m.value === text("modulo")) ? text("modulo") : "";
  const action = isAuditAction(text("acao")) ? text("acao") : "";
  const from = ISO_DATE.test(text("de")) ? text("de") : "";
  const to = ISO_DATE.test(text("ate")) ? text("ate") : "";
  const page = Math.max(1, Number(params.pagina) || 1);

  const filters = new URLSearchParams();
  if (actor) filters.set("actor", actor);
  if (moduleFilter) filters.set("module", moduleFilter);
  if (action) filters.set("action", action);
  if (from) filters.set("from", from);
  if (to) filters.set("to", to);
  const hasFilters = filters.toString() !== "";

  const query = new URLSearchParams(filters);
  query.set("page", String(page));
  query.set("pageSize", String(PAGE_SIZE));

  const [entries, actors] = await Promise.all([
    apiLoadWithSession(`/api/admin/audit/entries?${query}`, adminPage(auditEntrySchema)),
    apiLoadWithSession("/api/admin/audit/actors", z.array(auditActorOptionSchema)),
  ]);
  const actorOptions = actors.ok ? actors.data : [];

  const href = (nextPage: number) => {
    const next = new URLSearchParams({
      ...(actor && { usuario: actor }),
      ...(moduleFilter && { modulo: moduleFilter }),
      ...(action && { acao: action }),
      ...(from && { de: from }),
      ...(to && { ate: to }),
    });
    if (nextPage > 1) next.set("pagina", String(nextPage));
    const qs = next.toString();
    return `/atividade${qs ? `?${qs}` : ""}`;
  };

  return (
    <>
      <PageHeader
        title="Atividade"
        description="Quem fez o quê no backoffice, quando e qual era o valor anterior."
        actions={
          // Download direto pelo BFF (mesma origem, cookie de sessão vai junto).
          <a
            href={`/api/admin/audit/entries.csv${hasFilters ? `?${filters}` : ""}`}
            download
            className={buttonClassName("secondary")}
          >
            <Icon name="download" />
            Exportar CSV
          </a>
        }
      />

      <form
        action="/atividade"
        className="mb-4 flex flex-wrap items-end gap-3"
        aria-label="Filtros da atividade"
      >
        <Select
          id="usuario"
          label="Usuário"
          defaultValue={actor}
          className="min-w-44 flex-[1_1_180px]"
        >
          <option value="">Todos</option>
          {ACTOR_GROUPS.map((group) => {
            const options = actorOptions.filter((a) => a.kind === group.kind);
            if (options.length === 0) return null;
            return (
              <optgroup key={group.kind} label={group.label}>
                {options.map((a) => (
                  <option key={a.key} value={a.key}>
                    {a.name}
                  </option>
                ))}
              </optgroup>
            );
          })}
          {actor && !actorOptions.some((a) => a.key === actor) && (
            <option value={actor}>{actor}</option>
          )}
        </Select>
        <Select
          id="modulo"
          label="Módulo"
          defaultValue={moduleFilter}
          className="min-w-36 flex-[1_1_160px]"
        >
          <option value="">Todos</option>
          {AUDIT_MODULES.map((m) => (
            <option key={m.value} value={m.value}>
              {m.label}
            </option>
          ))}
        </Select>
        <Select id="acao" label="Ação" defaultValue={action} className="min-w-36 flex-[1_1_160px]">
          <option value="">Todas</option>
          {AUDIT_ACTIONS.map((a) => (
            <option key={a.value} value={a.value}>
              {a.label}
            </option>
          ))}
        </Select>
        <Input
          id="de"
          label="De"
          type="date"
          defaultValue={from}
          max={to || undefined}
          className="min-w-36 flex-[1_1_140px]"
        />
        <Input
          id="ate"
          label="Até"
          type="date"
          defaultValue={to}
          min={from || undefined}
          className="min-w-36 flex-[1_1_140px]"
        />
        <div className="flex items-center gap-1">
          <Button type="submit" variant="secondary">
            Filtrar
          </Button>
          <Link href="/atividade" className="px-2.5 text-sm text-muted hover:text-ink">
            Limpar filtros
          </Link>
        </div>
      </form>

      {!actors.ok && (
        <div className="mb-4">
          <Alert>Não foi possível carregar a lista de usuários: {actors.message}</Alert>
        </div>
      )}

      {entries.ok ? (
        <>
          <ActivityTable entries={entries.data.items} />
          <Pagination
            page={entries.data.page}
            pageSize={entries.data.pageSize}
            total={entries.data.total}
            href={href}
          />
        </>
      ) : (
        <Alert>{entries.message}</Alert>
      )}

      <p className="mt-4 flex items-center gap-2 text-xs text-muted">
        <Icon name="lock" className="size-3.5" />
        Somente inclusão: nenhum registro pode ser editado ou apagado. Retenção de 2 anos.
      </p>
    </>
  );
}
