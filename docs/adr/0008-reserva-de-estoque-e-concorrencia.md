# 0008 — Estratégia de reserva de estoque e concorrência

- **Status:** aceita
- **Data:** 2026-10-06
- **Release:** R2

## Contexto

Entre "clicar em pagar" e o banco aprovar o pagamento passam alguns segundos (ou minutos). Se o
estoque só baixasse na aprovação, dois clientes poderiam pagar pelo último item. Se baixasse ao
clicar, pagamentos recusados ou abandonados prenderiam estoque para sempre. Além disso, dois checkouts
simultâneos podem ler o mesmo saldo ao mesmo tempo.

## Decisão

- `StockLevel` tem `Quantity` (físico) e `Reserved` (soma das reservas ativas).
  Disponível = `Quantity − Reserved`, e a vitrine mostra o disponível.
- Ao **iniciar o pagamento**, o Orders pede ao Inventory uma **reserva tudo-ou-nada** de todos os
  itens, com **validade de 15 minutos**. Faltou qualquer item → nada é reservado e o checkout
  responde 409 com as faltas.
- Reserva → **Committed** quando o pagamento é aprovado (baixa o físico), **Released** quando é
  recusado ou o pedido é cancelado, **Expired** quando vence (um _worker_ varre a cada 30s e
  publica `ReservationExpired`, que cancela o pedido).
- **Concorrência otimista** com a coluna de sistema `xmin` do Postgres como _row version_:
  o `UPDATE` só vale se a linha não mudou desde a leitura. Em conflito, o EF lança
  `DbUpdateConcurrencyException`, o contexto é limpo e a operação é refeita com dados novos (até 5
  tentativas). Reserva, alteração de saldo e criação da reserva acontecem em **uma transação**.
- Restrições no banco (`CHECK reserved <= quantity`, `quantity >= 0`) são a última linha de defesa.
- Pagamento aprovado depois da expiração: o commit falha (a reserva não está mais ativa), o pedido é
  cancelado e o caso é registrado para estorno.

Provas automatizadas: duas compras simultâneas do último item resultam em exatamente um pedido;
20 reservas paralelas sobre 5 unidades resultam em exatamente 5 sucessos.

## Alternativas consideradas

- **Lock pessimista (`SELECT ... FOR UPDATE`)** — correto e simples, mas serializa checkouts e
  segura conexões durante a espera; o conflito real (mesmo SKU, mesmo instante) é raro.
- **Baixar estoque só na aprovação** — permite vender o mesmo item duas vezes.
- **Contador atômico no Redis** — rápido, mas cria uma segunda fonte de verdade para o estoque.

## Consequências

- Nenhum _overselling_ e nenhuma espera bloqueante; em picos de disputa pelo mesmo SKU, as
  tentativas aumentam (observar na R6 com testes de carga).
- O estoque fica "preso" por até 15 minutos em pagamentos sem resposta.
- O _worker_ de expiração precisa rodar em uma instância só ou ser idempotente (é: só muda reservas
  ainda ativas, também sob concorrência otimista).
