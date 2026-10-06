# Deploy

O MVP roda em qualquer plataforma que execute containers Docker com Postgres e Redis gerenciados.
O repositório traz um _blueprint_ pronto para o [Render](https://render.com) (`render.yaml`), que
cria tudo com o plano gratuito.

## Render (recomendado para a demo)

1. Faça login no Render com a conta do GitHub.
2. **New → Blueprint** e selecione este repositório. O Render lê o `render.yaml` e cria:
   - `kamus-db` (PostgreSQL), `kamus-redis` (Key Value);
   - `kamus-api` (container da API, com migrations e seed automáticos na primeira subida);
   - `kamus-web` (container do Next.js, apontando para a API pela rede interna).
3. Aguarde os dois serviços ficarem _Live_ e abra a URL do `kamus-web`.

O que já está resolvido no código para rodar em plataformas gerenciadas:

- conexões no formato URL (`postgresql://...`, `redis://...`) são convertidas automaticamente;
- a API confia em `X-Forwarded-Proto` vindo do Next.js, então o cookie de login é `Secure` em HTTPS;
- chaves do Data Protection ficam no Postgres (os logins sobrevivem a novos deploys);
- o disco dos containers é efêmero: as ilustrações do catálogo são regeradas na inicialização
  (na R4 as imagens vão para storage S3-compatível);
- `SITE_URL` é opcional: no Render o Next.js usa `RENDER_EXTERNAL_URL` em canonical e sitemap.

### Limitações do plano gratuito

- Serviços "dormem" após 15 minutos sem acesso; a primeira requisição depois disso leva ~1 minuto.
- O Postgres gratuito expira após 30 dias (basta recriar; o seed roda de novo).

## Outras plataformas

| Componente | Variáveis |
|---|---|
| API (`apps/api/Dockerfile`, contexto na raiz) | `ConnectionStrings__Postgres`, `ConnectionStrings__Redis`, `Database__MigrateOnStartup=true`, `Seed__Enabled=true`, `Payments__FakePay__WebhookUrl` (URL do próprio container, ex.: `http://localhost:8080/api/payments/webhooks/fakepay`), `Payments__FakePay__WebhookSecret` |
| Web (`apps/web/Dockerfile`, contexto em `apps/web`) | `API_URL` (URL interna da API), `SITE_URL` (URL pública), `ENABLE_FULFILLMENT_SIMULATION` |

A infraestrutura como código (Terraform/Pulumi), com ambientes separados, fica para a R7.
