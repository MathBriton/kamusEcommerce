# Stack & Roadmap de Releases

> E-commerce de moda fictício para estudo de arquitetura e system design.
> Domínio inspirado em lojas de moda brasileiras; marca, identidade visual e conteúdo são próprios.

**Codinome do projeto:** `Kamus`
**Status:** R8 concluída; próxima: R12 (Auditoria & Exclusão segura). Deploy público adiado por decisão.
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

Os números identificam cada release (e são citados nos ADRs); a coluna **Ordem** define a sequência
de execução. Releases *planejadas* tiveram o escopo aprovado e ainda não começaram.

| Ordem | Release | Nome | Escopo | Situação |
|---|---|---|---|---|
| 1 | R0 | Fundação | Repo, CI, ambiente local, esqueleto | ✅ concluída |
| 2 | R1 | Vitrine | Catálogo navegável com SSR | ✅ concluída |
| 3 | R2 | Compra | Conta, carrinho, checkout, pedido | ✅ concluída **(MVP)**; deploy público adiado |
| 4 | R2.5 | Backoffice | Admin de catálogo, estoque e pedidos; relatórios | ✅ concluída |
| 5 | R8 | Qualidade contínua | Testes E2E no CI, regressão visual, dependências | ✅ concluída |
| 6 | R12 | Auditoria & Exclusão segura | Log de auditoria, soft delete, lixeira no backoffice | *planejada* · próxima |
| 7 | R3 | Eventos & Busca | Outbox, mensageria, busca facetada, read models | — |
| 8 | R6 | Operação | Observabilidade, testes de carga, resiliência | — |
| 9 | R9 | Promoções | Cupons, regras de preço, campanhas | *planejada* |
| 10 | R10 | Segurança & LGPD | Direitos do titular, 2FA, papéis, CSP | *planejada* |
| 11 | R4 | Conteúdo & Mídia | CMS headless, storage S3, CDN | — |
| 12 | R5 | Integrações | Reviews, trocas, e-mails, cashback | — |
| 13 | R11 | Backoffice 2 | Categorias, estorno, import/export | *planejada* |
| 14 | R7 | Cloud | IaC, ambientes, escalabilidade | — |

**Por que essa ordem:** R8 é pequena e protege tudo o que vem depois; R12 vem cedo para que
toda funcionalidade nova (cupons, categorias, estornos) já nasça auditada e com exclusão reversível;
R3 é a base de busca,
relatórios e integrações; R6 vem antes de novas funcionalidades para que o sistema seja medido
antes de crescer (e é muito valorizada em vagas de backend); R9 e R10 são domínio rico e
realidade brasileira; R4, R5 e R11 ampliam o produto; R7 fecha com a infraestrutura.

---|---|---|---|
| R0 | Fundação | Repo, CI, ambiente local, esqueleto | ✅ (concluída) |
| R1 | Vitrine | Catálogo navegável com SSR | ✅ (concluída) |
| R2 | Compra | Conta, carrinho, checkout, pedido | ✅ **(MVP fecha aqui)** (concluída; falta publicar) |
| R2.5 | Backoffice | Admin de catálogo, estoque e pedidos; relatórios | ✅ (concluída) |
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

### R2.5 — Backoffice

**Objetivo:** a operação da loja deixa de depender de seed e de botões de simulação: uma equipe
interna cadastra produtos, ajusta estoque, despacha pedidos e acompanha os números.

**Acesso**
- Papel `Admin` no Identity; endpoints em `/api/admin/*` exigem a policy `Admin`
- Usuário administrador criado na inicialização a partir de configuração (`Admin:Email`, `Admin:Password`)

**Catálogo e estoque**
- Lista de produtos com busca (inclui inativos), criação e edição de produto
- SKUs: adicionar cor/tamanho, alterar preço e preço promocional
- Upload de imagens por cor (JPEG, PNG ou WebP, validados pelo conteúdo) e remoção
- Ativar/desativar produto; ajuste de estoque físico por SKU, com reservado e disponível visíveis

**Pedidos**
- Lista com filtro por status, detalhe, despacho com código de rastreio, confirmação de entrega e cancelamento

**Relatórios** (módulo `Reporting`, que compõe dados via contratos dos outros módulos)
- Faturamento, pedidos, ticket médio e unidades no período; faturamento por dia
- Pedidos por status, taxa de pagamento recusado e de cancelamento
- Produtos mais vendidos e itens com estoque baixo

**Front:** app separado `apps/admin` (Next.js), com o mesmo design system em versão densa.

