import { z } from "zod";

export const meSchema = z.object({
  id: z.string(),
  email: z.string(),
  fullName: z.string(),
  isAdmin: z.boolean(),
});

export type CategoryNode = { id: string; name: string; path: string; children: CategoryNode[] };
export const categoryNodeSchema: z.ZodType<CategoryNode> = z.lazy(() =>
  z.object({
    id: z.string(),
    name: z.string(),
    path: z.string(),
    children: z.array(categoryNodeSchema),
  }),
);

export const collectionSchema = z.object({ id: z.string(), name: z.string(), slug: z.string() });

export const adminPage = <T extends z.ZodType>(item: T) =>
  z.object({ items: z.array(item), total: z.number(), page: z.number(), pageSize: z.number() });

export const productRowSchema = z.object({
  id: z.string(),
  name: z.string(),
  slug: z.string(),
  path: z.string(),
  brand: z.string(),
  categoryName: z.string(),
  isActive: z.boolean(),
  skuCount: z.number(),
  minPrice: z.number().nullable(),
  available: z.number(),
  imageUrl: z.string().nullable(),
  updatedAt: z.string(),
});

export const skuSchema = z.object({
  id: z.string(),
  code: z.string(),
  color: z.string(),
  colorHex: z.string(),
  size: z.string(),
  price: z.number(),
  salePrice: z.number().nullable(),
  quantity: z.number(),
  reserved: z.number(),
  available: z.number(),
});

export const productDetailSchema = z.object({
  id: z.string(),
  name: z.string(),
  slug: z.string(),
  path: z.string(),
  description: z.string(),
  brand: z.string(),
  categoryId: z.string(),
  collectionId: z.string().nullable(),
  isActive: z.boolean(),
  skus: z.array(skuSchema),
  images: z.array(
    z.object({
      id: z.string(),
      color: z.string(),
      url: z.string(),
      alt: z.string(),
      sortOrder: z.number(),
    }),
  ),
  createdAt: z.string(),
  updatedAt: z.string(),
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

export const orderRowSchema = z.object({
  id: z.string(),
  number: z.string(),
  status: orderStatusSchema,
  total: z.number(),
  itemCount: z.number(),
  recipientName: z.string(),
  city: z.string(),
  state: z.string(),
  createdAt: z.string(),
});

export const orderDetailSchema = z.object({
  order: z.object({
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
    address: z.object({
      recipientName: z.string(),
      postalCode: z.string(),
      street: z.string(),
      number: z.string(),
      complement: z.string().nullable(),
      district: z.string(),
      city: z.string(),
      state: z.string(),
    }),
    shipping: z.object({ region: z.string(), cost: z.number(), estimatedDays: z.number() }),
    subtotal: z.number(),
    total: z.number(),
    createdAt: z.string(),
    history: z.array(
      z.object({ status: orderStatusSchema, at: z.string(), note: z.string().nullable() }),
    ),
    canCancel: z.boolean(),
    trackingCode: z.string().nullable(),
  }),
  customerId: z.string(),
  customerEmail: z.string().nullable(),
  customerName: z.string().nullable(),
  paymentId: z.string().nullable(),
});

export const overviewSchema = z.object({
  period: z.object({ from: z.string(), to: z.string(), timeZone: z.string() }),
  kpis: z.object({
    revenue: z.number(),
    paidOrders: z.number(),
    averageTicket: z.number(),
    unitsSold: z.number(),
    totalOrders: z.number(),
    paymentFailureRate: z.number(),
    cancellationRate: z.number(),
  }),
  daily: z.array(z.object({ date: z.string(), revenue: z.number(), orders: z.number() })),
  statuses: z.array(z.object({ status: orderStatusSchema, count: z.number() })),
  topProducts: z.array(
    z.object({
      skuId: z.string(),
      productName: z.string(),
      productPath: z.string(),
      color: z.string(),
      size: z.string(),
      imageUrl: z.string().nullable(),
      units: z.number(),
      revenue: z.number(),
    }),
  ),
  lowStock: z.array(
    z.object({
      skuId: z.string(),
      code: z.string(),
      productName: z.string(),
      color: z.string(),
      size: z.string(),
      quantity: z.number(),
      reserved: z.number(),
      available: z.number(),
    }),
  ),
});

export type Me = z.infer<typeof meSchema>;
export type ProductRow = z.infer<typeof productRowSchema>;
export type ProductDetail = z.infer<typeof productDetailSchema>;
export type Sku = z.infer<typeof skuSchema>;
export type OrderStatus = z.infer<typeof orderStatusSchema>;
export type OrderRow = z.infer<typeof orderRowSchema>;
export type OrderDetail = z.infer<typeof orderDetailSchema>;
export type Overview = z.infer<typeof overviewSchema>;
export type Collection = z.infer<typeof collectionSchema>;

export const ORDER_STATUS_LABEL: Record<OrderStatus, string> = {
  Created: "Criado",
  AwaitingPayment: "Aguardando pagamento",
  Paid: "Pago",
  Shipped: "Enviado",
  Delivered: "Entregue",
  Cancelled: "Cancelado",
  PaymentFailed: "Pagamento recusado",
};

/** Formulários do backoffice (validação no cliente; a API valida de novo). */
export const productFormSchema = z.object({
  name: z.string().trim().min(2, "Informe o nome."),
  description: z.string().trim().min(10, "Descreva a peça (mín. 10 caracteres)."),
  brand: z.string().trim().min(2, "Informe a marca."),
  categoryId: z.string().min(1, "Escolha a categoria."),
  collectionId: z
    .string()
    .optional()
    .transform((v) => (v ? v : null)),
});

const price = z.coerce
  .number({ message: "Preço inválido." })
  .positive("Preço precisa ser positivo.");

export const skuFormSchema = z
  .object({
    color: z.string().trim().min(2, "Informe a cor."),
    colorHex: z.string().regex(/^#[0-9a-fA-F]{6}$/, "Cor em hexadecimal."),
    sizes: z
      .string()
      .transform((v) => v.split(/[,\s]+/).filter(Boolean))
      .refine((v) => v.length > 0, "Informe ao menos um tamanho."),
    price,
    salePrice: z
      .string()
      .optional()
      .transform((v) => (v ? Number(v.replace(",", ".")) : null)),
    initialStock: z.coerce.number().int().min(0, "Estoque não pode ser negativo."),
  })
  .refine((v) => v.salePrice === null || (v.salePrice > 0 && v.salePrice < v.price), {
    message: "Preço promocional precisa ser menor que o preço cheio.",
    path: ["salePrice"],
  });

export const problemSchema = z.object({
  title: z.string().optional(),
  detail: z.string().optional(),
  errors: z.record(z.string(), z.array(z.string())).optional(),
});

export async function readProblem(response: Response): Promise<string> {
  try {
    const problem = problemSchema.parse(await response.json());
    if (problem.errors) return Object.values(problem.errors).flat().join(" ");
    return problem.detail ?? problem.title ?? "Algo deu errado.";
  } catch {
    return `Erro ${response.status}. Tente novamente.`;
  }
}
