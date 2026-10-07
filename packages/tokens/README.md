# @kamus/tokens

Tokens de design compartilhados entre a loja (`apps/web`) e o backoffice (`apps/admin`).
Cada app importa `theme.css` no próprio `globals.css`:

```css
@import "tailwindcss";
@import "../../../../packages/tokens/theme.css";
```

Alterou uma cor ou fonte? Rode os testes E2E (`e2e/`): a regressão visual mostra todas as telas
afetadas, nos dois apps.
