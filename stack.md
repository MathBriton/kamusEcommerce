# Stack & Roadmap de Releases

> E-commerce de moda fictício para estudo de arquitetura e system design.
> Domínio inspirado em lojas de moda brasileiras; marca, identidade visual e conteúdo são próprios.

**Codinome do projeto:** `Kamus`
**Status:** R2 — concluída no código (MVP); deploy público pendente
**Última revisão:** 2026-10-06

---

## 1. Princípios

1. **Monólito modular primeiro.** Os módulos têm fronteiras explícitas (sem acesso direto às tabelas de outro módulo). Um serviço só é extraído quando houver um motivo documentado em ADR.
2. **Cada release é entregável e demonstrável sozinha**, com deploy funcionando e README atualizado.
3. **Decisões relevantes viram ADR** em `docs/adr/NNNN-titulo.md` (contexto, decisão, alternativas, consequências).
4. **Nada entra antes de ser necessário.** Mensageria, cache distribuído e observabilidade avançada têm release própria.
5. **Stack escolhida por demanda de mercado** (vagas remotas e nacionais), não por novidade.

---

## 2. Stack base (MVP)

| Camada | Tecnologia | Por quê |
|---|---|---|
| Backend | **.NET 10 / ASP.NET Core** (Minimal APIs ou Controllers) | Forte em vagas enterprise no Brasil e no exterior; boa performance; ecossistema maduro |
| ORM | **EF Core** + migrations | Padrão de mercado em .NET |
| Banco | **PostgreSQL 17** | Open source, muito pedido em vagas, roda em qualquer cloud |
| Cache / Carrinho | **Redis** | Carrinho com TTL; base para cache a partir da R3 |
| Frontend | **Next.js (App Router) + TypeScript** | SSR/ISR para SEO de catálogo; meta-framework React mais pedido |
| Estilo | **Tailwind CSS** | Produtividade e identidade visual própria sem framework de componentes pesado |
| Validação | FluentValidation (API) e Zod (front) | Contratos explícitos dos dois lados |
| Auth | **ASP.NET Core Identity** + JWT/cookie | Sem dependência de provedor externo no MVP |
| Testes | xUnit + **Testcontainers** (integração com Postgres/Redis reais) e Vitest no front | Testa contra infraestrutura de verdade |
| Infra local | **Docker Compose** | Sobe tudo com um comando |
| CI | **GitHub Actions** | Build, testes e lint a cada PR |
| Deploy MVP | Container único da API + Next.js em plataforma gerenciada (ex.: Fly.io, Railway, Render ou Vercel para o front) | Barato; a infraestrutura como código fica para a R7 |

### Estrutura do repositório (monorepo)

```
/
├── apps/
│   ├── api/                 # ASP.NET Core (host do monólito)
│   └── web/                 # Next.js storefront
├── src/modules/
│   ├── Catalog/             # produtos, SKUs, categorias, coleções
│   ├── Inventory/           # estoque e reservas
│   ├── Cart/                # carrinho (Redis)
│   ├── Orders/              # pedidos, checkout, máquina de estados
│   ├── Payments/            # gateway mock + webhooks
│   └── Identity/            # clientes e autenticação
├── src/shared/              # building blocks (Result, eventos de domínio, etc.)
├── tests/
├── docs/
│   ├── adr/
│   └── architecture.md      # diagramas C4 (contexto e contêineres)
├── docker-compose.yml
└── stack.md
```

Regra de dependência: um módulo expõe apenas um contrato público (interfaces/DTOs). A comunicação entre módulos no MVP é por chamada in-process via contrato; na R3 passa a ser por eventos.

---

## 3. Releases

### Visão geral

| Release | Nome | Escopo | MVP? |
|---|---|---|---|
| R0 | Fundação | Repo, CI, ambiente local, esqueleto | ✅ (concluída) |
| R1 | Vitrine | Catálogo navegável com SSR | ✅ (concluída) |
| R2 | Compra | Conta, carrinho, checkout, pedido | ✅ **(MVP fecha aqui)** (concluída; falta publicar) |
| R3 | Eventos & Busca | Outbox, mensageria, busca facetada | — |
| R4 | Conteúdo & Mídia | CMS headless, storage S3, CDN | — |
| R5 | Integrações | Reviews, trocas, newsletter, cashback | — |
| R6 | Operação | Observabilidade, testes de carga, resiliência | — |
| R7 | Cloud | IaC, ambientes, escalabilidade | — |

---

### R0 — Fundação

**Objetivo:** qualquer pessoa clona o repositório, roda `docker compose up` e vê a API e o front respondendo.

**Escopo**
- Monorepo com `apps/api` e `apps/web` criados
- Docker Compose com Postgres e Redis
- Health check (`/health`) na API, consumido pela home do front
- GitHub Actions: build, testes, lint e format check (API e web)
- Convenções: Conventional Commits, `.editorconfig`, branch `main` protegida
- `docs/architecture.md` com o diagrama C4 nível 1 (contexto)

**ADRs**
- 0001 — Monólito modular em vez de microsserviços
- 0002 — Escolha de PostgreSQL
- 0003 — Next.js com SSR para o storefront

**Pronto quando:** o CI está verde e o README tem instruções de setup em até 5 passos.

---

### R1 — Vitrine (catálogo)

**Objetivo:** navegar pela loja como um cliente anônimo, com páginas indexáveis.

**Modelo de domínio**
- `Product` (produto pai: nome, descrição, slug, marca, coleção)
- `Sku` (variação: cor + tamanho, código, preço, preço promocional)
- `Category` (hierárquica: Masculino › Calças › Jeans)
- `Collection` (agrupamento editorial: "Nova coleção", "Outlet")
- `ProductImage` (por produto e cor)
- `StockLevel` no módulo Inventory (quantidade por SKU)

