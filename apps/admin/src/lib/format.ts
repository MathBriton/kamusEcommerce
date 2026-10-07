const brl = new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" });
const brlCompact = new Intl.NumberFormat("pt-BR", {
  style: "currency",
  currency: "BRL",
  notation: "compact",
  maximumFractionDigits: 1,
});
const integer = new Intl.NumberFormat("pt-BR");
const percent = new Intl.NumberFormat("pt-BR", { style: "percent", maximumFractionDigits: 1 });
const dateTime = new Intl.DateTimeFormat("pt-BR", { dateStyle: "short", timeStyle: "short" });
const shortDate = new Intl.DateTimeFormat("pt-BR", {
  day: "2-digit",
  month: "short",
  timeZone: "UTC",
});

export const formatPrice = (value: number) => brl.format(value);
export const formatPriceCompact = (value: number) => brlCompact.format(value);
export const formatInt = (value: number) => integer.format(value);
export const formatPercent = (value: number) => percent.format(value);
export const formatDateTime = (iso: string) => dateTime.format(new Date(iso));
/** Datas "yyyy-MM-dd" (sem fuso) vindas dos relatórios. */
export const formatDay = (isoDate: string) =>
  shortDate.format(new Date(`${isoDate}T00:00:00Z`)).replace(".", "");
