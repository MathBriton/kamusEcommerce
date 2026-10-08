import type { TrashItemType } from "./schemas";

/** Prazo da lixeira antes do expurgo automático (Catalog:TrashRetentionDays na API). */
export const TRASH_RETENTION_DAYS = 30;

/** Abas da lixeira: valor na URL (?tipo=) → tipo da API (?type=). */
export const TRASH_TABS = [
  { value: "", type: "all", label: "Tudo" },
  { value: "produto", type: "product", label: "Produtos" },
  { value: "sku", type: "sku", label: "SKUs" },
  { value: "imagem", type: "image", label: "Imagens" },
] as const;

export type TrashTab = (typeof TRASH_TABS)[number];

export const trashTab = (value: unknown): TrashTab =>
  TRASH_TABS.find((t) => t.value === value) ?? TRASH_TABS[0];

export const TRASH_TYPE_LABEL: Record<TrashItemType, string> = {
  product: "Produto",
  sku: "SKU",
  image: "Imagem",
};

const DAY = 24 * 60 * 60 * 1000;

/** Quando o item some de vez: "em 12 dias", "amanhã", "hoje". `soon` destaca o que está no fim. */
export function purgeLabel(purgeAt: string, now: number): { label: string; soon: boolean } {
  const days = Math.ceil((Date.parse(purgeAt) - now) / DAY);
  if (days <= 0) return { label: "hoje", soon: true };
  if (days === 1) return { label: "amanhã", soon: true };
  return { label: `em ${days} dias`, soon: false };
}

const skus = (n: number) => (n === 1 ? "o SKU" : `os ${n} SKUs`);
const images = (n: number) => (n === 1 ? "a imagem" : `as ${n} imagens`);
const capitalize = (text: string) => text.charAt(0).toUpperCase() + text.slice(1);

/**
 * O que vai junto com o produto para a lixeira, para o diálogo de confirmação.
 * Sem `imageCount` (lista de produtos), fala das imagens sem número.
 */
export function cascadeSummary(skuCount: number, imageCount?: number): string {
  const parts: string[] = [];
  if (skuCount > 0) parts.push(skus(skuCount));
  if (imageCount === undefined) parts.push("as imagens do produto");
  else if (imageCount > 0) parts.push(images(imageCount));

  if (parts.length === 0) return "O produto ainda não tem SKUs nem imagens.";

  const subject = capitalize(parts.join(" e "));
  const plural = parts.length > 1 || skuCount > 1 || (imageCount ?? 2) > 1;
  return plural
    ? `${subject} vão junto e voltam junto ao restaurar.`
    : `${subject} vai junto e volta junto ao restaurar.`;
}
