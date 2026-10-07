#!/usr/bin/env bash
# Roda os testes E2E e a regressão visual numa stack Docker isolada e com banco zerado.
#   ./e2e/run.sh            compara as telas com as imagens de apps/*/design
#   ./e2e/run.sh --update   aceita as telas atuais como novas referências (mudança intencional)
# Argumentos extras são repassados ao Playwright (ex.: ./e2e/run.sh --grep vitrine).
set -euo pipefail
cd "$(dirname "$0")/.."

args=()
for arg in "$@"; do
  if [[ "$arg" == "--update" ]]; then args+=("--update-snapshots=all"); else args+=("$arg"); fi
done
export PLAYWRIGHT_ARGS="${args[*]:-}"

compose=(docker compose -p kamus-e2e -f docker-compose.yml -f e2e/docker-compose.e2e.yml ${E2E_COMPOSE_EXTRA:-})

"${compose[@]}" down -v --remove-orphans >/dev/null 2>&1 || true
status=0
"${compose[@]}" up --build --abort-on-container-exit --exit-code-from playwright || status=$?
"${compose[@]}" down -v --remove-orphans >/dev/null 2>&1 || true

if [[ $status -ne 0 ]]; then
  echo "E2E falhou. Relatório: e2e/playwright-report/index.html (diferenças visuais em e2e/test-results/)"
fi
exit $status
