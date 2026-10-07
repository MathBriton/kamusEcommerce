# 0012 — Relatórios compostos por contratos (e evolução para read models)

- **Status:** aceita
- **Data:** 2026-10-07
- **Release:** R2.5

## Contexto

O backoffice precisa de faturamento, ticket médio, pedidos por status, mais vendidos e estoque
baixo. Esses números cruzam três módulos (Orders, Inventory, Catalog). A regra do monólito modular
(ADR 0001) proíbe um módulo de ler tabelas de outro, e um SQL único com `JOIN` entre schemas seria
o atalho mais tentador.

## Decisão

- Novo módulo **Reporting**, sem tabelas próprias, que **compõe** os relatórios a partir de
  contratos:
  - `IOrderReports` (Orders): fatos de pedido no período (data, status, total, unidades) e itens
    mais vendidos a partir do snapshot dos pedidos;
  - `IInventoryService.GetLowStockAsync` (Inventory): SKUs com disponível baixo;
  - `ICatalogService.GetSkusAsync` (Catalog): nome, cor e tamanho para enriquecer o estoque baixo.
- Cada módulo agrega o que é seu no banco (somas e agrupamentos em SQL); o Reporting só junta
  resultados já pequenos.
- **Dias no horário de Brasília**, não em UTC: um pedido às 23h de segunda pertence a segunda.
  A série diária devolve todos os dias do período, inclusive os sem venda.
- Receita considera pedidos **Pago, Enviado e Entregue**; taxa de recusa = recusados ÷ (vendidos +
  recusados); cancelamento = cancelados ÷ pedidos criados.

## Alternativas consideradas

- **SQL direto entre schemas** — mais rápido de escrever, mas acopla o Reporting ao modelo físico de
  três módulos e quebra no dia em que um deles for extraído.
- **Banco analítico / ferramenta de BI (Metabase, warehouse)** — adequado em escala, exagerado sem
  volume e sem eventos para alimentá-lo.

## Consequências

- Relatórios respeitam as fronteiras e são testáveis com os mesmos contratos (os testes conferem
  que o faturamento sobe exatamente o total dos pedidos pagos).
- Cada consulta varre os pedidos do período; com volume, isso fica caro. **Na R3**, com eventos
  (`OrderPaid`, `StockChanged`), o Reporting passa a manter **read models** próprios (ex.: tabela de
  vendas por dia), atualizados por consumidores — mesma API, implementação trocada.
