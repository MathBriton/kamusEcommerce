# 0002 — PostgreSQL como banco relacional

- **Status:** aceita
- **Data:** 2026-10-06
- **Release:** R0

## Contexto

O domínio é fortemente relacional (produto → SKU → estoque → item de pedido) e precisa de
transações e controle de concorrência no estoque. O projeto também deve rodar em qualquer cloud e
usar uma tecnologia comum em vagas.

## Decisão

Usar **PostgreSQL 17**, acessado via EF Core com o provider Npgsql. Um único banco com **um schema
por módulo** (`catalog`, `inventory`, `orders`, `payments`, `identity`), cada um com seu próprio
`DbContext` e histórico de migrations.

## Alternativas consideradas

- **SQL Server** — integração natural com .NET, mas licenciamento e custo em cloud pesam; menos
  presente fora do ecossistema Microsoft.
- **MySQL/MariaDB** — viável, mas com menos recursos úteis aqui (tipos `jsonb`, arrays, índices
  parciais, `xmin` para concorrência otimista).
- **Banco de documentos (MongoDB)** — o catálogo até caberia, mas pedidos e estoque pedem
  transações e integridade referencial.

## Consequências

- Open source, disponível como serviço gerenciado em todas as clouds e fácil de subir em Docker e
  Testcontainers.
- Schemas separados permitem, no futuro, mover um módulo para um banco próprio sem reescrever
  consultas.
- Recursos específicos do Postgres (ex.: `xmin`, `jsonb`) criam algum acoplamento ao banco, aceito
  conscientemente.
