# 0004 — Modelagem produto/SKU e atributos de variação

- **Status:** aceita
- **Data:** 2026-10-06
- **Release:** R1

## Contexto

Em moda, o cliente escolhe um **produto** ("Calça Jeans Slim") mas compra uma **variação**
(Azul, tamanho 40). Estoque, preço e código de barras pertencem à variação. A vitrine precisa
filtrar por cor e tamanho, mostrar preço "de/por" e montar a galeria por cor.

## Decisão

- `Product` é o agregado pai: nome, slug, descrição, marca, categoria e coleção.
- `Sku` é a unidade vendável, com **duas variações fixas e tipadas**: `Color` (nome + hex) e
  `Size` (rótulo + `SizeOrder` para ordenar PP < P < M e 36 < 38). Preço e preço promocional
  ficam no SKU, permitindo diferenças entre tamanhos no futuro.
- `ProductImage` pertence ao produto e é **agrupada por cor**, já que a foto muda com a cor e não
  com o tamanho.
- `Category` é hierárquica com caminho materializado (`Path = "masculino/calcas/jeans"`), o que
  torna "categoria e descendentes" um simples `Path = x OR Path LIKE 'x/%'`, indexável.
- O estoque **não** fica no SKU: pertence ao módulo Inventory (`StockLevel` por `SkuId`).
- O slug do produto é global e único; a URL pública é `{caminho-da-categoria}/{slug}`.

## Alternativas consideradas

- **Atributos genéricos (EAV / `jsonb` de variações)** — flexível para outros segmentos, mas
  complica filtros, índices e validação. Moda tem cor e tamanho como eixos estáveis.
- **Produto por cor** (cada cor é um produto separado) — simplifica a galeria, mas fragmenta a
  PLP e o SEO, e duplica descrição.
- **Árvore de categorias com adjacency list pura** — exigiria CTE recursiva em toda listagem.

## Consequências

- Filtros de PLP viram `EXISTS` em `skus` com índices em `size` e `color`.
- Adicionar um terceiro eixo (ex.: comprimento) exigirá migration, aceito conscientemente.
- Mover uma categoria exige reescrever o `Path` dos descendentes (operação rara e administrativa).
