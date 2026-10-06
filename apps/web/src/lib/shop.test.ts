import { describe, expect, it } from "vitest";
import { addressFormSchema, registerFormSchema } from "./shop";

describe("formulários de compra", () => {
  const address = {
    recipientName: "Maria Silva",
    postalCode: "01310-100",
    street: "Avenida Paulista",
    number: "1000",
    complement: "",
    district: "Bela Vista",
    city: "São Paulo",
    state: "SP",
  };

  it("normaliza o CEP para 8 dígitos", () => {
    expect(addressFormSchema.parse(address).postalCode).toBe("01310100");
  });

  it("rejeita CEP incompleto e UF inválida", () => {
    const result = addressFormSchema.safeParse({ ...address, postalCode: "0131", state: "XX" });

    expect(result.success).toBe(false);
    const fields = result.error!.issues.map((i) => i.path[0]);
    expect(fields).toEqual(expect.arrayContaining(["postalCode", "state"]));
  });

  it("exige senha com 8 caracteres no cadastro", () => {
    const result = registerFormSchema.safeParse({
      fullName: "Ana",
      email: "ana@kamus.test",
      password: "123",
    });

    expect(result.success).toBe(false);
    expect(result.error!.issues[0].message).toContain("8 caracteres");
  });
});