**ADRs**
- 0011 — Backoffice como aplicação separada
- 0012 — Relatórios compostos por contratos (e evolução para read models na R3)

**Pronto quando:** um produto criado no admin aparece na loja, um pedido pago é despachado com
rastreio pelo admin e os relatórios batem com os pedidos dos testes de integração.

### R3 — Eventos & Busca *(futuro)*

- **Outbox pattern** nos módulos Orders e Catalog
- **RabbitMQ** (ou similar) com MassTransit; os módulos passam a reagir a eventos (`OrderPaid`, `ProductUpdated`, `StockChanged`)
- **Meilisearch** como índice de busca, com facetas, typo tolerance e sinônimos, sincronizado por eventos
- Cache de leitura do catálogo em Redis, com invalidação por evento
- **Reporting com read models** próprios (vendas por dia, mais vendidos), atualizados por eventos (ADR 0012)
- O reenvio de avisos de pagamento vira um **outbox de verdade** (ADR 0009)
- Busca na vitrine (caixa de busca no cabeçalho e página de resultados)
- ADRs: consistência eventual na busca; escolha de broker

### R4 — Conteúdo & Mídia *(futuro)*

- CMS headless (Payload ou Strapi) para banners, vitrines da home e páginas de campanha
- Storage S3-compatível (MinIO local, S3/R2 em produção) e CDN com redimensionamento de imagem
- Revalidação on-demand no Next.js quando o conteúdo muda; produtos alterados no backoffice
  aparecem na listagem na hora, em vez de até 5 minutos (ADR 0011)
- Upload de imagens do backoffice direto para o storage, com variações de tamanho geradas na CDN

### R5 — Integrações *(futuro)*

- Serviços externos simulados consumindo eventos: avaliações pós-entrega, portal de trocas e newsletter/CRM
- Cashback como módulo próprio (crédito gerado em `OrderDelivered`)
- E-mails transacionais disparados por eventos (pedido confirmado, enviado, entregue), com Mailpit
  no ambiente local
- Webhooks de saída assinados (HMAC), com retry e dead-letter queue

### R6 — Operação *(futuro)*

- **OpenTelemetry**: traces, métricas e logs estruturados, com Grafana como stack de visualização
- Testes de carga com **k6** simulando pico de Black Friday, com relatório no repositório
- Resiliência com Polly (retry, circuit breaker) nas integrações
- Rate limiting na API
- Medir a disputa por estoque do mesmo SKU em pico e o número de retentativas de concorrência (ADR 0008)
- Dashboards técnicos (latência por endpoint, fila, erros) ao lado do painel de negócio do backoffice

### R7 — Cloud *(futuro)*

- Infraestrutura como código (Terraform ou Pulumi)
- Ambientes de staging e produção, com preview por PR
- Avaliar a extração do primeiro módulo para serviço, se algum ADR justificar

### R8 — Qualidade contínua ✅

- **Testes E2E com Playwright no CI**: fluxo de compra completo (vitrine → pagamento) e fluxo do
  backoffice (criar, publicar, despachar)
- **Regressão visual**: o mesmo roteiro gera as imagens de `apps/web/design` e `apps/admin/design`
  e falha o CI se uma tela mudar sem querer (as imagens do design system passam a se atualizar sozinhas)
- Dependabot e CodeQL; pacote compartilhado de tokens de design (`packages/tokens`) entre loja e admin
- Codespaces/devcontainer para rodar tudo no navegador, sem Docker local
- ADR 0013 — Testes E2E e regressão visual com as imagens do design system

**Entregue:** 23 testes E2E (vitrine, compra, backoffice) comparando 28 telas com tolerância de 50
pixels, estáveis em execuções repetidas; seed com ids determinísticos; página `/design-system`.

### R9 — Promoções *(planejada)*

- Cupons (percentual, valor fixo, frete grátis) com validade, valor mínimo e limite de usos
- Regras de preço por coleção ou categoria ("20% na coleção Outlet")
- Preço final calculado no checkout com explicação dos descontos aplicados; snapshot no pedido
- Desafio de system design: **cupom com limite de usos sob concorrência** (mesmo problema do último
  item em estoque, agora com contador)
- Gestão de cupons no backoffice e impacto das promoções nos relatórios

### R10 — Segurança & LGPD *(planejada)*

- Direitos do titular: exportar meus dados, excluir conta por **anonimização** (não soft delete:
  manter dados pessoais "escondidos" não atende o direito de eliminação), registro de consentimento
