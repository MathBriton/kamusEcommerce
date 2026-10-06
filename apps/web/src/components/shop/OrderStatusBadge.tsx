import { ORDER_STATUS_LABEL, type OrderStatus } from "@/lib/shop";

const STYLE: Record<OrderStatus, string> = {
  Created: "bg-sand text-ink",
  AwaitingPayment: "bg-amber-100 text-amber-900",
  Paid: "bg-emerald-100 text-emerald-900",
  Shipped: "bg-sky-100 text-sky-900",
  Delivered: "bg-emerald-200 text-emerald-950",
  Cancelled: "bg-stone-200 text-stone-700",
  PaymentFailed: "bg-red-100 text-red-900",
};

export function OrderStatusBadge({ status }: { status: OrderStatus }) {
  return (
    <span className={`px-2 py-1 text-xs whitespace-nowrap ${STYLE[status]}`}>
      {ORDER_STATUS_LABEL[status]}
    </span>
  );
}
