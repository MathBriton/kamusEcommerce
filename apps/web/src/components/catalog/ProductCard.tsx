import Image from "next/image";
import Link from "next/link";
import type { ProductCard as Card } from "@/lib/catalog";
import { Price } from "./Price";

export function ProductCard({ product, priority = false }: { product: Card; priority?: boolean }) {
  return (
    <article className="group">
      <Link href={`/${product.path}`} className="block">
        <div className="relative aspect-[3/4] overflow-hidden bg-sand">
          {product.image && (
            <Image
              src={product.image.url}
              alt={product.image.alt}
              fill
              unoptimized
              priority={priority}
              sizes="(min-width: 1024px) 25vw, 50vw"
              className="object-cover transition-transform duration-500 group-hover:scale-105"
            />
          )}
          {product.salePrice !== null && (
            <span className="absolute top-2 left-2 bg-sale px-2 py-0.5 text-xs text-white">
              Promoção
            </span>
          )}
        </div>
        <div className="mt-3 space-y-1">
          <h3 className="text-sm leading-snug">{product.name}</h3>
          <Price price={product.price} salePrice={product.salePrice} />
        </div>
      </Link>
      <ul className="mt-2 flex gap-1" aria-label="Cores disponíveis">
        {product.colors.map((color) => (
          <li
            key={color.name}
            title={color.name}
            className="size-3 rounded-full border border-black/15"
            style={{ backgroundColor: color.hex }}
          >
            <span className="sr-only">{color.name}</span>
          </li>
        ))}
      </ul>
    </article>
  );
}
