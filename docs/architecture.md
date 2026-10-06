# Arquitetura

Visão da arquitetura do Kamus usando o [modelo C4](https://c4model.com/). Os diagramas usam Mermaid e
são renderizados pelo GitHub.

## Nível 1 — Contexto

Quem usa o sistema e com quais sistemas externos ele conversa.

```mermaid
flowchart TB
    cliente["👤 Cliente<br/><small>Navega no catálogo, compra e acompanha pedidos</small>"]
    kamus["🛍️ Kamus<br/><small>E-commerce de moda: catálogo, carrinho, checkout e pedidos</small>"]
    fakepay["💳 FakePay<br/><small>Gateway de pagamento simulado (externo)</small>"]
    buscadores["🔎 Buscadores<br/><small>Indexam o catálogo (SEO)</small>"]

    cliente -- "Navega e compra (HTTPS)" --> kamus
    buscadores -- "Rastreiam páginas e sitemap.xml" --> kamus
    kamus -- "Cria cobranças" --> fakepay
    fakepay -- "Notifica o resultado via webhook" --> kamus
```

## Nível 2 — Contêineres

```mermaid
flowchart TB
    cliente["👤 Cliente (navegador)"]

    subgraph kamus["Kamus"]
        web["Storefront<br/><small>Next.js · SSR/ISR · BFF</small>"]
        api["API<br/><small>ASP.NET Core · monólito modular</small>"]
        pg[("PostgreSQL<br/><small>um schema por módulo</small>")]
        redis[("Redis<br/><small>carrinho, cache</small>")]
        files[("Arquivos<br/><small>imagens (disco local → S3 na R4)</small>")]
    end

    fakepay["💳 FakePay"]

    cliente -- HTTPS --> web
    web -- "HTTP/JSON (API_URL)" --> api
    api --> pg
    api --> redis
    api --> files
    api -- cobrança --> fakepay
    fakepay -- webhook --> api
```

## Módulos da API

| Módulo | Responsabilidade | Armazenamento |
|---|---|---|
| Catalog | Produtos, SKUs, categorias, coleções, imagens | `catalog` (Postgres) |
| Inventory | Níveis de estoque e reservas | `inventory` (Postgres) |
| Cart | Carrinho de visitantes e clientes | Redis |
| Orders | Checkout, pedidos e máquina de estados | `orders` (Postgres) |
| Payments | Integração com o FakePay e webhooks | `payments` (Postgres) |
| Identity | Clientes e autenticação | `identity` (Postgres) |

Regras (ver [ADR 0001](adr/0001-monolito-modular.md)):

- um módulo expõe apenas `Kamus.<Modulo>.Contracts`;
- nenhum módulo acessa as tabelas de outro;
- a comunicação é in-process via contratos no MVP e passa a ser por eventos na R3.
