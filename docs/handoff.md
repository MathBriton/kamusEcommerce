# Handoff — estado atual do trabalho

> Atualize este arquivo ao final de cada sessão (Claude Code, Codex ou humano). Mantenha curto:
> é um bilhete para quem continua, não um histórico completo (o histórico está no `git log` e no
> `stack.md`).

**Última atualização:** 2026-10-07 · Claude Code

## Onde estamos

- MVP concluído: **R0** (fundação), **R1** (vitrine), **R2** (compra) e **R2.5** (backoffice).
- `main` atualizada e com CI verde (API, Web, Admin e imagens Docker).
- Deploy público **adiado por decisão do responsável**; `render.yaml` e `docs/deploy.md` estão prontos.
- Roteiro reordenado em `stack.md`: próxima release é a **R8 — Qualidade contínua** (proposta).

## Próximo passo sugerido: R8 — Qualidade contínua

1. Playwright E2E no CI: fluxo de compra (vitrine → sacola → cadastro → checkout → pedido pago) e
   backoffice (login → criar produto → SKUs → publicar → despachar pedido).
2. Regressão visual: o mesmo roteiro gera as capturas de `apps/web/design` e `apps/admin/design`.
3. Dependabot + CodeQL.
4. Pacote compartilhado de tokens (`packages/ui`) para loja e admin.
5. `.devcontainer` para GitHub Codespaces (o responsável não consegue rodar Docker no trabalho).

Escopo completo e ordem das demais releases: `stack.md`, seção 3.

## Decisões em aberto

- Releases **R8 a R11** estão como *proposta*: confirmar escopo antes de implementar.
- Quando publicar: Render para tudo (blueprint pronto, recomendado) ou híbrido, com os fronts na
  Vercel e API + Postgres + Redis no Render. O híbrido exige ler `VERCEL_PROJECT_PRODUCTION_URL`
  em `apps/web/src/lib/env.ts` e um blueprint só com a API.

## Contexto útil

- Os testes de integração precisam de Docker (Testcontainers); sem Docker, rode só unitários e arquitetura:
  `dotnet test --project tests/Kamus.UnitTests` e `dotnet test --project tests/Kamus.ArchitectureTests`.
- Imagens do design system: `apps/web/design/README.md` (vitrine) e `apps/admin/design/README.md`
  (backoffice). Para recapturar, suba a stack, gere dados de demonstração e use Playwright em 2x.
- O responsável prefere respostas e documentação em português, com explicações de "por quê".
