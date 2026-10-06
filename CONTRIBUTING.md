# Contribuindo

## Fluxo

- `main` é protegida: toda mudança entra por pull request com o CI verde.
- Branches curtas, nomeadas pelo tipo da mudança: `feat/plp-filtros`, `fix/reserva-expirada`.

## Commits

Seguimos [Conventional Commits](https://www.conventionalcommits.org/pt-br/):

```
<tipo>(<escopo opcional>): <descrição no imperativo>
```

Tipos: `feat`, `fix`, `docs`, `refactor`, `test`, `chore`, `ci`, `perf`, `build`.
Escopos comuns: `api`, `web`, `catalog`, `inventory`, `cart`, `orders`, `payments`, `identity`, `infra`.

Exemplos:

- `feat(catalog): adiciona filtro por faixa de preço na PLP`
- `fix(inventory): impede reserva acima do estoque disponível`
- `docs(adr): registra decisão sobre idempotência de webhooks`

## Checagens locais

```bash
# API
dotnet format Kamus.slnx --verify-no-changes
dotnet build Kamus.slnx
dotnet test --solution Kamus.slnx    # precisa do Docker (Testcontainers)

# Web
cd apps/web
npm run format:check && npm run lint && npm run typecheck && npm test
```

## Decisões de arquitetura

Decisões relevantes viram ADR em `docs/adr/NNNN-titulo.md`, a partir de
[`0000-template.md`](docs/adr/0000-template.md).

## Configurando a proteção da `main` (uma vez, no GitHub)

Settings → Branches → Add rule para `main`:

- Require a pull request before merging
- Require status checks to pass: `API (.NET)`, `Web (Next.js)`, `Docker images`
- Do not allow bypassing the above settings
