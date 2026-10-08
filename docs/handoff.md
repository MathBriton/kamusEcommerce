# Handoff — estado atual do trabalho

> Atualize este arquivo ao final de cada sessão (Claude Code, Codex ou humano). Mantenha curto:
> é um bilhete para quem continua, não um histórico completo (o histórico está no `git log` e no
> `stack.md`).

**Última atualização:** 2026-10-08 · Claude Code

## Onde estamos

- Concluídas: **R0, R1, R2** (MVP), **R2.5** (backoffice) e **R8** (qualidade contínua).
- `main` é a branch padrão do GitHub. CI: API, Web, Admin, Docker images, **E2E e regressão
  visual**; CodeQL e Dependabot ativos.
- Deploy público **adiado por decisão do responsável** (`render.yaml` e `docs/deploy.md` prontos).

## Em andamento: R12 — Auditoria & Exclusão segura (branch `claude/festive-pasteur-l8jhiz`)

Telas aprovadas pelo responsável num canvas (Atividade, Lixeira, aba Histórico do produto, ator no
histórico do pedido, diálogo de exclusão). Decisões: retenção da auditoria 2 anos (mín. 365 dias),
expurgo da lixeira 30 dias, ações automáticas como ator "Sistema" (ex.: FakePay), excluir produto
leva SKUs e imagens junto, "Excluir de vez" para Admin, pedidos nunca são excluídos.

**Já na branch:**
- ADRs [0014](adr/0014-auditoria-por-interceptor-e-modulo-audit.md) e
  [0015](adr/0015-exclusao-reversivel-com-lixeira.md); `docs/architecture.md`, `AGENTS.md`, README.
- Shared (`Kamus.Shared.Auditing`): `ICurrentActor`, `AuditPolicyBuilder` (allowlist por módulo),
  `SoftDeleteInterceptor`, `AuditingInterceptor` (grava na mesma transação), `IFileStorage.DeleteAsync`;
  claim `kamus:full_name` no Identity.
- Módulo **Audit** (schema `audit`, triggers de somente inclusão, retenção, endpoints
  `/api/admin/audit/*` com CSV), registrado no host; testes unitários, de arquitetura e integração.
- Contrato do Catalog: `DescribeSkusAsync`, `SkuDescription`, evento `SkusPurged`.
- Backoffice completo contra o contrato (Atividade, Lixeira, Histórico, exclusões, ator no pedido).
- E2E novos em `e2e/tests/backoffice.spec.ts` (capturas 07 a 10 ainda **não geradas**).

**Pela metade (worktrees locais, branches `r12-catalog` e `r12-orders`, ainda não mergeadas):**
- Catalog: soft delete em produto/SKU/imagem, filtros e índices parciais, política de auditoria,
  endpoints de exclusão, lixeira, restauração, expurgo e `TrashPurgeWorker`.
- Orders/Inventory/Payments/Reporting: ator no histórico do pedido (migration), políticas de
  auditoria de pedido e estoque, `ActAs(System("FakePay"))`, handler de `SkusPurged`.
- Se essas worktrees se perderem (o container é efêmero), refazer a partir do escopo da R12 no
  `stack.md`, dos ADRs 0014/0015 e da API já pronta no Shared e no front (o front mostra o contrato
  JSON esperado em `apps/admin/src/lib/schemas.ts`).

**Falta depois do merge:** checagens completas, `./e2e/run.sh --update` (todas as capturas do admin
mudam: menu ganhou a seção Controle), revisão das imagens, revisão de código, `stack.md` (R12 ✅) e
este handoff.

## Decisões em aberto

- **Hospedagem (pausa do responsável em 2026-10-07):** ele prefere uma **VPS** para o backend, pelo
  valor de portfólio. Sugestão apresentada: Hetzner (4 GB+ de RAM), Docker Compose com as imagens
  publicadas no GHCR pelo CI, Caddy como proxy reverso com HTTPS automático, deploy por SSH a partir
  do GitHub Actions, backup diário do Postgres e firewall/SSH endurecidos. Front pode ficar na mesma
  VPS ou na Vercel. Aguardando escolha do provedor e do domínio; quando confirmado, vira escopo da R7
  (ou uma release de deploy antes dela).

## Contexto útil

- Os testes de integração precisam de Docker (Testcontainers); sem Docker, rode só unitários e arquitetura:
  `dotnet test --project tests/Kamus.UnitTests` e `dotnet test --project tests/Kamus.ArchitectureTests`.
- Imagens do design system: geradas pelos testes E2E (`./e2e/run.sh --update`), nunca à mão.
- Sem Docker local? GitHub Codespaces (`.devcontainer/`) sobe tudo no navegador.
- O responsável prefere respostas e documentação em português, com explicações de "por quê".
