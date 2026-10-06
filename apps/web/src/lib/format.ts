const brl = new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" });

export const formatPrice = (value: number) => brl.format(value);

/** Percentual de desconto arredondado, ex.: 300 → 199,90 = 33. */
export const discountPercent = (price: number, salePrice: number) =>
  Math.round((1 - salePrice / price) * 100);
