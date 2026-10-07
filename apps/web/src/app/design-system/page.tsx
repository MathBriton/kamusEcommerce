import type { Metadata } from "next";
import Image from "next/image";
import { notFound } from "next/navigation";
import { Price } from "@/components/catalog/Price";
import { ProductCard } from "@/components/catalog/ProductCard";
import { HealthStatus } from "@/components/HealthStatus";
import { OrderStatusBadge } from "@/components/shop/OrderStatusBadge";
import { Alert, Field, PrimaryButton, SelectField } from "@/components/shop/ui";
import { listProducts } from "@/lib/catalog";
import { orderStatusSchema } from "@/lib/shop";
import { ColorSwatches } from "./ColorSwatches";

/**
 * Guia vivo do design system da loja. Fonte das imagens em apps/web/design (geradas pelos
 * testes E2E). Desligado em produção: só existe com ENABLE_DESIGN_SYSTEM_PAGE=true.
 */
export const dynamic = "force-dynamic";
export const metadata: Metadata = { title: "Design system", robots: { index: false } };

const GARMENTS = [
  "feminino/vestidos",
  "feminino/blusas",
  "feminino/calcas/jeans",
  "feminino/saias",
  "feminino/jaquetas",
  "masculino/camisetas",
  "masculino/camisas",
  "masculino/bermudas",
  "acessorios/bolsas",
  "acessorios/bones",
];

function Section({
  id,
  title,
  children,
}: {
  id: string;
  title: string;
  children: React.ReactNode;
}) {
  return (
    <section id={id} className="bg-paper p-10">
      <p className="mb-1 text-xs tracking-widest text-accent uppercase">Kamus · Design system</p>
      <h2 className="mb-8 font-display text-3xl">{title}</h2>
      {children}
    </section>
  );
}

function Specimen({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex items-baseline gap-6">
      <span className="w-28 shrink-0 text-xs text-muted">{label}</span>
      {children}
    </div>
  );
}

