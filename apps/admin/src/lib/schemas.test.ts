import { describe, expect, it } from "vitest";
import { z } from "zod";
import {
  adminPage,
  auditActorOptionSchema,
  auditEntrySchema,
  orderDetailSchema,
  productFormSchema,
  restoreResultSchema,
  skuFormSchema,
  trashItemSchema,
  trashPageSchema,
} from "./schemas";

describe("formulários do backoffice", () => {
  it("separa tamanhos por espaço ou vírgula e converte preços", () => {
    const parsed = skuFormSchema.parse({
      color: "Terracota",
      colorHex: "#b4532a",
      sizes: "P, M  G",
      price: "199.90",
      salePrice: "149,90",
      initialStock: "5",
    });

    expect(parsed.sizes).toEqual(["P", "M", "G"]);
    expect(parsed.price).toBe(199.9);
    expect(parsed.salePrice).toBe(149.9);
    expect(parsed.initialStock).toBe(5);
  });

  it("recusa promocional maior que o preço cheio", () => {
    const result = skuFormSchema.safeParse({
      color: "Preto",
      colorHex: "#000000",
      sizes: "M",
      price: "100",
      salePrice: "120",
      initialStock: "1",
    });

    expect(result.success).toBe(false);
    expect(result.error!.issues[0].path).toEqual(["salePrice"]);
  });

  it("coleção vazia vira null", () => {
    const parsed = productFormSchema.parse({
      name: "Camiseta",
      description: "Uma descrição longa o bastante.",
      brand: "Kamus",
      categoryId: "abc",
      collectionId: "",
    });

    expect(parsed.collectionId).toBeNull();
  });
});

/** Entrada de auditoria como a API devolve (camelCase, nulos explícitos). */
const auditEntry = {
  id: "0199c2a1-0000-7000-8000-000000000031",
  occurredAt: "2026-10-07T19:42:00Z",
  module: "orders",
  entityType: "Order",
  entityId: "0199c2a1-7f3e-7b21-9c4d-5e8f0a1b2c3d",
  action: "shipped",
  subjectType: "Order",
  subjectId: "0199c2a1-7f3e-7b21-9c4d-5e8f0a1b2c3d",
  subjectLabel: "Pedido KM10003",
  detail: "Maria Silva",
  changes: [
    { field: "Situação", before: "Pago", after: "Enviado" },
    { field: "Rastreio", before: null, after: "BR123456789BR" },
  ],
  actor: {
    kind: "Admin",
    id: "0199b000-0000-7000-8000-000000000001",
    name: "Administrador Kamus",
    email: "admin@kamus.dev",
  },
  correlationId: "0HN7GK2Q4E9M1:00000031",
  ipAddress: "203.0.113.24",
};

describe("auditoria", () => {
  it("lê a página de entradas com antes → depois e ator", () => {
    const page = adminPage(auditEntrySchema).parse({
      items: [auditEntry],
      total: 1,
      page: 1,
      pageSize: 20,
    });

    expect(page.total).toBe(1);
    expect(page.items[0].changes[1]).toEqual({
      field: "Rastreio",
      before: null,
      after: "BR123456789BR",
    });
    expect(page.items[0].actor.kind).toBe("Admin");
  });

  it("ator do sistema vem sem id nem e-mail; campos opcionais ausentes viram null", () => {
    const entry = auditEntrySchema.parse({
      ...auditEntry,
      action: "paid",
      subjectLabel: undefined,
      detail: undefined,
      correlationId: undefined,
      ipAddress: undefined,
      actor: { kind: "System", id: null, name: "FakePay", email: null },
    });

    expect(entry.actor).toEqual({ kind: "System", id: null, name: "FakePay", email: null });
    expect(entry.subjectLabel).toBeNull();
    expect(entry.detail).toBeNull();
    expect(entry.correlationId).toBeNull();
    expect(entry.ipAddress).toBeNull();
  });

  it("recusa tipo de ator fora do contrato (enum numérico)", () => {
    const result = auditEntrySchema.safeParse({
      ...auditEntry,
      actor: { ...auditEntry.actor, kind: 0 },
    });

    expect(result.success).toBe(false);
  });

  it("histórico de um item é uma lista simples de entradas", () => {
    const entries = z.array(auditEntrySchema).parse([auditEntry, { ...auditEntry, id: "outra" }]);

    expect(entries).toHaveLength(2);
  });

  it("lê as opções do filtro de usuários (usuário e sistema)", () => {
    const actors = z.array(auditActorOptionSchema).parse([
      {
        key: "0199b000-0000-7000-8000-000000000001",
        kind: "Admin",
        name: "Administrador Kamus",
        email: "admin@kamus.dev",
      },
      { key: "system:FakePay", kind: "System", name: "FakePay", email: null },
    ]);

    expect(actors.map((a) => a.key)).toEqual([
      "0199b000-0000-7000-8000-000000000001",
      "system:FakePay",
    ]);
    expect(actors[1].email).toBeNull();
  });
});

