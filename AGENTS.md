# AGENTS.md — guia para agentes de código (Codex, Claude Code e outros)

Este arquivo é a fonte única de instruções para qualquer agente que trabalhe neste repositório.
O `CLAUDE.md` apenas importa este arquivo. **Antes de começar, leia também
[`docs/handoff.md`](docs/handoff.md)**: ele diz em que ponto o trabalho parou e qual é o próximo passo.

## O projeto

**Kamus** é um e-commerce de moda fictício para estudo de arquitetura e system design.
Roteiro e releases: [`stack.md`](stack.md). Decisões: [`docs/adr/`](docs/adr). Arquitetura:
[`docs/architecture.md`](docs/architecture.md).

| Pasta | O que é |
|---|---|
| `apps/api` | Host ASP.NET Core (.NET 10) do monólito modular |
| `apps/web` | Loja (Next.js 16, App Router, Tailwind 4) |
| `apps/admin` | Backoffice (Next.js 16), porta 3001 |
| `src/modules/<Modulo>/Kamus.<Modulo>` | Implementação de cada módulo |
| `src/modules/<Modulo>/Kamus.<Modulo>.Contracts` | Contrato público (interfaces, DTOs, eventos) |
| `src/shared/Kamus.Shared` | Building blocks (Result, IModule, eventos in-process, storage, paginação) |
| `tests/` | Unitários, integração (Testcontainers) e arquitetura |
| `apps/web/design`, `apps/admin/design` | Imagens do design system = referências da regressão visual |
| `packages/tokens` | Tokens de design (cores, fontes) compartilhados pelos dois fronts |
| `e2e/` | Testes E2E e de regressão visual (Playwright) |

Módulos: Catalog, Inventory, Cart, Orders, Payments, Identity, Reporting.

## Comandos

```bash
# Tudo via Docker (loja :3000, admin :3001, API :5080)
docker compose up -d --build

# API (.NET 10 SDK). Testes de integração precisam do Docker rodando (Testcontainers)
dotnet format Kamus.slnx --verify-no-changes
dotnet build Kamus.slnx
dotnet test --solution Kamus.slnx

# Fronts (Node 22): rodar dentro de apps/web e de apps/admin
npm ci
npm run format:check && npm run lint && npm run typecheck && npm test && npm run build

# E2E + regressão visual (só precisa de Docker): stack isolada, banco zerado
./e2e/run.sh            # compara as telas com apps/*/design
./e2e/run.sh --update   # mudança visual intencional: reescreve as imagens (revise o diff!)

# Nova migration (exemplo no módulo Orders)
dotnet tool restore
dotnet ef migrations add NomeDaMudanca --project src/modules/Orders/Kamus.Orders \
  --startup-project apps/api --context OrdersDbContext --output-dir Persistence/Migrations
```

Credenciais de desenvolvimento: admin `admin@kamus.dev` / `admin-kamus-123`; Postgres `kamus/kamus`.
Cartões de teste do FakePay: ver README.

**Antes de dar uma tarefa como pronta**, rode as checagens de cada parte que você alterou (as mesmas
do CI em `.github/workflows/ci.yml`). Não suba código com teste falhando.

## Regras de arquitetura (não negociáveis)

1. **Fronteira de módulos.** Um módulo só referencia o projeto `*.Contracts` de outro, nunca a
   implementação, e nunca lê tabelas de outro módulo. `tests/Kamus.ArchitectureTests` quebra o
   build se isso for violado. Comunicação: chamada a contrato ou evento in-process (`IEventPublisher`).
2. **Um schema por módulo** no Postgres, com migrations próprias.
3. **Decisões relevantes viram ADR** em `docs/adr/NNNN-titulo.md` (use `0000-template.md`).
4. **Nada entra antes de ser necessário**: cada release tem escopo em `stack.md`.
5. Rotas de backoffice ficam em `/api/admin/{área}` via `MapAdminGroup` (policy `Admin`).

## Convenções

- **Idioma:** documentação, mensagens ao usuário, nomes de testes e commits em **português**;
  identificadores de código em inglês.
- **Commits:** Conventional Commits (`feat(catalog): ...`, `fix(orders): ...`). Ver `CONTRIBUTING.md`.
- **C#:** warnings são erros; `dotnet format` define o estilo (`.editorconfig`). Entidades com
  construtor privado e métodos de domínio; erros esperados via `Result`/`Error`, não exceções.
- **Front:** valide respostas da API com Zod; o navegador só fala com o próprio Next.js (BFF em
  `/api/*` e `/files/*`). Formatação com Prettier.
- **Imagens de design:** são geradas pelos testes E2E (ADR 0013); não capture à mão. Tela nova ou
  alterada → acrescente/ajuste o teste em `e2e/tests/` e rode `./e2e/run.sh --update`. Mantenha o
  índice no README de `apps/*/design/`.
- **Telas determinísticas:** valores que mudam a cada execução (datas, horas, ids) levam o atributo
  `data-volatile`; ordenações sempre com desempate estável (nunca por id aleatório).
- **Tokens de design** só em `packages/tokens/theme.css` (os dois apps importam esse arquivo).

## Armadilhas conhecidas

- **Next.js 16 mudou APIs.** Leia o guia em `node_modules/next/dist/docs/` antes de escrever código
  de front (ver `apps/*/AGENTS.md`). `middleware` agora é `proxy.ts`; `params` e `searchParams` são Promises.
- **EF Core com ids gerados na aplicação** (Guid v7): configure `ValueGeneratedNever()`, senão uma
  entidade filha nova é tratada como existente (UPDATE em vez de INSERT).
- **Concorrência otimista** usa a coluna `xmin` do Postgres (`IsRowVersion()` em `uint Version`);
  use `ConcurrencyRetry.ExecuteAsync` em operações disputadas.
- **Testes:** xUnit v3 sobre Microsoft.Testing.Platform (`dotnet test --solution ...`). O host de
  testes sobe uma vez por assembly (`KamusApiFactory`); testes de checkout e relatórios rodam na
  coleção sequencial `CheckoutCollection`.
- **Next.js num monorepo:** `turbopack.root` e `outputFileTracingRoot` apontam para a raiz (por causa
  de `packages/tokens`); os Dockerfiles dos fronts usam a raiz como contexto e o servidor standalone
  fica em `apps/<app>/server.js`.
- **Layout da loja** não pode ler cookies no servidor (tornaria as páginas ISR dinâmicas); dados de
  sessão no cabeçalho são carregados no cliente.

## Passagem de bastão entre agentes

Ao **encerrar uma sessão** (ou quando o limite de uso estiver perto), atualize
[`docs/handoff.md`](docs/handoff.md): o que foi feito, o que ficou pela metade, próximos passos e
decisões em aberto. Faça commit e push. Quem assumir lê o `handoff.md` e continua dali.
