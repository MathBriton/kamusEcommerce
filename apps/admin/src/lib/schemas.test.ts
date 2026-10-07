import { describe, expect, it } from "vitest";
import { productFormSchema, skuFormSchema } from "./schemas";

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
