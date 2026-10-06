# 0010 — Autenticação com cookie HttpOnly via BFF

- **Status:** aceita
- **Data:** 2026-10-06
- **Release:** R2

## Contexto

O storefront Next.js precisa de sessão em páginas renderizadas no servidor (checkout, minha conta)
e em chamadas do navegador. A stack prevê ASP.NET Core Identity, sem provedor externo no MVP.

## Decisão

- ASP.NET Core Identity com usuários no schema `identity` e **cookie de autenticação**
  (`kamus_auth`): `HttpOnly`, `SameSite=Lax`, `Secure` quando a requisição original é HTTPS,
  validade de 14 dias com renovação deslizante.
- O navegador só conversa com o Next.js (mesma origem). O Next encaminha `/api/*` para a API
  (BFF), repassando cookies e `X-Forwarded-Proto/Host`; páginas do servidor repassam o cookie
  recebido ao chamar a API.
- Chaves do Data Protection persistidas no Postgres, para os cookies sobreviverem a deploys e
  funcionarem com várias instâncias.
- A API responde 401/403 (sem redirecionar) e bloqueia a conta por 5 minutos após 5 senhas erradas.
  Login e cadastro respondem a mesma mensagem para e-mail inexistente e senha errada.
- CSRF: `SameSite=Lax` impede o envio do cookie em POSTs de outros sites e a API só aceita JSON.

## Alternativas consideradas

- **JWT em `localStorage`** — exposto a XSS e exige refresh token; não ajuda o SSR.
- **JWT em cookie** — possível, mas revogar e renovar fica por conta da aplicação; o cookie do
  Identity já resolve isso.
- **Provedor externo (Auth0, Clerk, Entra ID)** — fora do escopo do MVP.

## Consequências

- Nenhum token acessível ao JavaScript; SSR autenticado sem configuração extra.
- O layout continua estático: o estado de login no cabeçalho é carregado no cliente
  (`/api/identity/session`), para não tornar dinâmicas as páginas em ISR.
- Clientes que não sejam navegador (apps móveis) exigirão tokens no futuro.
