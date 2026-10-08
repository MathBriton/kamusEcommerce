import { describe, expect, it } from "vitest";
import {
  ACTOR_KIND_LABEL,
  AUDIT_ACTIONS,
  AUDIT_MODULES,
  actionLabel,
  actionTone,
  changeValue,
  entityTypeLabel,
  isAuditAction,
  isVolatileField,
  isVolatileValue,
  moduleLabel,
  subjectLink,
  subjectName,
} from "./audit";

describe("rótulos da auditoria", () => {
  it("traduz os módulos e mantém o código dos desconhecidos", () => {
    expect(moduleLabel("catalog")).toBe("Catálogo");
    expect(moduleLabel("inventory")).toBe("Estoque");
    expect(moduleLabel("orders")).toBe("Pedidos");
    expect(moduleLabel("payments")).toBe("payments");
    expect(AUDIT_MODULES.map((m) => m.value)).toEqual(["catalog", "inventory", "orders"]);
  });

  it.each([
    ["Product", "created", "Criou produto"],
    ["Product", "updated", "Editou produto"],
    ["Product", "published", "Publicou"],
    ["Product", "unpublished", "Despublicou"],
    ["Product", "deleted", "Excluiu"],
    ["Product", "restored", "Restaurou"],
    ["Product", "purged", "Excluiu de vez"],
    ["Sku", "created", "Adicionou variação"],
    ["Sku", "updated", "Editou variação"],
    ["Sku", "deleted", "Excluiu variação"],
    ["Sku", "restored", "Restaurou variação"],
    ["Sku", "purged", "Excluiu variação de vez"],
    ["ProductImage", "created", "Enviou imagem"],
    ["ProductImage", "deleted", "Excluiu imagem"],
    ["ProductImage", "restored", "Restaurou imagem"],
    ["ProductImage", "purged", "Excluiu imagem de vez"],
    ["StockLevel", "created", "Definiu estoque"],
    ["StockLevel", "stock_adjusted", "Ajustou estoque"],
    ["StockLevel", "stock_sold", "Baixa por venda"],
    ["StockLevel", "purged", "Removeu estoque"],
    ["Order", "created", "Criou pedido"],
    ["Order", "payment_started", "Iniciou pagamento"],
    ["Order", "paid", "Confirmou pagamento"],
    ["Order", "payment_failed", "Pagamento recusado"],
    ["Order", "shipped", "Despachou"],
    ["Order", "delivered", "Confirmou entrega"],
    ["Order", "cancelled", "Cancelou"],
  ])("%s + %s → %s", (entityType, action, label) => {
    expect(actionLabel(entityType, action)).toBe(label);
  });

  it("sem rótulo próprio, cai no verbo genérico e depois no código", () => {
    expect(actionLabel("ProductImage", "updated")).toBe("Editou");
    expect(actionLabel("Collection", "created")).toBe("Criou");
    expect(actionLabel("Product", "archived")).toBe("archived");
  });

  it("não confunde chaves herdadas de objeto com rótulos", () => {
    expect(actionLabel("constructor", "toString")).toBe("toString");
    expect(moduleLabel("__proto__")).toBe("__proto__");
    expect(entityTypeLabel("hasOwnProperty")).toBe("hasOwnProperty");
    expect(isAuditAction("constructor")).toBe(false);
  });

  it("o filtro de ação aceita só códigos conhecidos", () => {
    expect(isAuditAction("stock_adjusted")).toBe(true);
    expect(isAuditAction("drop table")).toBe(false);
    expect(AUDIT_ACTIONS.find((a) => a.value === "purged")?.label).toBe("Excluiu de vez");
  });

  it.each([
    ["created", "green"],
    ["published", "green"],
    ["paid", "green"],
    ["delivered", "green"],
    ["restored", "green"],
    ["deleted", "red"],
    ["purged", "red"],
    ["cancelled", "red"],
    ["payment_failed", "red"],
    ["stock_adjusted", "amber"],
    ["stock_sold", "amber"],
    ["shipped", "blue"],
    ["updated", "sand"],
    ["unpublished", "sand"],
    ["payment_started", "sand"],
  ])("tom de %s é %s", (action, tone) => {
    expect(actionTone(action)).toBe(tone);
  });

  it("selo do ator", () => {
    expect(ACTOR_KIND_LABEL).toEqual({ Admin: "Admin", Customer: "Cliente", System: "Sistema" });
  });

  it("tipos de entidade", () => {
    expect(entityTypeLabel("Sku")).toBe("Variação");
    expect(entityTypeLabel("StockLevel")).toBe("Estoque");
  });
});

describe("item afetado", () => {
  const product = { subjectType: "Product", subjectId: "p-1", subjectLabel: "Boné Trucker" };

  it("usa o rótulo da API ou o tipo com o início do id", () => {
    expect(subjectName(product)).toBe("Boné Trucker");
    expect(
      subjectName({
        subjectType: "Order",
        subjectId: "0199c2a1-7f3e-7b21-9c4d-5e8f0a1b2c3d",
        subjectLabel: null,
      }),
    ).toBe("Pedido 0199c2a1");
  });

  it("produto abre na aba Histórico; pedido abre o pedido", () => {
    expect(subjectLink({ ...product, action: "updated" })).toEqual({
      href: "/produtos/p-1?aba=historico",
      label: "Abrir o produto",
    });
    expect(subjectLink({ subjectType: "Order", subjectId: "o 1", action: "shipped" })).toEqual({
      href: "/pedidos/o%201",
      label: "Abrir o pedido",
    });
  });

  it("exclusão leva à lixeira; expurgo e tipos desconhecidos não têm link", () => {
    expect(subjectLink({ ...product, action: "deleted" })).toEqual({
      href: "/lixeira",
      label: "Ver na lixeira",
    });
    expect(subjectLink({ ...product, action: "purged" })).toBeNull();
    expect(subjectLink({ subjectType: "Sku", subjectId: "s-1", action: "updated" })).toBeNull();
  });
});

describe("valores do antes → depois", () => {
  it("ausente vira travessão", () => {
    expect(changeValue(null)).toBe("—");
    expect(changeValue("")).toBe("—");
    expect(changeValue("R$ 249,90")).toBe("R$ 249,90");
  });

  it("código do SKU é volátil nas capturas", () => {
    expect(isVolatileField("Código")).toBe(true);
    expect(isVolatileField("Preço")).toBe(false);
    expect(isVolatileValue("Código", "CDLA1B2C-AREI-P")).toBe(true);
    expect(isVolatileValue("Código", null)).toBe(false);
    expect(isVolatileValue("Preço", "R$ 10,00")).toBe(false);
  });
});