**Funcionalidades**
- Menu com árvore de categorias
- **PLP** (listagem): filtro por categoria, tamanho, cor e faixa de preço via SQL; ordenação; paginação por cursor
- **PDP** (produto): galeria por cor, seletor de tamanho com disponibilidade, preço "de/por"
- URLs amigáveis (`/masculino/calcas/jeans-slim-azul`)
- SSR na PDP e ISR na PLP, com metadata, Open Graph e `sitemap.xml`
- Seed com cerca de 100 produtos fictícios e imagens de banco gratuito
- Imagens servidas por uma abstração `IFileStorage` (implementação em disco local por enquanto)

**ADRs**
- 0004 — Modelagem produto/SKU e atributos de variação
- 0005 — Paginação por cursor em vez de offset
- 0006 — Estratégia de renderização por página (SSR vs. ISR)

**Pronto quando:** Lighthouse SEO ≥ 90 na PDP e testes de integração cobrem os filtros da PLP.

---

### R2 — Compra (fecha o MVP)

**Objetivo:** um cliente cria conta, monta o carrinho, paga (mock) e acompanha o pedido.

**Identity**
- Cadastro, login e logout; área "Minha conta" com dados e pedidos

**Cart**
- Carrinho em Redis com TTL (ex.: 7 dias), chaveado por um id de visitante em cookie
- Merge do carrinho anônimo com o do usuário no login
- Validação de preço e estoque ao adicionar e ao entrar no checkout

**Inventory**
- **Reserva de estoque** ao iniciar o pagamento, com expiração (ex.: 15 min)
- Controle de concorrência otimista (row version) para evitar overselling

**Orders**
- Máquina de estados: `Created → AwaitingPayment → Paid → Shipped → Delivered`, com `Cancelled` e `PaymentFailed` como saídas
- Snapshot de preço, endereço e itens no pedido (o pedido não depende do catálogo atual)
- Frete com tabela fixa por região (sem integração real)

**Payments**
- `FakePay`: provedor simulado que responde de forma assíncrona via **webhook**
- Webhook com **idempotência** (chave por evento, tabela de eventos processados)
- Cenários simuláveis: aprovado, recusado, timeout e webhook duplicado

**ADRs**
- 0007 — Carrinho em Redis vs. banco relacional
- 0008 — Estratégia de reserva de estoque e concorrência
- 0009 — Idempotência no processamento de webhooks

**Pronto quando:**
- O fluxo completo funciona em produção (deploy público)
- Há um teste automatizado de duas compras simultâneas do último item, com exatamente um sucesso
- Há um teste de webhook duplicado sem efeito colateral
- O README tem GIF ou vídeo curto do fluxo de compra

> 🎯 **MVP = R0 + R1 + R2.** A partir daqui, cada release nasce de uma necessidade identificada.

---

### R3 — Eventos & Busca *(futuro)*

- **Outbox pattern** nos módulos Orders e Catalog
- **RabbitMQ** (ou similar) com MassTransit; os módulos passam a reagir a eventos (`OrderPaid`, `ProductUpdated`, `StockChanged`)
- **Meilisearch** como índice de busca, com facetas, typo tolerance e sinônimos, sincronizado por eventos
- Cache de leitura do catálogo em Redis, com invalidação por evento
- ADRs: consistência eventual na busca; escolha de broker

### R4 — Conteúdo & Mídia *(futuro)*

- CMS headless (Payload ou Strapi) para banners, vitrines da home e páginas de campanha
- Storage S3-compatível (MinIO local, S3/R2 em produção) e CDN com redimensionamento de imagem
- Revalidação on-demand no Next.js quando o conteúdo muda

### R5 — Integrações *(futuro)*

- Serviços externos simulados consumindo eventos: avaliações pós-entrega, portal de trocas e newsletter/CRM
- Cashback como módulo próprio (crédito gerado em `OrderDelivered`)
- Webhooks de saída assinados (HMAC), com retry e dead-letter queue

### R6 — Operação *(futuro)*

- **OpenTelemetry**: traces, métricas e logs estruturados, com Grafana como stack de visualização
- Testes de carga com **k6** simulando pico de Black Friday, com relatório no repositório
- Resiliência com Polly (retry, circuit breaker) nas integrações
- Rate limiting na API

### R7 — Cloud *(futuro)*

- Infraestrutura como código (Terraform ou Pulumi)
- Ambientes de staging e produção, com preview por PR
- Avaliar a extração do primeiro módulo para serviço, se algum ADR justificar

---

## 4. Backlog de ideias (sem release definida)

- Lista de desejos
- Cupons e regras de promoção
- Painel administrativo (catálogo, pedidos, estoque)
- Recomendação "quem viu também viu"
- Multi-idioma e multi-moeda
- PWA

---

## 5. Histórico

| Data | Release | Nota |
|---|---|---|
| 2026-10-06 | — | Documento criado; MVP definido como R0–R2 |
| 2026-10-06 | R0 | Fundação entregue: monorepo, Compose, /health, CI, ADRs 0001–0003 |
| 2026-10-06 | R1 | Vitrine entregue: catálogo com 100 produtos, PLP (ISR + filtros), PDP (SSR), sitemap; Lighthouse SEO 100 na PDP; ADRs 0004–0006 |
| 2026-10-06 | R2 | Compra entregue: Identity, carrinho em Redis, checkout, reserva de estoque, FakePay com webhooks idempotentes, pedidos; ADRs 0007–0010; blueprint de deploy no Render |
