# 0009 — Idempotência no processamento de webhooks

- **Status:** aceita
- **Data:** 2026-10-06
- **Release:** R2

## Contexto

O resultado do pagamento chega por **webhook** do provedor (FakePay, simulado). Provedores entregam
"pelo menos uma vez": o mesmo evento pode chegar duplicado, fora de ordem ou em paralelo (retentativas
após timeout). Processar duas vezes poderia baixar estoque em dobro ou registrar dois pagamentos.
Também é preciso garantir que o webhook veio mesmo do provedor.

## Decisão

1. **Autenticidade:** o corpo é assinado com HMAC-SHA256 e segredo compartilhado
   (`FakePay-Signature: t=<unix>,v1=<hex>`, onde `v1 = HMAC(segredo, "t.corpo")`). A API recalcula
   sobre o corpo bruto, compara em tempo constante e rejeita assinaturas com mais de 5 minutos
   (proteção contra _replay_). Falha → 401.
2. **Idempotência:** tabela `payments.processed_webhook_events` com o **id do evento como chave
   primária**. O registro do evento e a mudança de status do pagamento são gravados **na mesma
   transação**. Uma segunda entrega encontra o id (ou perde a corrida no `INSERT`, violação de
   unicidade 23505) e recebe `200 {"duplicate": true}` sem nenhum efeito.
3. **Estado monotônico:** um pagamento finalizado (`Approved`/`Declined`) não muda mais, e a máquina
   de estados do pedido recusa transições repetidas (`Paid → Paid`).
4. **Aviso ao pedido com reenvio:** após gravar, o módulo Payments publica `PaymentApproved` ou
   `PaymentDeclined` e marca `order_notified_at`. Se o aviso falhar, um _worker_ reenvia os
   pendentes a cada 10s; os handlers do Orders são idempotentes. (É um _outbox_ simplificado; a R3
   formaliza o padrão com broker.)

Prova automatizada: o mesmo evento enviado 1 + 5 vezes (em paralelo) gera um único registro, um único
`Paid` no histórico e uma única baixa de estoque.

## Alternativas consideradas

- **Checar o status do pagamento antes de processar (sem tabela de eventos)** — sujeito a corrida
  entre duas entregas simultâneas e não distingue eventos diferentes do mesmo pagamento.
- **Lock distribuído no Redis por evento** — resolve a corrida, mas a garantia fica fora da
  transação do banco.
- **Responder 500 para forçar retentativa até o pedido ser atualizado** — acopla o provedor à
  disponibilidade do módulo Orders.

## Consequências

- Reentregas são baratas e seguras; o provedor pode retentar à vontade.
- A tabela de eventos cresce indefinidamente; um expurgo por idade (ex.: 90 dias) é suficiente.
- O segredo do webhook é configuração sensível (no Render, gerado automaticamente).