- 2FA para administradores e papéis granulares (atendimento, estoque, financeiro), aproveitando a
  auditoria da R12 para registrar acessos sensíveis
- Cabeçalhos de segurança e Content Security Policy na loja e no admin; revisão OWASP Top 10

### R12 — Auditoria & Exclusão segura *(planejada · próxima)*

**Objetivo:** saber sempre quem fez o quê, quando e qual era o valor anterior, e nunca perder um
dado por um clique errado no backoffice.

**Log de auditoria**
- Cada alteração feita por um usuário (criar, editar, publicar, ajustar estoque, despachar,
  cancelar, excluir, restaurar) gera um registro: **quem** (id e e-mail), **o quê** (módulo,
  entidade, id, ação), **quando**, **antes → depois** (somente os campos alterados) e contexto da
  requisição (IP, correlation id)
- Captura automática por **interceptor do EF Core** em cada módulo; os registros vão para o módulo
  **Audit** (schema próprio) pelo contrato `IAuditLog`, respeitando as fronteiras
- **Somente inclusão (append-only)**: o usuário do banco da aplicação não tem permissão de
  `UPDATE`/`DELETE` na tabela de auditoria
- Backoffice: tela **Atividade** (filtros por usuário, módulo, período) e aba **Histórico** no
  produto e no pedido
- Política de retenção configurável (ex.: 2 anos) e exportação em CSV

**Soft delete (exclusão reversível)**
- Entidades excluíveis ganham `DeletedAt` e `DeletedBy`; **filtro global** do EF Core esconde os
  excluídos de toda consulta; o interceptor transforma `Remove()` em marcação
- Aplica-se a produtos, SKUs, imagens e, na R11, categorias, coleções e cupons
- **Índices únicos parciais** (`WHERE deleted_at IS NULL`): o slug de um produto excluído pode ser
  reutilizado
- Backoffice: **Lixeira** com restaurar e excluir definitivamente (só para quem tiver permissão),
  e expurgo automático após N dias
- Pedidos **não** são excluídos (só cancelados) e dados pessoais seguem a regra de anonimização da R10

**ADRs**
- Auditoria por interceptor + módulo Audit (vs. triggers no banco vs. event sourcing)
- Soft delete com filtro global e índices parciais; o que nunca é excluído

**Pronto quando:** toda ação do backoffice aparece na tela Atividade com o valor anterior; um
produto excluído some da loja e do admin, aparece na Lixeira e volta intacto ao ser restaurado.

### R11 — Backoffice 2 *(planejada)*

- Gestão de categorias e coleções; reordenação de imagens
- Estorno de pagamento pelo admin (novo evento do FakePay: `charge.refunded`)
- Importação de produtos por CSV e exportação de pedidos e relatórios
- Responsivo para uso no celular (consulta rápida de pedidos)

---

## 4. Backlog de ideias (sem release definida)

- Lista de desejos
- Recomendação "quem viu também viu"
- Multi-idioma e multi-moeda
- PWA
- Recomendações por IA (descrição de produto gerada no backoffice, busca semântica)
- Programa de fidelidade além do cashback

---

## 5. Histórico

| Data | Release | Nota |
|---|---|---|
| 2026-10-06 | — | Documento criado; MVP definido como R0–R2 |
| 2026-10-06 | R0 | Fundação entregue: monorepo, Compose, /health, CI, ADRs 0001–0003 |
| 2026-10-06 | R1 | Vitrine entregue: catálogo com 100 produtos, PLP (ISR + filtros), PDP (SSR), sitemap; Lighthouse SEO 100 na PDP; ADRs 0004–0006 |
| 2026-10-06 | R2 | Compra entregue: Identity, carrinho em Redis, checkout, reserva de estoque, FakePay com webhooks idempotentes, pedidos; ADRs 0007–0010; blueprint de deploy no Render |
| 2026-10-07 | R2.5 | Backoffice entregue: papel Admin, app `apps/admin`, CRUD de catálogo com upload, estoque, operação de pedidos e módulo Reporting; ADRs 0011–0012 |
| 2026-10-07 | — | Roteiro reordenado (R3 → R6 antes de R4/R5); pendências dos ADRs incorporadas; propostas R8–R11 |
| 2026-10-07 | — | Propostas R8–R11 aprovadas; nova R12 (Auditoria & Exclusão segura), logo após a R8 |
| 2026-10-07 | R8 | Qualidade contínua: E2E + regressão visual (Playwright em container), `packages/tokens`, página `/design-system`, Dependabot, CodeQL, devcontainer; ADR 0013 |
