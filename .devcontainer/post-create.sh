#!/usr/bin/env bash
# Prepara o Codespace: ferramentas .NET e dependências dos fronts e dos testes E2E.
set -euo pipefail
dotnet tool restore
dotnet restore Kamus.slnx
for dir in apps/web apps/admin e2e; do
  (cd "$dir" && npm ci --no-audit --no-fund)
done
echo "Pronto. A stack sobe sozinha (docker compose); loja na porta 3000, backoffice na 3001."
