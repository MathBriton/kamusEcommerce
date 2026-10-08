import type { AuditActorKind, AuditEntry } from "./schemas";

/**
 * Rótulos e tons da auditoria. A API devolve códigos canônicos (módulo, tipo de entidade, ação);
 * a tradução para o usuário fica aqui, num lugar só.
 */

/** Busca só em chaves próprias: "constructor" ou "toString" vindos da API não viram rótulo. */
const own = <T>(record: Record<string, T>, key: string): T | undefined =>
  Object.hasOwn(record, key) ? record[key] : undefined;

const MODULE_LABEL: Record<string, string> = {
  catalog: "Catálogo",
  inventory: "Estoque",
  orders: "Pedidos",
};

/** Opções do filtro "Módulo" (valor = código da API). */
export const AUDIT_MODULES = Object.entries(MODULE_LABEL).map(([value, label]) => ({
  value,
  label,
}));

export const moduleLabel = (module: string) => own(MODULE_LABEL, module) ?? module;

/** Verbo genérico por código de ação: filtro "Ação" e reserva para tipos sem rótulo próprio. */
const ACTION_VERB: Record<string, string> = {
  created: "Criou",
  updated: "Editou",
  published: "Publicou",
  unpublished: "Despublicou",
  deleted: "Excluiu",
  restored: "Restaurou",
  purged: "Excluiu de vez",
  stock_adjusted: "Ajustou estoque",
  stock_sold: "Baixa por venda",
  payment_started: "Iniciou pagamento",
  paid: "Confirmou pagamento",
  payment_failed: "Pagamento recusado",
  shipped: "Despachou",
  delivered: "Confirmou entrega",
  cancelled: "Cancelou",
};

/** Opções do filtro "Ação" (valor = código da API). */
export const AUDIT_ACTIONS = Object.entries(ACTION_VERB).map(([value, label]) => ({
  value,
  label,
}));

export const isAuditAction = (value: string) => Object.hasOwn(ACTION_VERB, value);

/** Rótulo por (tipo de entidade, ação): "Excluiu variação", "Despachou"… */
const ACTION_LABEL: Record<string, Record<string, string>> = {
  Product: {
    created: "Criou produto",
    updated: "Editou produto",
    published: "Publicou",
    unpublished: "Despublicou",
    deleted: "Excluiu",
    restored: "Restaurou",
    purged: "Excluiu de vez",
  },
  Sku: {
    created: "Adicionou variação",
    updated: "Editou variação",
    deleted: "Excluiu variação",
    restored: "Restaurou variação",
    purged: "Excluiu variação de vez",
  },
  ProductImage: {
    created: "Enviou imagem",
    deleted: "Excluiu imagem",
    restored: "Restaurou imagem",
    purged: "Excluiu imagem de vez",
  },
  StockLevel: {
    created: "Definiu estoque",
    stock_adjusted: "Ajustou estoque",
    stock_sold: "Baixa por venda",
    purged: "Removeu estoque",
  },
  Order: {
    created: "Criou pedido",
    payment_started: "Iniciou pagamento",
    paid: "Confirmou pagamento",
    payment_failed: "Pagamento recusado",
    shipped: "Despachou",
    delivered: "Confirmou entrega",
    cancelled: "Cancelou",
  },
};

export function actionLabel(entityType: string, action: string): string {
  const byEntity = own(ACTION_LABEL, entityType);
  return (byEntity && own(byEntity, action)) ?? own(ACTION_VERB, action) ?? action;
}

export type AuditTone = "green" | "red" | "amber" | "blue" | "sand";

const GREEN = new Set(["created", "published", "paid", "delivered", "restored"]);
const RED = new Set(["deleted", "purged", "cancelled", "payment_failed"]);

/** Verde para o que cria ou conclui, vermelho para o que remove, âmbar para estoque. */
export function actionTone(action: string): AuditTone {
  if (GREEN.has(action)) return "green";
  if (RED.has(action)) return "red";
  if (action.startsWith("stock_")) return "amber";
  if (action === "shipped") return "blue";
  return "sand";
}

/** Selo da ação (mesma paleta do StatusBadge dos pedidos). */
export const AUDIT_TONE_STYLE: Record<AuditTone, string> = {
  green: "bg-emerald-100 text-emerald-900",
  red: "bg-red-100 text-red-900",
  amber: "bg-amber-100 text-amber-900",
  blue: "bg-sky-100 text-sky-900",
  sand: "bg-sand text-ink",
};

/** Ponto da linha do tempo. */
export const AUDIT_TONE_DOT: Record<AuditTone, string> = {
  green: "bg-emerald-600",
  red: "bg-red-700",
  amber: "bg-amber-700",
  blue: "bg-sky-600",
  sand: "bg-stone-400",
};

/** Selo curto do tipo de ator (histórico do pedido e do produto). */
export const ACTOR_KIND_LABEL: Record<AuditActorKind, string> = {
  Admin: "Admin",
  Customer: "Cliente",
  System: "Sistema",
};

/** Linha de apoio sob o nome do ator na Atividade. */
export const ACTOR_KIND_DESCRIPTION: Record<AuditActorKind, string> = {
  Admin: "Administrador",
  Customer: "Cliente",
  System: "Sistema",
};

export const ACTOR_KIND_STYLE: Record<AuditActorKind, string> = {
  Admin: "bg-ink text-paper",
  Customer: "bg-sand text-ink",
  System: "bg-stone-200 text-stone-700",
};

const ENTITY_LABEL: Record<string, string> = {
  Product: "Produto",
  Sku: "Variação",
  ProductImage: "Imagem",
  StockLevel: "Estoque",
  Order: "Pedido",
};

export const entityTypeLabel = (entityType: string) => own(ENTITY_LABEL, entityType) ?? entityType;

/** Nome do item afetado; sem rótulo, cai no tipo + início do id. */
export function subjectName(entry: Pick<AuditEntry, "subjectType" | "subjectId" | "subjectLabel">) {
  return (
    entry.subjectLabel ?? `${entityTypeLabel(entry.subjectType)} ${entry.subjectId.slice(0, 8)}`
  );
}

/**
 * Para onde o detalhe da Atividade leva. Exclusão aponta para a lixeira; expurgo não tem destino
 * (o item não existe mais).
 */
export function subjectLink(
  entry: Pick<AuditEntry, "subjectType" | "subjectId" | "action">,
): { href: string; label: string } | null {
  if (entry.subjectType === "Product") {
    if (entry.action === "purged") return null;
    if (entry.action === "deleted") return { href: "/lixeira", label: "Ver na lixeira" };
    return {
      href: `/produtos/${encodeURIComponent(entry.subjectId)}?aba=historico`,
      label: "Abrir o produto",
    };
  }
  if (entry.subjectType === "Order") {
    return { href: `/pedidos/${encodeURIComponent(entry.subjectId)}`, label: "Abrir o pedido" };
  }
  return null;
}

/** Valor de um campo no antes → depois: ausente vira travessão. */
export const changeValue = (value: string | null) => (value === null || value === "" ? "—" : value);

/** Campos cujo valor muda a cada execução (o código do SKU leva parte do id do produto). */
const VOLATILE_FIELDS = new Set(["Código"]);
export const isVolatileField = (field: string) => VOLATILE_FIELDS.has(field);

/** Só valores que existem mudam entre execuções: o "—" de um campo vazio fica visível na captura. */
export const isVolatileValue = (field: string, value: string | null) =>
  value !== null && isVolatileField(field);
