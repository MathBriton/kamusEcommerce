import { describe, expect, it } from "vitest";
import { listingQuery } from "@/lib/catalog";
import {
  isDefaultState,
  parseListingState,
  serializeListingState,
  toApiFilters,
} from "./listing-state";

describe("listing-state", () => {
  it("faz ida e volta pela URL", () => {
    const params = new URLSearchParams(
      "tamanho=M&tamanho=G&cor=Azul&preco=100-200&ordem=price_asc",
    );
    const state = parseListingState(params);

    expect(state).toEqual({
      sizes: ["M", "G"],
      colors: ["Azul"],
      price: { min: 100, max: 200 },
      sort: "price_asc",
    });
    expect(serializeListingState(state)).toBe(params.toString());
  });

  it("ignora ordenação e faixa de preço inválidas", () => {
    const state = parseListingState(new URLSearchParams("ordem=hack&preco=abc"));

    expect(state.sort).toBe("newest");
    expect(state.price).toBeUndefined();
    expect(isDefaultState(state)).toBe(true);
  });

  it("aceita faixa aberta", () => {
    expect(parseListingState(new URLSearchParams("preco=300-")).price).toEqual({
      min: 300,
      max: undefined,
    });
  });

  it("gera a query da API com parâmetros repetidos", () => {
    const state = parseListingState(new URLSearchParams("tamanho=P&tamanho=M&preco=0-100"));
    const query = listingQuery({ category: "feminino", ...toApiFilters(state), limit: 24 });

    expect(query).toBe(
      "category=feminino&size=P&size=M&minPrice=0&maxPrice=100&sort=newest&limit=24",
    );
  });
});
