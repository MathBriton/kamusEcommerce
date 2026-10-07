# 0011 — Backoffice como aplicação separada

- **Status:** aceita
- **Data:** 2026-10-07
- **Release:** R2.5

## Contexto

A loja precisava de uma área interna para cadastrar produtos, ajustar estoque, despachar pedidos e
acompanhar números. Loja e backoffice têm públicos e exigências opostos: a loja vive de SEO, ISR e
uma estética espaçosa; o backoffice é autenticado, denso, cheio de tabelas e formulários, e não pode
ser indexado nem cacheado.

## Decisão

- **Front separado** em `apps/admin` (Next.js), com deploy, porta (3001) e container próprios. Usa
  o mesmo padrão de BFF da loja (`/api/*` e `/files/*` repassados à API).
- **Mesma API e mesmo Identity.** Não há API de admin à parte: cada módulo expõe rotas em
  `/api/admin/{área}`, agrupadas pelo helper `MapAdminGroup`, que exige a policy `Admin`
  (papel `Admin` do Identity). O primeiro administrador é criado na inicialização a partir de
  `Admin:Email`/`Admin:Password`.
- **Autorização no servidor.** O layout do painel consulta `/api/identity/me` a cada requisição e
  redireciona quem não é admin; a API recusa com 401/403 de qualquer forma.
- **Design system compartilhado por tokens.** As cores e fontes são as mesmas da loja (copiadas em
  `globals.css`), com componentes próprios de densidade (tabelas, campos compactos, indicadores).
- **Paginação por número de página** nas listas do admin (total e "ir para a página" importam mais
  que o custo do `OFFSET` em milhares de linhas); a vitrine continua com cursor (ADR 0005).
- **Imagens enviadas** são validadas pelo conteúdo (assinatura de JPEG, PNG ou WebP), não pela
  extensão; SVG não é aceito, porque pode carregar script.

## Alternativas consideradas

- **Área `/admin` dentro do storefront** — um deploy a menos, mas mistura o layout estático da loja
  com páginas autenticadas e aumenta o bundle e a superfície de ataque do site público.
- **Painel pronto (ex.: React Admin, AdminJS)** — rápido de montar, mas esconde justamente as
  decisões de UX e de contrato de API que o projeto quer estudar.
- **API de admin separada** — isolamento maior, porém duplica regras de domínio que já vivem nos
  módulos; o papel e o prefixo de rota já dão a separação necessária no MVP.

## Consequências

- ~~Os tokens de design existem em dois arquivos~~ — resolvido na R8: os dois apps importam
  `packages/tokens/theme.css`.
- Mudanças feitas no admin aparecem na PLP em até 5 minutos (ISR, ADR 0006) e na PDP imediatamente
  (SSR). A revalidação sob demanda fica para a R4.
- Mais um container para operar e publicar (incluído no Compose, no CI e no `render.yaml`).
