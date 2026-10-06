import { z } from "zod";

/** Schemas e tipos das áreas de compra (carrinho, conta, checkout e pedidos). */

export const cartItemSchema = z.object({
  skuId: z.string(),
  code: z.string(),
  productName: z.string(),
  productPath: z.string(),
  color: z.string(),
  size: z.string(),
  imageUrl: z.string().nullable(),
  quantity: z.number(),
  unitPrice: z.number(),
  listPrice: z.number(),
  priceWhenAdded: z.number(),
  lineTotal: z.number(),
  available: z.number(),
  issue: z.enum(["unavailable", "out_of_stock", "insufficient_stock", "price_changed"]).nullable(),
});

export const cartSchema = z.object({
  items: z.array(cartItemSchema),
  itemCount: z.number(),
  subtotal: z.number(),
  hasIssues: z.boolean(),
});

export const meSchema = z.object({ id: z.string(), email: z.string(), fullName: z.string() });

export const checkoutSchema = z.object({
  items: z.array(
    z.object({
      skuId: z.string(),
      productName: z.string(),
      productPath: z.string(),
      color: z.string(),
      size: z.string(),
      imageUrl: z.string().nullable(),
      quantity: z.number(),
      unitPrice: z.number(),
      listPrice: z.number(),
      lineTotal: z.number(),
      available: z.number(),
      issue: z.string().nullable(),
    }),
  ),
  subtotal: z.number(),
  freeShippingThreshold: z.number(),
  canPlaceOrder: z.boolean(),
});

export const shippingQuoteSchema = z.object({
  state: z.string(),
  region: z.string(),
  cost: z.number(),
  estimatedDays: z.number(),
});

export const placedOrderSchema = z.object({
  id: z.string(),
  number: z.string(),
  status: z.string(),
  total: z.number(),
});

export const orderStatusSchema = z.enum([
  "Created",
  "AwaitingPayment",
  "Paid",
  "Shipped",
  "Delivered",
  "Cancelled",
  "PaymentFailed",
]);

const addressSchema = z.object({
  recipientName: z.string(),
  postalCode: z.string(),
  street: z.string(),
  number: z.string(),
  complement: z.string().nullable(),
  district: z.string(),
  city: z.string(),
  state: z.string(),
});

export const orderSummarySchema = z.object({
  id: z.string(),
  number: z.string(),
  status: orderStatusSchema,
  total: z.number(),
  itemCount: z.number(),
  imageUrl: z.string().nullable(),
  createdAt: z.string(),
});

export const orderDetailSchema = z.object({
  id: z.string(),
  number: z.string(),
  status: orderStatusSchema,
  items: z.array(
    z.object({
      skuId: z.string(),
      skuCode: z.string(),
      productName: z.string(),
      productPath: z.string(),
      color: z.string(),
      size: z.string(),
      imageUrl: z.string().nullable(),
      unitPrice: z.number(),
      listPrice: z.number(),
      quantity: z.number(),
      lineTotal: z.number(),
    }),
  ),
  address: addressSchema,
  shipping: shippingQuoteSchema,
  subtotal: z.number(),
  total: z.number(),
  createdAt: z.string(),
  history: z.array(
    z.object({ status: orderStatusSchema, at: z.string(), note: z.string().nullable() }),
  ),
  canCancel: z.boolean(),
});

export type Cart = z.infer<typeof cartSchema>;
export type CartItem = z.infer<typeof cartItemSchema>;
export type Me = z.infer<typeof meSchema>;
export type Checkout = z.infer<typeof checkoutSchema>;
export type ShippingQuote = z.infer<typeof shippingQuoteSchema>;
export type OrderStatus = z.infer<typeof orderStatusSchema>;
export type OrderSummary = z.infer<typeof orderSummarySchema>;
export type OrderDetail = z.infer<typeof orderDetailSchema>;

export const ORDER_STATUS_LABEL: Record<OrderStatus, string> = {
  Created: "Criado",
  AwaitingPayment: "Aguardando pagamento",
  Paid: "Pago",
  Shipped: "Enviado",
  Delivered: "Entregue",
  Cancelled: "Cancelado",
  PaymentFailed: "Pagamento recusado",
};

export const UFS = [
  "AC",
  "AL",
  "AM",
  "AP",
  "BA",
  "CE",
  "DF",
  "ES",
  "GO",
  "MA",
  "MG",
  "MS",
  "MT",
  "PA",
  "PB",
  "PE",
  "PI",
  "PR",
  "RJ",
  "RN",
  "RO",
  "RR",
  "RS",
  "SC",
  "SE",
  "SP",
  "TO",
] as const;

/** Cartões de teste do FakePay (gateway simulado). */
export const TEST_CARDS = [
  { number: "4242424242424242", label: "Pagamento aprovado" },
  { number: "4000000000000002", label: "Pagamento recusado" },
  { number: "4000000000000119", label: "Sem resposta (a reserva expira em 15 min)" },
  { number: "4000000000000259", label: "Aprovado com webhook duplicado" },
] as const;

/** Formulários (validação no cliente com Zod; a API valida de novo com FluentValidation). */
export const loginFormSchema = z.object({
  email: z.email("Informe um e-mail válido."),
  password: z.string().min(1, "Informe a senha."),
});

export const registerFormSchema = z.object({
  fullName: z.string().trim().min(3, "Informe seu nome completo."),
  email: z.email("Informe um e-mail válido."),
  password: z.string().min(8, "A senha precisa ter pelo menos 8 caracteres."),
});

export const addressFormSchema = z.object({
  recipientName: z.string().trim().min(3, "Informe o nome de quem recebe."),
  postalCode: z
    .string()
    .transform((v) => v.replace(/\D/g, ""))
    .refine((v) => v.length === 8, "CEP deve ter 8 dígitos."),
  street: z.string().trim().min(2, "Informe a rua."),
  number: z.string().trim().min(1, "Informe o número."),
  complement: z.string().trim().optional(),
  district: z.string().trim().min(2, "Informe o bairro."),
  city: z.string().trim().min(2, "Informe a cidade."),
  state: z.enum(UFS, "Selecione a UF."),
});

export type AddressForm = z.infer<typeof addressFormSchema>;

/** Erro no formato ProblemDetails devolvido pela API. */
export const problemSchema = z.object({
  title: z.string().optional(),
  detail: z.string().optional(),
  code: z.string().optional(),
  errors: z.record(z.string(), z.array(z.string())).optional(),
});

export async function readProblem(response: Response): Promise<string> {
  try {
    const problem = problemSchema.parse(await response.json());
    if (problem.errors) return Object.values(problem.errors).flat().join(" ");
    return problem.detail ?? problem.title ?? "Algo deu errado.";
  } catch {
    return "Algo deu errado. Tente novamente.";
  }
}

export function notifyCartChanged() {
  window.dispatchEvent(new Event("kamus:cart-changed"));
}

export function notifyAuthChanged() {
  window.dispatchEvent(new Event("kamus:auth-changed"));
}
