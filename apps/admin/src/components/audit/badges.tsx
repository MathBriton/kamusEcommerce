import {
  ACTOR_KIND_LABEL,
  ACTOR_KIND_STYLE,
  AUDIT_TONE_STYLE,
  actionLabel,
  actionTone,
} from "@/lib/audit";
import type { AuditActorKind } from "@/lib/schemas";

/** Selo do tipo de ator: Admin, Cliente ou Sistema. */
export function ActorBadge({ kind }: { kind: AuditActorKind }) {
  return (
    <span
      className={`inline-block rounded px-1.5 py-px text-[11px] font-semibold whitespace-nowrap ${ACTOR_KIND_STYLE[kind]}`}
    >
      {ACTOR_KIND_LABEL[kind]}
    </span>
  );
}

/** Selo da ação, na cor do tom (verde cria, vermelho remove, âmbar estoque…). */
export function ActionBadge({ entityType, action }: { entityType: string; action: string }) {
  return (
    <span
      className={`inline-block rounded px-2 py-0.5 text-xs font-medium whitespace-nowrap ${AUDIT_TONE_STYLE[actionTone(action)]}`}
    >
      {actionLabel(entityType, action)}
    </span>
  );
}
