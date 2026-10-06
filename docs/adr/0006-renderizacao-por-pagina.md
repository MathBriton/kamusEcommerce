# 0006 — Estratégia de renderização por página (SSR vs. ISR)

- **Status:** aceita
- **Data:** 2026-10-06
- **Release:** R1

## Contexto

O storefront precisa de páginas indexáveis e rápidas, mas os dados têm volatilidades diferentes:
categorias e listagens mudam pouco; a disponibilidade por tamanho na PDP muda a cada venda. As URLs
públicas não têm prefixo (`/masculino/calcas` é PLP, `/masculino/calcas/jeans-slim` é PDP), então
uma única rota dinâmica não permitiria estratégias diferentes.

## Decisão

| Página | Estratégia | Motivo |
|---|---|---|
| PLP (`/categoria/...`, `/colecoes/...`) | **ISR** (`revalidate = 300`), pré-gerada no build quando a API está disponível | Muda pouco; HTML estático servido do cache |
| Filtros, ordenação, "carregar mais" | Client-side, via BFF (`/api/*` do Next) | Não fragmenta o cache ISR por combinação de filtros; estado na URL |
| PDP (`/produto/...`) | **SSR** (`dynamic = "force-dynamic"`) | Estoque e preço sempre atuais |
| Home | SSR com dados em cache (300s) | Evita congelar uma home vazia se o build rodar sem API |
| `sitemap.xml` | Sob demanda, dados em cache por 1h | Mesmo motivo |

O **`proxy.ts`** do Next.js decide o destino de cada URL sem prefixo: mantém em memória (TTL de
60s) o conjunto de caminhos de categoria e faz *rewrite* para `/categoria/...` ou
`/produto/...`. As rotas internas usam `canonical` para a URL pública e estão em `Disallow` no
`robots.txt`.

O navegador conversa só com o Next.js (mesma origem); o Next encaminha `/api/*` e `/files/*` para
a API .NET (_backend for frontend_).

## Alternativas consideradas

- **Prefixos explícitos (`/c/...`, `/p/...`)** — dispensa o proxy, mas gera URLs menos amigáveis.
- **Tudo SSR com cache de dados** — mais simples, porém sem HTML estático na PLP.
- **Filtros renderizados no servidor via `searchParams`** — tornaria toda a PLP dinâmica.
- **PDP em ISR + disponibilidade no cliente** — mais rápido, mas o estoque na primeira pintura
  poderia estar desatualizado; fica como opção se o SSR da PDP virar gargalo (R6).

## Consequências

- O proxy depende da API para conhecer as categorias; com a API fora, usa o último valor
  conhecido e, sem ele, deixa a requisição seguir (404).
- Uma categoria nova aparece em até 60s no proxy e 300s na PLP; na R4 a revalidação passa a ser
  sob demanda (`revalidateTag("catalog")`).
- Lighthouse na PDP (R1): SEO 100, Performance 100, Acessibilidade 100, Boas práticas 100.
