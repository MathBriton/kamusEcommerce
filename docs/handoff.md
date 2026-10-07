# Handoff — estado atual do trabalho

> Atualize este arquivo ao final de cada sessão (Claude Code, Codex ou humano). Mantenha curto:
> é um bilhete para quem continua, não um histórico completo (o histórico está no `git log` e no
> `stack.md`).

**Última atualização:** 2026-10-07 · Claude Code

## Onde estamos

- Concluídas: **R0, R1, R2** (MVP), **R2.5** (backoffice) e **R8** (qualidade contínua).
- `main` é a branch padrão do GitHub. CI: API, Web, Admin, Docker images, **E2E e regressão
  visual**; CodeQL e Dependabot ativos.
- Deploy público **adiado por decisão do responsável** (`render.yaml` e `docs/deploy.md` prontos).

## Próximo passo: R12 — Auditoria & Exclusão segura

Escopo em `stack.md`. Sugestão de execução:

1. ADR: auditoria por interceptor do EF Core + módulo **Audit** (schema próprio, contrato
   `IAuditLog`, tabela append-only) vs. triggers no banco. Recomendado: interceptor.
2. Interceptor compartilhado em `Kamus.Shared` que captura quem/o quê/quando/antes→depois a partir do
   `HttpContext` (usuário) e do ChangeTracker; cada módulo o registra no próprio DbContext.
3. Soft delete: `DeletedAt`/`DeletedBy`, filtro global (`HasQueryFilter`), índices únicos parciais
   (`WHERE deleted_at IS NULL`) — começando por produtos, SKUs e imagens.
4. Backoffice: tela **Atividade**, aba **Histórico** em produto e pedido, **Lixeira** (restaurar).
5. Testes de integração + cenários E2E novos (com capturas em `apps/admin/design`).

## Decisões em aberto

- Retenção da auditoria (sugestão: 2 anos) e do expurgo da lixeira (sugestão: 30 dias).
- Quando publicar: Render para tudo (recomendado) ou híbrido (fronts na Vercel).

## Contexto útil

- Os testes de integração precisam de Docker (Testcontainers); sem Docker, rode só unitários e arquitetura:
  `dotnet test --project tests/Kamus.UnitTests` e `dotnet test --project tests/Kamus.ArchitectureTests`.
- Imagens do design system: geradas pelos testes E2E (`./e2e/run.sh --update`), nunca à mão.
- Sem Docker local? GitHub Codespaces (`.devcontainer/`) sobe tudo no navegador.
- O responsável prefere respostas e documentação em português, com explicações de "por quê".
