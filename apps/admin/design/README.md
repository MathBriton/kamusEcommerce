# Design · Backoffice

Capturas do backoffice (`apps/admin`) geradas a partir da aplicação rodando, com pedidos de
demonstração. Os tokens são os mesmos da loja (ver [`src/app/globals.css`](../src/app/globals.css)
e o ADR 0011).

**Estilo:** denso e utilitário; sans-serif, tabelas, formulários compactos e indicadores. Gráficos
usam uma única série na cor de destaque (terracota); as cores de status ficam reservadas aos selos.

## Telas

|                                             |                                                      |
| ------------------------------------------- | ---------------------------------------------------- |
| ![Login](telas/01-login.png)                | ![Painel com relatórios](telas/02-painel.png)        |
| ![Lista de produtos](telas/03-produtos.png) | ![Editor de produto](telas/04-produto-editor.png)    |
| ![Pedidos](telas/05-pedidos.png)            | ![Pedido despachado](telas/06-pedido-despachado.png) |

## Convenções

- `telas/`: páginas, numeradas na ordem de navegação. Componentes isolados, quando houver, vão em
  `componentes/`.
- Nomes em português, minúsculos, com hífen. Capturas em 2x, desktop (1360px de largura).
