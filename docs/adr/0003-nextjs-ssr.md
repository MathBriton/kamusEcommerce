# 0003 — Next.js com SSR para o storefront

- **Status:** aceita
- **Data:** 2026-10-06
- **Release:** R0

## Contexto

A vitrine de um e-commerce de moda depende de tráfego orgânico: as páginas de listagem e de produto
precisam ser indexáveis, rápidas no primeiro carregamento e ter metadata/Open Graph corretos. O
front também precisa de áreas interativas (carrinho, checkout, minha conta).

## Decisão

Usar **Next.js (App Router) com TypeScript** em `apps/web`, com Tailwind CSS para estilo e Zod para
validar as respostas da API. Páginas de catálogo são renderizadas no servidor (SSR/ISR); partes
interativas usam Client Components. O Next.js consome a API .NET pelo servidor (`API_URL`), sem
acessar banco diretamente.

## Alternativas consideradas

- **SPA (React + Vite)** — ótima DX, mas SEO e tempo de primeira renderização piores para catálogo.
- **Razor Pages/Blazor no próprio .NET** — elimina um runtime, mas é menos comum em vagas de front e
  o ecossistema de UI para e-commerce é menor.
- **Remix/Astro** — boas opções técnicas, com menor demanda de mercado.

## Consequências

- Dois runtimes (Node e .NET) para operar; mitigado com Docker Compose e containers separados.
- A estratégia de renderização por página (SSR vs. ISR) precisa ser decidida explicitamente
  (ADR 0006, R1).
- O Next.js funciona como _backend for frontend_: cookies de sessão e chamadas à API passam por ele.
