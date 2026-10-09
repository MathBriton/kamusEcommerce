# 0015 — Exclusão reversível (soft delete) com lixeira

- **Status:** aceita
- **Data:** 2026-10-08
- **Release:** R12

## Contexto

No backoffice, excluir um produto apagava a linha e, com ela, os SKUs, as imagens e qualquer chance
de desfazer um clique errado. Ao mesmo tempo, um produto excluído precisa sumir de verdade: da
vitrine, da busca, do carrinho e do checkout, sem que cada consulta precise lembrar de um filtro.

## Decisão

- **Produtos, SKUs e imagens** ganham `DeletedAt`, `DeletedById` e `DeletedByName`.
  - Um **filtro global do EF Core** (`HasQueryFilter`) esconde os excluídos de toda consulta. Só a
    lixeira e os rótulos da auditoria usam `IgnoreQueryFilters()`.
- **`Remove()` vira marcação.** O `SoftDeleteInterceptor` troca o DELETE por um UPDATE.
  - Os filhos que o EF marcou para exclusão em cascata recebem o **mesmo instante**. É por esse
    instante que o produto restaurado traz de volta exatamente o que saiu com ele.
  - Regra única para apagar de vez: **remover o que já está na lixeira é exclusão definitiva**.
- **Índices únicos parciais** (`WHERE deleted_at IS NULL`) no slug do produto, no código do SKU e
  em produto + cor + tamanho.
  - O slug de um produto excluído pode ser reaproveitado.
  - Se isso acontecer, restaurar o antigo é recusado com uma mensagem clara, em vez de renomear em
    silêncio.
- **Lixeira no backoffice:**
  - restaurar e excluir de vez;
  - **expurgo automático** depois de `Catalog:TrashRetentionDays` (30 dias por padrão), que também
    remove os arquivos de imagem e avisa o Inventory (`SkusPurged`) para apagar o estoque.
- **Nunca excluídos:**
  - **pedidos**, que só são cancelados, porque são registro fiscal e histórico do cliente;
  - **o log de auditoria** (ADR 0014).
- **Dados pessoais não usam soft delete:** manter dados "escondidos" não atende o direito de
  eliminação da LGPD. A exclusão de conta será por anonimização (R10).
- **Concorrência otimista** (`xmin`) em produto, SKU e imagem:
  - expurgar algo que acabou de ser restaurado, ou excluir as duas últimas variações ao mesmo
    tempo, falha em vez de um atropelar o outro;
  - os casos de uso disputados tentam de novo com dados frescos (`ConcurrencyRetry`);
  - o resto responde 409 com uma mensagem clara.
- **Fotos usadas por pedidos ficam.** O item do pedido guarda a URL da foto da compra. Antes de
  apagar um arquivo, o expurgo pergunta ao Orders (`IOrderImageReferences`); o histórico do
  cliente não perde a miniatura.

## Alternativas consideradas

- **Tabela de arquivo (mover a linha para `deleted_products`)** — consultas normais ficam limpas
  sem filtro, mas restaurar exige copiar de volta o agregado inteiro e manter dois esquemas iguais.
- **Coluna `is_deleted` booleana** — não diz quando nem por quem, e não permite o expurgo por idade.
- **Exclusão definitiva com confirmação dupla** — protege do clique errado, mas não do
  arrependimento, e perde o histórico do que existia.

## Consequências

- Toda entidade nova "excluível" (categorias, coleções e cupons, na R11) precisa implementar
  `ISoftDeletable`, ganhar filtro e ter os índices únicos revistos.
- Consultas que precisam ver excluídos têm de pedir isso explicitamente (`IgnoreQueryFilters`),
  o que deixa a intenção visível no código.
- O aviso `SkusPurged` sai depois do commit do expurgo. Se falhar, o estoque daqueles SKUs fica
  órfão (sem efeito na loja, que já não os vê) até a R3 trazer o outbox.
- As linhas excluídas ocupam espaço até o expurgo; com 30 dias de retenção, o volume é desprezível.
- A página da vitrine de um produto excluído pode seguir no cache do Next.js por até 5 minutos (ISR);
  a revalidação sob demanda chega na R4.
