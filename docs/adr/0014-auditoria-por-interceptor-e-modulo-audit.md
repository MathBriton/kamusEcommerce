# 0014 — Auditoria por interceptor do EF Core e módulo Audit

- **Status:** aceita
- **Data:** 2026-10-08
- **Release:** R12

## Contexto

O backoffice altera catálogo, estoque e pedidos, mas até a R2.5 não havia como responder "quem
mudou o preço desta camisa, quando e qual era o valor anterior?". A resposta precisa valer para toda
alteração, inclusive as feitas por processos automáticos (aviso de pagamento, expiração de reserva,
expurgo da lixeira), sem depender de cada caso de uso lembrar de registrar algo. E o registro precisa
ser confiável: se a alteração foi gravada, o registro também foi, e ninguém o edita depois.

## Decisão

- **Captura automática por `SaveChangesInterceptor`** (`Kamus.Shared.Auditing`). Cada módulo
  declara uma **política** no próprio registro do DbContext: quais entidades são auditadas, qual é o
  agregado exibido (produto, pedido), qual é o rótulo e **quais campos entram (allowlist)**, com nome
  em português e formatação ("R$ 249,90", "Publicado"). Campo fora da lista nunca é gravado.
- **Somente o que mudou**, como pares antes → depois. Ações genéricas (`created`, `updated`,
  `deleted`, `restored`, `purged`) são refinadas pela política quando o campo diz mais que o verbo:
  `published`, `shipped`, `stock_adjusted`, `stock_sold`.
- **Quem**: `ICurrentActor` resolve o usuário autenticado (Admin ou Cliente, com nome e e-mail) ou
  um ator **Sistema** nomeado, definido por quem roda sem requisição (`ActAs(System("FakePay"))`).
  Contexto da requisição: IP e o identificador da requisição (`TraceIdentifier`).
- **Mesma transação da alteração.** O interceptor abre uma transação, se não houver, e grava os
  registros na mesma conexão antes do commit: ou a alteração e o registro existem juntos, ou nenhum
  dos dois. Pagamos uma inserção a mais por SaveChanges auditado.
- **Módulo Audit** (schema `audit`) é o dono da tabela e implementa `IAuditLog`, um building block
  do Shared, como `IEventPublisher`. Os módulos de negócio não conhecem o schema `audit`. O módulo
  também expõe a leitura no backoffice: tela Atividade, histórico por produto ou pedido e
  exportação CSV.
- **Somente inclusão, garantido pelo banco**: triggers recusam `UPDATE` e `TRUNCATE` sempre, e
  `DELETE` de linhas com menos de um ano. A retenção (`Audit:RetentionDays`, 2 anos por padrão,
  nunca menos de 365 dias) é aplicada por um worker diário.
- Seed de dados de exemplo roda com a auditoria suspensa: não é alteração de ninguém.

## Alternativas consideradas

- **Triggers no banco gravando a auditoria** — pegam até alteração feita fora da aplicação, mas não
  sabem quem é o usuário (a aplicação usa uma única credencial) nem o nome legível do campo; a lógica
  fica em PL/pgSQL, fora do alcance dos testes do .NET.
- **Event sourcing** — o histórico seria o próprio modelo, com auditoria "de graça", mas muda a forma
  de persistir todos os módulos e complica as consultas. Desproporcional ao problema.
- **Registro manual em cada caso de uso** — explícito, mas basta esquecer um caso para ter um buraco;
  o interceptor torna a auditoria o padrão, não uma lembrança.
- **Gravar depois do commit, em outra conexão** — mais simples, mas uma falha entre as duas gravações
  deixa alteração sem registro.

## Consequências

- Entidade nova num módulo auditado só entra na auditoria quando ganha política. Campos novos
  precisam ser incluídos de propósito, o que evita vazar dado sensível (o Identity nunca é auditado).
- Operações em massa (`ExecuteUpdate`/`ExecuteDelete`) não passam pelo interceptor; quem usá-las em
  dados auditados precisa registrar à mão.
- A tela Atividade e o histórico do produto são consultas sobre uma única tabela indexada por
  agregado e por data. Quando a R3 trouxer outbox e broker, a auditoria pode virar consumidora de
  eventos sem mudar a tabela.