export default async function DesignSystemPage() {
  if (process.env.ENABLE_DESIGN_SYSTEM_PAGE !== "true") notFound();

  const [cards, ...garments] = await Promise.all([
    listProducts({ collection: "outlet", sort: "price_asc", limit: 3 }),
    ...GARMENTS.map((category) => listProducts({ category, sort: "name", limit: 1 })),
  ]);
  const newest = await listProducts({ sort: "name", limit: 1 });

  return (
    <div className="mx-auto max-w-5xl space-y-6 py-6">
      <Section id="marca" title="Marca">
        <div className="grid grid-cols-2 gap-6">
          <div className="flex h-48 items-center justify-center bg-surface">
            <span className="font-display text-5xl tracking-[0.2em]">KAMUS</span>
          </div>
          <div className="flex h-48 items-center justify-center bg-ink">
            <span className="font-display text-5xl tracking-[0.2em] text-paper">KAMUS</span>
          </div>
        </div>
        <p className="mt-6 max-w-2xl text-muted">
          Moda com sotaque brasileiro. Tons de areia, tinta e terracota; serifa clássica para
          títulos e sans-serif do sistema para leitura.
        </p>
      </Section>

      <Section id="cores" title="Cores">
        <ColorSwatches />
      </Section>

      <Section id="tipografia" title="Tipografia">
        <div className="space-y-5">
          <Specimen label="Display 5xl">
            <span className="font-display text-5xl">Moda com sotaque brasileiro.</span>
          </Specimen>
          <Specimen label="Display 4xl">
            <span className="font-display text-4xl">Calça Jeans Slim</span>
          </Specimen>
          <Specimen label="Display 2xl">
            <span className="font-display text-2xl">Resumo do pedido</span>
          </Specimen>
          <Specimen label="Eyebrow">
            <span className="text-sm tracking-widest text-accent uppercase">Nova coleção</span>
          </Specimen>
          <Specimen label="Corpo lg">
            <span className="text-lg text-muted">
              Linho, tons terrosos e cortes amplos para dias quentes.
            </span>
          </Specimen>
          <Specimen label="Corpo sm">
            <span className="text-sm">
              Peça em algodão com acabamento cuidadoso e modelagem pensada para o dia a dia.
            </span>
          </Specimen>
          <Specimen label="Navegação">
            <span className="text-sm tracking-wide uppercase">
              Feminino · Masculino · Acessórios
            </span>
          </Specimen>
        </div>
      </Section>

      <Section id="botoes" title="Botões e links">
        <div className="grid grid-cols-2 gap-6">
          <PrimaryButton type="button">Adicionar à sacola</PrimaryButton>
          <PrimaryButton type="button" disabled>
            Selecione um tamanho
          </PrimaryButton>
          <button
            type="button"
            className="border border-ink px-8 py-3 text-sm tracking-wide uppercase"
          >
            Carregar mais
          </button>
          <div className="flex items-center gap-6 text-sm">
            <span className="text-accent underline underline-offset-4">Ver tudo</span>
            <span className="text-muted underline underline-offset-4">Remover</span>
            <span className="border border-line bg-surface px-3 py-1.5">Subcategoria</span>
          </div>
        </div>
      </Section>

      <Section id="formularios" title="Formulários">
        <div className="grid grid-cols-2 gap-6">
          <Field id="ds-email" label="E-mail" defaultValue="maria@kamus.com.br" />
          <Field id="ds-cep" label="CEP" defaultValue="0131" error="CEP deve ter 8 dígitos." />
          <SelectField id="ds-uf" label="UF" defaultValue="SP">
            <option>SP</option>
          </SelectField>
          <Field id="ds-complemento" label="Complemento (opcional)" placeholder="Apto, bloco…" />
        </div>
        <div className="mt-6">
          <Alert>O total do pedido mudou para R$ 259,80. Revise e confirme novamente.</Alert>
        </div>
      </Section>

      <Section id="selecao" title="Seleção de variação">
        <p className="mb-2 text-sm">
          Cor: <span className="font-medium">Terracota</span>
        </p>
        <div className="mb-6 flex gap-3">
          {["var(--color-accent)", "#1f1f1f", "#d9c7a7", "#3b6ea8", "#6b6f3a"].map((color, i) => (
            <span
              key={color}
              className={`size-11 rounded-full border-2 p-1 ${i === 0 ? "border-ink" : "border-transparent"}`}
            >
              <span
                className="block size-full rounded-full border border-black/15"
                style={{ backgroundColor: color }}
              />
            </span>
          ))}
        </div>
        <p className="mb-2 text-sm">Tamanho</p>
        <div className="flex gap-2">
          <span className="min-w-12 border border-line bg-surface px-3 py-2.5 text-center text-sm">
            PP
          </span>
          <span className="min-w-12 border border-line px-3 py-2.5 text-center text-sm text-muted line-through">
            P
          </span>
          <span className="min-w-12 border border-ink bg-ink px-3 py-2.5 text-center text-sm text-paper">
            M
          </span>
          <span className="min-w-12 border border-line bg-surface px-3 py-2.5 text-center text-sm">
            G
          </span>
          <span className="min-w-12 border border-line bg-surface px-3 py-2.5 text-center text-sm">
            GG
          </span>
        </div>
        <p className="mt-2 text-sm text-muted">Últimas 2 unidades!</p>
      </Section>

      <Section id="precos-e-status" title="Preços e status">
        <div className="mb-8 flex items-end gap-12">
          <Price price={199.9} salePrice={null} size="lg" />
          <Price price={349.9} salePrice={229.9} size="lg" />
          <Price price={129.9} salePrice={89.9} />
          <span className="bg-sale px-2 py-0.5 text-xs text-white">Promoção</span>
        </div>
        <div className="flex flex-wrap gap-3">
          {orderStatusSchema.options.map((status) => (
            <OrderStatusBadge key={status} status={status} />
          ))}
        </div>
        <div className="mt-8 max-w-sm">
          <HealthStatus
            result={{
              ok: true,
              health: {
                status: "Healthy",
                totalDurationMs: 2.1,
                checks: [
                  { name: "postgres", status: "Healthy", durationMs: 1.2 },
                  { name: "redis", status: "Healthy", durationMs: 0.4 },
                ],
              },
            }}
          />
        </div>
      </Section>

      <Section id="card-de-produto" title="Card de produto">
        <div className="grid grid-cols-4 gap-4">
          {[...newest.items, ...cards.items].map((p) => (
            <ProductCard key={p.id} product={p} />
          ))}
        </div>
      </Section>

      <Section id="ilustracoes" title="Ilustrações das peças">
        <div className="grid grid-cols-5 gap-4">
          {garments.map((g, i) => {
            const p = g.items[0];
            return p?.image ? (
              <figure key={GARMENTS[i]}>
                <div className="relative aspect-[3/4] bg-sand">
                  <Image
                    src={p.image.url}
                    alt={p.image.alt}
                    fill
                    unoptimized
                    className="object-cover"
                  />
                </div>
                <figcaption className="mt-2 text-xs text-muted">
                  {GARMENTS[i].split("/").at(-1)}
                </figcaption>
              </figure>
            ) : null;
          })}
        </div>
      </Section>
    </div>
  );
}
