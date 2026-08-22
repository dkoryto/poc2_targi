# Troubleshooting

| Objaw | Przyczyna / rozwiązanie |
|---|---|
| `docker compose up` kończy się na `business-api` z błędem połączenia do bazy | Postgres jeszcze nie był gotowy — Compose czeka na healthcheck; uruchom ponownie `./scripts/start.sh`. Sprawdź `docker compose logs postgres`. |
| Strona otwiera się, ale login nie działa / brak auto-loginu | `DEMO_ENABLED=false` w `.env` lub `ASPNETCORE_ENVIRONMENT` ≠ `Demo`. Ustaw `DEMO_ENABLED=true` i zrestartuj `business-api`. |
| „Tryb offline” w pasku | Połączenie SignalR zerwane. Odśwież stronę. Jeśli trwa: `docker compose logs business-api`, proxy nginx musi przepuszczać WebSocket (`/hubs`). |
| Scenariusz What-If oznaczony `Heuristic fallback` | `planning-engine` nie odpowiedział w 3 s lub jest wyłączony. `docker compose ps planning-engine`, `curl localhost:8081/actuator/health`. Demo działa dalej. |
| Generowanie PDF kończy się 422 | Paszport niekompletny — lista braków w odpowiedzi / na ekranie. Uzupełnij inspekcję lub dokument. |
| Upload dokumentu odrzucony | Dozwolone pdf/png/jpg ≤ 10 MB; MIME musi zgadzać się z rozszerzeniem. |
| Reset demo trwa > 10 s | Pierwszy reset po zimnym starcie może być wolniejszy (JIT). Kolejne < 10 s. Sprawdź obciążenie dysku Dockera. |
| Port 5173/5080/5432/9000 zajęty | Zmień `WEB_PORT`/`API_PORT` w `.env`; dla Postgres/MinIO edytuj mapowania w `docker-compose.yml`. |
| Mapa pusta | Plik `apps/web/public/geo/europe.geojson` nie został zbudowany do obrazu — `docker compose build web`. Mapa nie wymaga internetu. |
| Daty na dashboardzie „w przeszłości” | Seed liczony od poniedziałku bieżącego tygodnia; po długim działaniu kontenera wykonaj **Resetuj demo**. |
| Testy integracyjne .NET nie startują | Testcontainers potrzebuje Dockera; alternatywnie ustaw `ConnectionStrings__Test`. |
