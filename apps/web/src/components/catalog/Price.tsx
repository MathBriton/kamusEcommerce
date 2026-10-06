import { discountPercent, formatPrice } from "@/lib/format";

type Props = { price: number; salePrice: number | null; size?: "sm" | "lg" };

/** Preço "de/por": riscado + promocional quando há desconto. */
export function Price({ price, salePrice, size = "sm" }: Props) {
  const big = size === "lg";

  if (salePrice === null) {
    return <p className={big ? "text-2xl" : "text-sm"}>{formatPrice(price)}</p>;
  }

  return (
    <p className={`flex flex-wrap items-baseline gap-x-2 ${big ? "text-2xl" : "text-sm"}`}>
      <span className="sr-only">De</span>
      <s className={`text-muted ${big ? "text-base" : "text-xs"}`}>{formatPrice(price)}</s>
      <span className="sr-only">por</span>
      <span className="font-medium text-sale">{formatPrice(salePrice)}</span>
      <span className={`text-sale ${big ? "text-sm" : "text-xs"}`}>
        -{discountPercent(price, salePrice)}%
      </span>
    </p>
  );
}
