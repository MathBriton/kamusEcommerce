# 0013 — Testes E2E e regressão visual com as imagens do design system

- **Status:** aceita
- **Data:** 2026-10-07
- **Release:** R8

## Contexto

Os testes de integração cobrem a API, mas nada garantia que a loja e o backoffice continuassem
funcionando de ponta a ponta no navegador, nem que uma mudança de CSS (por exemplo, nos tokens
compartilhados) não quebrasse telas sem ninguém perceber. Ao mesmo tempo, as imagens do design system
eram capturadas à mão e envelheciam a cada mudança.

## Decisão

- **Playwright** em `e2e/`, com três projetos encadeados: `vitrine` (guia de design e páginas
  públicas, com o estoque do seed intacto) → `compra` (fluxo completo de um cliente) → `backoffice`
  (login, painel, criação e publicação de produto, despacho com rastreio).
- **As capturas de referência são as imagens do design system** (`apps/web/design`,
  `apps/admin/design`, via `snapshotPathTemplate`). Teste e documentação são o mesmo artefato: não
  existe imagem desatualizada.
- **Determinismo por construção**, porque comparação visual só vale se a tela for sempre igual:
  - tudo roda numa **stack Docker isolada** (`e2e/run.sh`, projeto `kamus-e2e`) com **banco zerado**
    e o **container oficial do Playwright** (mesmo navegador e mesmas fontes no CI e localmente);
  - seed com **ids determinísticos** (Guid v7 com timestamp real e restante derivado do slug) e
    **desempates explícitos** nas ordenações (nunca por id aleatório);
  - valores que mudam a cada execução (datas, horas, ids de pagamento) são marcados com
    `data-volatile` e cobertos por uma máscara na cor `sand`;
  - o mouse sai da tela antes de cada captura (sem hovers).
- **Tolerância mínima** (`maxDiffPixels: 50`, só ruído de antialiasing). Mudança intencional:
  `./e2e/run.sh --update` reescreve todas as referências, e o diff das imagens entra no PR para revisão.
- Página `/design-system` na loja (desligada em produção) serve de guia vivo para fundamentos e
  componentes.

## Alternativas consideradas

- **Serviço de regressão visual (Chromatic, Percy)** — revisão confortável, mas pago e externo.
- **Tolerância alta (1%)** — testada e rejeitada: deixou passar um tooltip inteiro e uma troca de
  palavras.
- **Rodar no navegador do próprio runner** — fontes diferentes entre máquinas geram falsos positivos.

## Consequências

- Um PR que muda a interface mostra as telas afetadas como diff de imagem.
- A execução completa leva poucos minutos (build das imagens + ~40s de testes).
- Toda tela nova precisa nascer determinística: datas com `data-volatile`, ordenações com desempate.
