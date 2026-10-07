# Kamus

E-commerce de moda **fictício** para estudar arquitetura e system design: monólito modular em
.NET 10, storefront em Next.js, PostgreSQL e Redis.

![Fluxo de compra: vitrine, sacola, cadastro, checkout, pagamento e acompanhamento do pedido](docs/media/fluxo-de-compra.gif)

> Marca, identidade visual e conteúdo são próprios e fictícios. Roadmap completo em [`stack.md`](stack.md).

## Rodando localmente

Pré-requisito: Docker com Compose.

1. `git clone https://github.com/MathBriton/kamusEcommerce.git && cd kamusEcommerce`
2. `docker compose up -d --build`
3. Abra a loja em <http://localhost:3000> (o catálogo de exemplo é criado na primeira subida)
4. A API responde em <http://localhost:5080/health> e documenta os endpoints em `/openapi/v1.json`

Para desenvolver fora do Docker (hot reload), suba só a infraestrutura com
`docker compose up -d postgres redis` e rode:

```bash
dotnet run --project apps/api        # .NET 10 SDK → http://localhost:5080
cd apps/web && npm install && npm run dev   # Node 22 → http://localhost:3000
```

## O que já funciona (MVP = R0 + R1 + R2)

- **Vitrine (R1):** menu de categorias, PLP com filtros por tamanho, cor e preço, ordenação e
  paginação por cursor; PDP com galeria por cor, disponibilidade por tamanho e preço "de/por";
  URLs amigáveis (`/masculino/calcas/jeans/calca-jeans-slim`), `sitemap.xml` e Open Graph.
- **Compra (R2):** cadastro e login (cookie HttpOnly), sacola em Redis com merge no login,
  checkout com revalidação de preço e estoque, frete por região, reserva de estoque por 15 minutos
  com concorrência otimista, pagamento assíncrono via webhook idempotente e assinado, máquina de
  estados do pedido e "Minha conta" com acompanhamento.

### Cartões de teste (FakePay)

| Cartão | Resultado |
|---|---|
| `4242 4242 4242 4242` | Aprovado |
| `4000 0000 0000 0002` | Recusado |
| `4000 0000 0000 0119` | Sem resposta: a reserva expira em 15 min e o pedido é cancelado |
| `4000 0000 0000 0259` | Aprovado, com o webhook entregue três vezes (só uma tem efeito) |

No ambiente de demonstração, a página do pedido tem botões para simular envio e entrega.

## Estrutura

```
apps/api          Host ASP.NET Core do monólito
apps/web          Storefront Next.js (App Router)
src/modules/*     Módulos: Catalog, Inventory, Cart, Orders, Payments, Identity
src/shared        Building blocks (Result, IModule, infraestrutura)
tests/            Testes unitários, de integração (Testcontainers) e de arquitetura
docs/             ADRs e diagramas C4
```

## Testes

```bash
dotnet test --solution Kamus.slnx       # precisa do Docker rodando (Testcontainers)
cd apps/web && npm test
```

## Deploy

Blueprint do Render pronto em [`render.yaml`](render.yaml); passo a passo em [docs/deploy.md](docs/deploy.md).

## Documentação

- [Arquitetura (C4, fluxo de compra, estados do pedido)](docs/architecture.md)
- [Design system](docs/design-system/README.md)
- [Decisões de arquitetura (ADRs)](docs/adr)
- [Como contribuir](CONTRIBUTING.md)
