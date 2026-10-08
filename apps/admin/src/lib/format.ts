const brl = new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" });
const brlCompact = new Intl.NumberFormat("pt-BR", {
  style: "currency",
  currency: "BRL",
  notation: "compact",
  maximumFractionDigits: 1,
});
const integer = new Intl.NumberFormat("pt-BR");
const percent = new Intl.NumberFormat("pt-BR", { style: "percent", maximumFractionDigits: 1 });
/**
 * Horários sempre no fuso da loja (o painel já fala em "horário de Brasília"). Fuso fixo também
 * deixa a renderização no servidor igual à do navegador (sem divergência na hidratação).
 */
export const STORE_TIME_ZONE = "America/Sao_Paulo";
const dateTime = new Intl.DateTimeFormat("pt-BR", {
  dateStyle: "short",
  timeStyle: "short",
  timeZone: STORE_TIME_ZONE,
});
const dateOnly = new Intl.DateTimeFormat("pt-BR", {
  day: "2-digit",
  month: "2-digit",
  year: "numeric",
  timeZone: STORE_TIME_ZONE,
});
const timeOnly = new Intl.DateTimeFormat("pt-BR", {
  hour: "2-digit",
  minute: "2-digit",
  timeZone: STORE_TIME_ZONE,
});
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
/** "07/10/2026" */
export const formatDate = (iso: string) => dateOnly.format(new Date(iso));
/** "16:42" */
export const formatTime = (iso: string) => timeOnly.format(new Date(iso));
/** Datas "yyyy-MM-dd" (sem fuso) vindas dos relatórios. */
export const formatDay = (isoDate: string) =>
  shortDate.format(new Date(`${isoDate}T00:00:00Z`)).replace(".", "");
