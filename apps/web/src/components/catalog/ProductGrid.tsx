import type { ProductCard as Card } from "@/lib/catalog";
import { ProductCard } from "./ProductCard";

export function ProductGrid({
  products,
  priorityCount = 4,
}: {
  products: Card[];
  priorityCount?: number;
}) {
  if (products.length === 0) {
    return (
      <p className="py-16 text-center text-muted">Nenhum produto encontrado com esses filtros.</p>
    );
  }

  return (
    <div className="grid grid-cols-2 gap-x-4 gap-y-8 lg:grid-cols-4">
      {products.map((product, index) => (
        <ProductCard key={product.id} product={product} priority={index < priorityCount} />
      ))}
    </div>
  );
}
