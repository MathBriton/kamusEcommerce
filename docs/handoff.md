# Handoff — estado atual do trabalho

> Atualize este arquivo ao final de cada sessão (Claude Code, Codex ou humano). Mantenha curto:
> é um bilhete para quem continua, não um histórico completo (o histórico está no `git log` e no
> `stack.md`).

**Última atualização:** 2026-10-09 · Claude Code

## Onde estamos

- Concluídas: **R0, R1, R2** (MVP), **R2.5** (backoffice), **R8** (qualidade contínua) e **R12**
  (auditoria e exclusão segura).
- `main` é a branch padrão do GitHub. CI: API, Web, Admin, Docker images, **E2E e regressão
  visual**; CodeQL e Dependabot ativos.
- A R12 foi feita na branch `claude/festive-pasteur-l8jhiz` e **ainda não foi para a `main`**
  (aguardando o responsável pedir o merge/push).
- Deploy público **adiado por decisão do responsável** (`render.yaml` e `docs/deploy.md` prontos).

## R12 em uma frase por peça

- `Kamus.Shared.Auditing`: `ICurrentActor`, política por módulo (allowlist de campos),
  `SoftDeleteInterceptor` e `AuditingInterceptor` (mesma transação da alteração).
- Módulo **Audit** (schema `audit`): tabela somente inclusão (triggers), retenção de 2 anos,
  `/api/admin/audit/*` com CSV.
- Catalog: soft delete + lixeira + expurgo (30 dias) + `xmin` em produto/SKU/imagem; Orders: ator no
  histórico; Inventory: auditoria do estoque e handler de `SkusPurged`.
- Backoffice: Atividade, Lixeira, aba Histórico, exclusão com confirmação, ator no pedido.
- Limitações conhecidas (IP atrás de proxy de borda, dono do banco, dados pessoais na trilha,
  `SkusPurged` sem outbox): ver consequências dos ADRs 0014 e 0015.

## Próximo passo: R3 — Eventos & Busca

Escopo em `stack.md`. Sugestão de execução:

1. ADR do broker (RabbitMQ + MassTransit) e da consistência eventual na busca.
2. Outbox no Orders e no Catalog (substitui o "outbox simplificado" do Payments, ADR 0009, e resolve
   o `SkusPurged` sem garantia de entrega da R12).
3. Meilisearch sincronizado por eventos; busca na vitrine (caixa no cabeçalho + página de resultados).
4. Reporting com read models próprios.
5. E2E da busca com capturas novas em `apps/web/design`.

## Decisões em aberto

- **Hospedagem (pausa do responsável em 2026-10-07):** ele prefere uma **VPS** para o backend, pelo
  valor de portfólio. Sugestão apresentada: Hetzner (4 GB+ de RAM), Docker Compose com as imagens
  publicadas no GHCR pelo CI, Caddy como proxy reverso com HTTPS automático, deploy por SSH a partir
  do GitHub Actions, backup diário do Postgres e firewall/SSH endurecidos. Front pode ficar na mesma
  VPS ou na Vercel. Aguardando escolha do provedor e do domínio; quando confirmado, vira escopo da R7
  (ou uma release de deploy antes dela).
- **Publicação para entrevista (2026-10-08):** o responsável quer aprender instalando um **Ubuntu
  Server num notebook parado**, com Docker Compose de produção e Cloudflare Tunnel. Proposta de
  guia passo a passo (`docs/deploy-notebook.md`, `docker-compose.prod.yml`) ainda não começada.
  Requisitos vindos da R12 para qualquer deploy: o proxy de borda precisa sobrescrever
  `X-Forwarded-For`, e migrations e aplicação devem usar papéis diferentes no Postgres.

## Contexto útil

- Os testes de integração precisam de Docker (Testcontainers); sem Docker, rode só unitários e arquitetura:
  `dotnet test --project tests/Kamus.UnitTests` e `dotnet test --project tests/Kamus.ArchitectureTests`.
- Imagens do design system: geradas pelos testes E2E (`./e2e/run.sh --update`), nunca à mão.
- Sem Docker local? GitHub Codespaces (`.devcontainer/`) sobe tudo no navegador.
- O responsável prefere respostas e documentação em português, com explicações de "por quê".
