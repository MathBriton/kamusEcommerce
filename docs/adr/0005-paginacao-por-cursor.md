# 0005 — Paginação por cursor em vez de offset

- **Status:** aceita
- **Data:** 2026-10-06
- **Release:** R1

## Contexto

A PLP lista produtos ordenados por lançamento, preço ou nome, com "carregar mais". Com
`OFFSET/LIMIT`, o banco percorre e descarta todas as linhas anteriores a cada página (custo
cresce com a profundidade) e itens inseridos ou removidos entre requisições causam repetições ou
buracos na listagem.

## Decisão

Usar **paginação keyset** com cursor opaco:

- a ordenação sempre termina no `Id` como desempate, garantindo ordem total
  (`price ASC, id ASC`, `created_at DESC, id DESC`...);
- o cursor é o par (chave de ordenação, id) do último item, serializado em JSON + base64url;
- a próxima página filtra `WHERE (key, id) > (cursor.key, cursor.id)` e busca `limit + 1` linhas
  para saber se há mais;
- o cursor carrega a ordenação de origem; usar um cursor com outra ordenação retorna 400.

A resposta é `{ items, nextCursor }`; `nextCursor = null` indica o fim.

## Alternativas consideradas

- **Offset/limit** — simples e permite "ir para a página 7", mas é instável e degrada com a
  profundidade. Navegação por número de página não é um requisito da vitrine.
- **Cursor do lado do banco (server-side cursor)** — mantém estado no servidor; não combina com
  HTTP stateless nem com cache.

## Consequências

- Custo por página constante, sem repetições quando o catálogo muda entre requisições.
- Não há "pular para a página N" nem total exato barato; o total vem do endpoint de facetas.
- Cada nova ordenação precisa de uma expressão keyset correspondente (coberta por testes de
  integração que comparam paginação com uma página única).
