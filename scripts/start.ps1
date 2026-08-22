# Windows / PowerShell equivalent of scripts/start.sh
$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")
if (-not (Test-Path ".env")) { Copy-Item ".env.example" ".env" }
docker compose --profile demo up --build -d
$apiPort = 5080; $webPort = 5173
Write-Host "Waiting for business-api on :$apiPort ..."
$ready = $false
for ($i = 0; $i -lt 90 -and -not $ready; $i++) {
  try { Invoke-WebRequest -UseBasicParsing "http://localhost:$apiPort/health/ready" | Out-Null; $ready = $true } catch { Start-Sleep 2 }
}
if (-not $ready) { throw "business-api not ready - run: docker compose logs business-api" }
$login = Invoke-RestMethod "http://localhost:$apiPort/api/v1/auth/demo-login?role=DemoPresenter"
$kpis = Invoke-RestMethod -Headers @{ Authorization = "Bearer $($login.accessToken)" } "http://localhost:$apiPort/api/v1/dashboard/kpis"
if (-not ($kpis.items | Where-Object code -eq "MATERIAL_READINESS")) { throw "KPIs missing" }
Write-Host "Demo ready: http://localhost:$webPort  (API: http://localhost:$apiPort/swagger)"
