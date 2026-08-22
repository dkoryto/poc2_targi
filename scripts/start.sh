#!/usr/bin/env bash
# One-shot start for Linux/macOS: builds images, starts the demo profile, waits for health, runs smoke test.
set -euo pipefail
cd "$(dirname "$0")/.."
[ -f .env ] || cp .env.example .env
PROFILE="${1:-demo}"
echo "▶ docker compose --profile $PROFILE up --build -d"
docker compose --profile "$PROFILE" up --build -d
WEB_PORT=$(grep -E '^WEB_PORT=' .env | cut -d= -f2); WEB_PORT=${WEB_PORT:-5173}
API_PORT=$(grep -E '^API_PORT=' .env | cut -d= -f2); API_PORT=${API_PORT:-5080}
echo "▶ waiting for business-api readiness on :$API_PORT"
for i in $(seq 1 90); do
  if curl -fs "http://localhost:$API_PORT/health/ready" >/dev/null 2>&1; then echo "  ready"; break; fi
  sleep 2
  [ "$i" -eq 90 ] && { echo "business-api not ready — see: docker compose logs business-api"; exit 1; }
done
if [ "$PROFILE" = "demo" ]; then
  echo "▶ smoke test"
  ./scripts/smoke.sh "http://localhost:$API_PORT" "http://localhost:$WEB_PORT" || { echo "smoke test FAILED"; exit 1; }
  echo
  echo "✔ Demo ready: http://localhost:$WEB_PORT  (API: http://localhost:$API_PORT/swagger)"
fi