describe("lixeira", () => {
  const product = {
    type: "product",
    id: "0199b8e0-41d2-7a6c-8e17-2f4b6c9d0e11",
    productId: "0199b8e0-41d2-7a6c-8e17-2f4b6c9d0e11",
    name: "Boné Trucker",
    detail: "Kamus Studio · 2 SKUs · 1 imagem",
    imageUrl: "/files/catalog/bone-trucker.png",
    deletedAt: "2026-10-07T19:38:00Z",
    deletedBy: { id: "0199b000-0000-7000-8000-000000000001", name: "Administrador Kamus" },
    purgeAt: "2026-11-06T19:38:00Z",
    skuCount: 2,
    imageCount: 1,
    wasActive: true,
  };

  it("lê itens, total e contagem por tipo", () => {
    const page = trashPageSchema.parse({
      items: [
        product,
        {
          ...product,
          type: "sku",
          id: "0199b8e0-0000-7000-8000-000000000042",
          name: "Calça Chino · Verde-oliva · 42",
          detail: "KM067-VERDE-OLIVA-42",
          imageUrl: null,
          skuCount: 0,
          imageCount: 0,
          wasActive: false,
        },
      ],
      total: 2,
      counts: { product: 1, sku: 1, image: 0 },
    });

    expect(page.total).toBe(2);
    expect(page.items.map((i) => i.type)).toEqual(["product", "sku"]);
    expect(page.items[0].deletedBy.name).toBe("Administrador Kamus");
    expect(page.items[1].imageUrl).toBeNull();
    expect(page.counts).toEqual({ product: 1, sku: 1, image: 0 });
  });

  it("normaliza o tipo e completa campos ausentes", () => {
    const item = trashItemSchema.parse({
      ...product,
      type: "Image",
      detail: undefined,
      imageUrl: undefined,
      deletedBy: { id: null, name: null },
      skuCount: null,
      imageCount: undefined,
      wasActive: null,
    });

    expect(item.type).toBe("image");
    expect(item.detail).toBeNull();
    expect(item.imageUrl).toBeNull();
    expect(item.deletedBy).toEqual({ id: null, name: null });
    expect(item.skuCount).toBe(0);
    expect(item.imageCount).toBe(0);
    expect(item.wasActive).toBe(false);
  });

  it("sem quem excluiu, deletedBy vira um objeto vazio", () => {
    expect(trashItemSchema.parse({ ...product, deletedBy: null }).deletedBy).toEqual({
      id: null,
      name: null,
    });
  });

  it("recusa tipo desconhecido", () => {
    expect(trashItemSchema.safeParse({ ...product, type: "order" }).success).toBe(false);
  });

  it("restauração devolve a frase pronta", () => {
    const result = restoreResultSchema.parse({
      message: "Boné Trucker voltou como estava: publicado, com 2 SKUs e 1 imagem.",
    });

    expect(result.message).toBe(
      "Boné Trucker voltou como estava: publicado, com 2 SKUs e 1 imagem.",
    );
    expect(restoreResultSchema.safeParse({}).success).toBe(false);
  });
});

describe("histórico do pedido", () => {
  const order = {
    order: {
      id: "o1",
      number: "KM10003",
      status: "Shipped",
      items: [],
      address: {
        recipientName: "Maria Silva",
        postalCode: "22041001",
        street: "Avenida Atlântica",
        number: "500",
        complement: null,
        district: "Copacabana",
        city: "Rio de Janeiro",
        state: "RJ",
      },
      shipping: { region: "Sudeste", cost: 19.9, estimatedDays: 3 },
      subtotal: 189.9,
      total: 209.8,
      createdAt: "2026-10-07T19:02:00Z",
      history: [
        { status: "Created", at: "2026-10-07T19:02:00Z", note: null },
        {
          status: "Paid",
          at: "2026-10-07T19:05:00Z",
          note: null,
          actorKind: "System",
          actorName: "FakePay",
        },
        {
          status: "Shipped",
          at: "2026-10-07T19:42:00Z",
          note: "Rastreio BR123456789BR",
          actorKind: "Admin",
          actorName: "Administrador Kamus",
        },
      ],
      canCancel: false,
      trackingCode: "BR123456789BR",
    },
    customerId: "c1",
    customerEmail: "maria.silva@kamus.test",
    customerName: "Maria Silva",
    paymentId: null,
  };

  it("traz o ator de cada mudança; entradas antigas ficam sem ator", () => {
    const { history } = orderDetailSchema.parse(order).order;

    expect(history[0]).toMatchObject({ actorKind: null, actorName: null });
    expect(history[1]).toMatchObject({ actorKind: "System", actorName: "FakePay" });
    expect(history[2]).toMatchObject({ actorKind: "Admin", actorName: "Administrador Kamus" });
  });
});
