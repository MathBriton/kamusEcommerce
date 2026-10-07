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
    equipe["🧑‍💼 Equipe da loja"]

    subgraph kamus["Kamus"]
        web["Storefront<br/><small>Next.js · SSR/ISR · BFF</small>"]
        admin["Backoffice<br/><small>Next.js · autenticado · BFF</small>"]
        api["API<br/><small>ASP.NET Core · monólito modular</small>"]
        pg[("PostgreSQL<br/><small>um schema por módulo</small>")]
        redis[("Redis<br/><small>carrinho, cache</small>")]
        files[("Arquivos<br/><small>imagens (disco local → S3 na R4)</small>")]
    end

    fakepay["💳 FakePay"]

    cliente -- HTTPS --> web
    equipe -- HTTPS --> admin
    web -- "HTTP/JSON (API_URL)" --> api
    admin -- "/api/admin/* (papel Admin)" --> api
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
| Identity | Clientes, papéis (Admin) e autenticação | `identity` (Postgres) |
| Reporting | Relatórios do backoffice, compostos via contratos | sem tabelas (read models na R3) |

Dependências entre módulos (sempre via `*.Contracts`):

```mermaid
flowchart LR
    Orders --> Cart & Catalog & Inventory & Payments
    Cart --> Catalog & Inventory
    Cart -. "evento CustomerSignedIn" .-> Identity
    Catalog --> Inventory
    Orders -. "eventos PaymentApproved/Declined" .-> Payments
    Orders -. "evento ReservationExpired" .-> Inventory
    Orders --> Identity
    Reporting --> Orders & Inventory & Catalog
```

Setas cheias são chamadas a contratos; tracejadas são eventos in-process que o módulo da esquerda
consome (publicados pelo módulo da direita).

Regras (ver [ADR 0001](adr/0001-monolito-modular.md)):

- um módulo expõe apenas `Kamus.<Modulo>.Contracts`;
- nenhum módulo acessa as tabelas de outro;
- a comunicação é in-process via contratos no MVP e passa a ser por eventos na R3.

## Fluxo de compra (R2)

```mermaid
sequenceDiagram
    autonumber
    actor C as Cliente
    participant W as Next.js (BFF)
    participant O as Orders
    participant I as Inventory
    participant P as Payments
    participant F as FakePay

    C->>W: Pagar (endereço, cartão, total esperado)
    W->>O: POST /api/orders (cookie de sessão)
    O->>O: revalida carrinho, preço e frete
    O->>I: reservar itens por 15 min (tudo ou nada)
    I-->>O: reservado (ou 409 sem estoque)
    O->>O: pedido Created → AwaitingPayment
    O->>P: iniciar pagamento
    P->>F: criar cobrança
    O-->>C: 201 pedido KM10001
    F--)P: webhook assinado charge.succeeded
    P->>P: grava evento (idempotente) + Approved
    P--)O: PaymentApproved
    O->>I: confirmar reserva (baixa o estoque)
    O->>O: AwaitingPayment → Paid
```

Saídas alternativas: `charge.failed` → reserva liberada e pedido `PaymentFailed`; sem resposta em
15 minutos → reserva expirada e pedido `Cancelled`. Decisões em
[ADR 0007](adr/0007-carrinho-em-redis.md), [0008](adr/0008-reserva-de-estoque-e-concorrencia.md),
[0009](adr/0009-idempotencia-de-webhooks.md) e [0010](adr/0010-autenticacao-com-cookie.md).

## Máquina de estados do pedido

```mermaid
stateDiagram-v2
    [*] --> Created
    Created --> AwaitingPayment: reserva feita
    Created --> Cancelled
    AwaitingPayment --> Paid: pagamento aprovado
    AwaitingPayment --> PaymentFailed: pagamento recusado
    AwaitingPayment --> Cancelled: reserva expirou / cliente cancelou
    Paid --> Shipped
    Shipped --> Delivered
    Delivered --> [*]
    Cancelled --> [*]
    PaymentFailed --> [*]
```
