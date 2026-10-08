import { describe, expect, it } from "vitest";
import { cascadeSummary, purgeLabel, trashTab } from "./trash";

describe("abas da lixeira", () => {
  it("traduz ?tipo= para o tipo da API; valor inválido vira Tudo", () => {
    expect(trashTab("produto").type).toBe("product");
    expect(trashTab("sku").type).toBe("sku");
    expect(trashTab("imagem").type).toBe("image");
    expect(trashTab(undefined).type).toBe("all");
    expect(trashTab("pedido").type).toBe("all");
    expect(trashTab(["sku"]).type).toBe("all");
  });
});

describe("prazo até o expurgo", () => {
  const now = Date.parse("2026-10-07T19:40:00Z");

  it("conta os dias que faltam", () => {
    expect(purgeLabel("2026-11-06T19:38:00Z", now)).toEqual({ label: "em 30 dias", soon: false });
    expect(purgeLabel("2026-10-20T09:00:00Z", now)).toEqual({ label: "em 13 dias", soon: false });
  });

  it("destaca o que some amanhã ou hoje", () => {
    expect(purgeLabel("2026-10-08T18:00:00Z", now)).toEqual({ label: "amanhã", soon: true });
    expect(purgeLabel("2026-10-07T19:00:00Z", now)).toEqual({ label: "hoje", soon: true });
  });
});

describe("o que vai junto com o produto", () => {
  it.each([
    [2, 1, "Os 2 SKUs e a imagem vão junto e voltam junto ao restaurar."],
    [3, 2, "Os 3 SKUs e as 2 imagens vão junto e voltam junto ao restaurar."],
    [1, 0, "O SKU vai junto e volta junto ao restaurar."],
    [4, 0, "Os 4 SKUs vão junto e voltam junto ao restaurar."],
    [0, 1, "A imagem vai junto e volta junto ao restaurar."],
    [0, 0, "O produto ainda não tem SKUs nem imagens."],
  ])("%i SKUs e %i imagens", (skus, images, text) => {
    expect(cascadeSummary(skus, images)).toBe(text);
  });

  it("sem a contagem de imagens (lista de produtos), fala delas sem número", () => {
    expect(cascadeSummary(2)).toBe(
      "Os 2 SKUs e as imagens do produto vão junto e voltam junto ao restaurar.",
    );
    expect(cascadeSummary(0)).toBe("As imagens do produto vão junto e voltam junto ao restaurar.");
  });
});
