import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { CheckoutForm } from "@/components/shop/CheckoutForm";
import { ApiError } from "@/lib/api";
import { apiGetWithSession } from "@/lib/server-api";
import { checkoutSchema, meSchema } from "@/lib/shop";

export const metadata: Metadata = { title: "Checkout", robots: { index: false } };

export default async function CheckoutPage() {
  let data;
  try {
    // Entrar no checkout revalida preço e estoque de cada item na API.
    const [me, checkout] = await Promise.all([
      apiGetWithSession("/api/identity/me", meSchema),
      apiGetWithSession("/api/checkout", checkoutSchema),
    ]);
    data = { me, checkout };
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) redirect("/entrar?next=/checkout");
    throw error;
  }

  if (!data.checkout || data.checkout.items.length === 0) redirect("/carrinho");
  if (!data.checkout.canPlaceOrder) redirect("/carrinho");

  return (
    <div className="mx-auto max-w-6xl px-4 py-10">
      <h1 className="mb-8 font-display text-4xl">Checkout</h1>
      <CheckoutForm checkout={data.checkout} customerName={data.me?.fullName ?? ""} />
    </div>
  );
}
