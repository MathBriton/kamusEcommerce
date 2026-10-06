import type { Metadata } from "next";
import { CartPage } from "@/components/shop/CartPage";

export const metadata: Metadata = { title: "Sacola", robots: { index: false } };

export default function Page() {
  return (
    <div className="mx-auto max-w-6xl px-4 py-10">
      <h1 className="mb-8 font-display text-4xl">Sacola</h1>
      <CartPage />
    </div>
  );
}
