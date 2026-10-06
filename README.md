# Kamus

E-commerce de moda **fictício** para estudar arquitetura e system design: monólito modular em
.NET 10, storefront em Next.js, PostgreSQL e Redis.

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

## O que já funciona

- **Vitrine (R1):** menu de categorias, PLP com filtros por tamanho, cor e preço, ordenação e
  paginação por cursor; PDP com galeria por cor, disponibilidade por tamanho e preço "de/por";
  URLs amigáveis (`/masculino/calcas/jeans/calca-jeans-slim`), `sitemap.xml` e Open Graph.

## Estrutura

```
apps/api          Host ASP.NET Core do monólito
apps/web          Storefront Next.js (App Router)
src/modules/*     Módulos: Catalog, Inventory, Cart, Orders, Payments, Identity
src/shared        Building blocks (Result, IModule, infraestrutura)
tests/            Testes de integração (Testcontainers) e de arquitetura
docs/             ADRs e diagramas C4
```

## Testes

```bash
dotnet test --solution Kamus.slnx       # precisa do Docker rodando (Testcontainers)
cd apps/web && npm test
```

## Documentação

- [Arquitetura (C4)](docs/architecture.md)
- [Decisões de arquitetura (ADRs)](docs/adr)
- [Como contribuir](CONTRIBUTING.md)
