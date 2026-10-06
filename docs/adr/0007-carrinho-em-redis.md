# 0007 — Carrinho em Redis vs. banco relacional

- **Status:** aceita
- **Data:** 2026-10-06
- **Release:** R2

## Contexto

O carrinho é o dado mais escrito da loja: cada clique em "adicionar" ou "alterar quantidade" é
uma escrita, a maioria de visitantes anônimos que nunca vão comprar. Carrinhos abandonados precisam
sumir sozinhos, e o carrinho anônimo precisa ser juntado ao do cliente no login.

## Decisão

- Carrinho em **Redis**, um _hash_ por dono: `kamus:cart:v:{visitorId}` (visitante, cookie
  `kamus_vid` HttpOnly) ou `kamus:cart:c:{customerId}` (cliente autenticado).
- Cada campo é um SKU; o valor guarda `quantidade|preço-no-momento-da-adição`, o que permite avisar
  "o preço mudou" no checkout.
- **TTL de 7 dias**, renovado a cada escrita: carrinhos abandonados expiram sem job de limpeza.
- Escritas usam **transação condicional** (`HashEqual`/`HashNotExists` + `MULTI/EXEC`): se outra
  aba alterou o mesmo item entre a leitura e a escrita, a operação é refeita.
- No login, o evento `CustomerSignedIn` (Identity) faz o módulo Cart somar o carrinho do visitante no
  do cliente (limitado a 10 unidades por item) e apagar o do visitante.
- O carrinho guarda só ids, quantidades e preço de referência; nome, imagem, preço atual e estoque
  são sempre lidos do Catalog e do Inventory ao exibir (nada fica velho no Redis).

## Alternativas consideradas

- **Tabela no Postgres** — durável e consultável (relatórios de abandono), mas coloca a escrita mais
  frequente e menos valiosa no banco transacional e exige limpeza periódica.
- **Cookie/localStorage** — zero infraestrutura, mas não sobrevive à troca de dispositivo, não
  permite validação no servidor e limita o tamanho.
- **JSON único por carrinho** — mais simples, porém toda alteração reescreve o documento e
  aumenta a chance de conflito entre abas.

## Consequências

- Escritas O(1) em memória e expiração automática.
- Carrinhos não são duráveis: uma perda do Redis sem persistência esvazia carrinhos (aceitável;
  em produção, usar Redis com AOF ou serviço gerenciado).
- Relatórios de carrinho abandonado exigirão eventos (R3) em vez de consultas SQL.
