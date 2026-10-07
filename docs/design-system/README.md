# Design system

O Kamus tem duas "peles" sobre os mesmos tokens de cor e tipografia:

- **Loja** (`apps/web`): espaçosa, editorial, serifa nos títulos, foco em imagem e conversão.
- **Backoffice** (`apps/admin`): densa, sans-serif, tabelas, formulários compactos, gráficos e
  indicadores. Status de pedido usam as mesmas cores nas duas, e nos gráficos a cor de destaque
  (terracota) é a única série; cores de status nunca viram cor de série.

Os tokens estão em `apps/web/src/app/globals.css` e `apps/admin/src/app/globals.css` (ADR 0011).

## Loja

| | |
|---|---|
| ![Marca](01-marca.png) | ![Cores](02-cores.png) |
| ![Tipografia](03-tipografia.png) | ![Botões e links](04-botoes.png) |
| ![Formulários](05-formularios.png) | ![Seleção de variação](06-selecao.png) |
| ![Preços e status](07-precos-e-status.png) | ![Card de produto](08-card-de-produto.png) |
| ![Ilustrações das peças](09-ilustracoes.png) | |

## Telas

| Desktop | Mobile |
|---|---|
| ![Home](10-tela-home-desktop.png) | ![Home](11-tela-home-mobile.png) |
| ![Listagem](12-tela-plp-desktop.png) | ![Listagem](13-tela-plp-mobile.png) |
| ![Produto](14-tela-pdp-desktop.png) | ![Produto](15-tela-pdp-mobile.png) |

## Backoffice

| | |
|---|---|
| ![Login](admin-01-login.png) | ![Painel com relatórios](admin-02-painel.png) |
| ![Lista de produtos](admin-03-produtos.png) | ![Editor de produto](admin-04-produto-editor.png) |
| ![Pedidos](admin-05-pedidos.png) | ![Pedido despachado](admin-06-pedido-despachado.png) |
