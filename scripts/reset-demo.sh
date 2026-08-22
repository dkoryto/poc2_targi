#!/usr/bin/env bash
# Resets demo data to the seeded state via the API.
set -euo pipefail
API="${1:-http://localhost:5080}"
TOKEN=$(curl -fs "$API/api/v1/auth/demo-login?role=DemoPresenter" | sed -n 's/.*"accessToken":"\([^"]*\)".*/\1/p')
curl -fs -X POST -H "Authorization: Bearer $TOKEN" "$API/api/v1/demo/reset"; echo
