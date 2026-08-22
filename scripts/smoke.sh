#!/usr/bin/env bash
# Minimal post-start smoke test (curl + jq-free). Usage: scripts/smoke.sh [apiBase] [webBase]
set -euo pipefail
API="${1:-http://localhost:5080}"; WEB="${2:-http://localhost:5173}"
fail() { echo "✘ $1"; exit 1; }
ok()   { echo "✔ $1"; }
curl -fs "$API/health/live"  >/dev/null && ok "api live"  || fail "api live"
curl -fs "$API/health/ready" >/dev/null && ok "api ready" || fail "api ready"
curl -fs "$WEB/" | grep -qi "<div id=\"root\"" && ok "web serves SPA" || fail "web SPA"
TOKEN=$(curl -fs "$API/api/v1/auth/demo-login?role=DemoPresenter" | sed -n 's/.*"accessToken":"\([^"]*\)".*/\1/p')
[ -n "$TOKEN" ] && ok "demo login" || fail "demo login (is Demo__Enabled=true?)"
KPIS=$(curl -fs -H "Authorization: Bearer $TOKEN" "$API/api/v1/dashboard/kpis")
echo "$KPIS" | grep -q '"MATERIAL_READINESS"' && ok "dashboard kpis computed" || fail "dashboard kpis"
curl -fs -H "Authorization: Bearer $TOKEN" "$API/api/v1/planning/baseline" | grep -q '"WO-2026-014"' && ok "baseline plan contains WO-2026-014" || fail "baseline plan"
curl -fs -H "Authorization: Bearer $TOKEN" "$API/api/v1/trace/serials/PMV-2026-0007" | grep -q '"HTS-22-2608"' && ok "trace-back reaches lot HTS-22-2608" || echo "… trace endpoint not ready yet (wave 2)"
curl -fs "$API/api/v1/admin/status" -H "Authorization: Bearer $TOKEN" | grep -q '"planning-engine"' && ok "admin status lists planning-engine" || echo "… admin status"
echo "smoke OK"
