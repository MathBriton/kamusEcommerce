# Design · Vitrine (loja)

Capturas do storefront (`apps/web`) geradas pelos testes E2E, com o catálogo de exemplo. Os
tokens de cor e fonte vivem em [`packages/tokens/theme.css`](../../../packages/tokens/theme.css),
compartilhados com o backoffice. Fundamentos e componentes vêm da página `/design-system`.

**Estilo:** espaçoso e editorial; serifa nos títulos, sans-serif no texto, foco em imagem.

## Fundamentos

| Marca                           | Cores                           | Tipografia                                |
| ------------------------------- | ------------------------------- | ----------------------------------------- |
| ![Marca](fundamentos/marca.png) | ![Cores](fundamentos/cores.png) | ![Tipografia](fundamentos/tipografia.png) |

## Componentes

|                                                                  |                                                                 |
| ---------------------------------------------------------------- | --------------------------------------------------------------- |
| ![Botões e links](componentes/botoes-e-links.png)                | ![Formulários](componentes/formularios.png)                     |
| ![Seleção de cor e tamanho](componentes/selecao-de-variacao.png) | ![Preços e status](componentes/precos-e-status.png)             |
| ![Card de produto](componentes/card-de-produto.png)              | ![Ilustrações das peças](componentes/ilustracoes-das-pecas.png) |

## Telas · vitrine

| Desktop                                               | Mobile                                               |
| ----------------------------------------------------- | ---------------------------------------------------- |
| ![Home](telas/vitrine/home-desktop.png)               | ![Home](telas/vitrine/home-mobile.png)               |
| ![Listagem (PLP)](telas/vitrine/listagem-desktop.png) | ![Listagem (PLP)](telas/vitrine/listagem-mobile.png) |
| ![Produto (PDP)](telas/vitrine/produto-desktop.png)   | ![Produto (PDP)](telas/vitrine/produto-mobile.png)   |

## Telas · fluxo de compra

|                                                           |                                                                       |
| --------------------------------------------------------- | --------------------------------------------------------------------- |
| ![Produto adicionado](telas/compra/01-pdp-adicionado.png) | ![Sacola](telas/compra/02-sacola.png)                                 |
| ![Checkout](telas/compra/03-checkout.png)                 | ![Pedido aguardando pagamento](telas/compra/04-pedido-aguardando.png) |
| ![Pedido pago](telas/compra/05-pedido-pago.png)           | ![Pedido entregue](telas/compra/06-pedido-entregue.png)               |
| ![Minha conta](telas/compra/07-minha-conta.png)           |                                                                       |

O fluxo animado está em [`docs/media/fluxo-de-compra.gif`](../../../docs/media/fluxo-de-compra.gif).

## Convenções

- `fundamentos/`: marca, cores, tipografia. `componentes/`: peças isoladas. `telas/<área>/`: páginas.
- Nomes em português, minúsculos, com hífen; sufixo `-desktop`/`-mobile` quando houver as duas.
- Capturas em resolução 1x (desktop 1280px, mobile 390px de largura).

## Como estas imagens são geradas

Elas são as **capturas de referência dos testes E2E** (`e2e/`, ADR 0013), produzidas numa stack
isolada com banco zerado, sempre iguais:

```bash
./e2e/run.sh            # compara as telas atuais com estas imagens (o CI faz o mesmo)
./e2e/run.sh --update   # aceita mudanças intencionais e reescreve as imagens
```

Áreas em tom areia cobrem valores que mudam a cada execução (datas, horas, ids).
