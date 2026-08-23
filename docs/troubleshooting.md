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
| `business-api` nie wstaje, `/health/ready` zwraca 503 albo połączenie jest odrzucane | Błąd migracji lub seeda. Deterministyczne błędy schematu **nie są ponawiane** — w logu jest jeden wpis `LogCritical` z podpowiedzią naprawy, a `/health/ready` zwraca `seedError` i `remediation`. Sprawdź `docker compose logs business-api \| grep -i critical`. |
| Po zmianie kodu UI/API kontener serwuje starą wersję | `docker compose up --build` przebudowuje obraz, ale **nie odtwarza kontenera**. Użyj `docker compose --profile demo up --build -d --force-recreate web business-api`. |
| Zmieniono dane w `packages/demo-data`, ale aplikacja pokazuje stare | Ponowny seed uruchamia się tylko przy zmianie stałej `SeedVersion` (porównywanej z tabelą `seed_metadata`). Podnieś wersję albo wykonaj **Resetuj demo**. |
| `dotnet ef` — „could not be found on the PATH" | To narzędzie **lokalne** (`apps/business-api/dotnet-tools.json`). Uruchamiaj z katalogu `apps/business-api`. |
| `?siteCode=` zwraca 404 lub 403 | 404 = nieznany kod zakładu; 403 = zakład poza zasięgiem konta (dostawca widzi tylko zakłady, do których dostarcza). Lista dostępnych zakładów: `GET /api/v1/auth/me` → `availableSites`. |
| Powiadomienia lub audyt pokazują zdarzenia z innego zakładu | Zachowanie zamierzone i udokumentowane: `/notifications` i `/audit` nie są zakresowane zakładem (patrz `docs/architecture/multi-site.md`). |
| Paszport ma status „Wygenerowany", ale nie ma PDF | Nie powinno wystąpić — seed odrzuca taki stan. Jeśli wystąpi, zgłoś: `GET /api/v1/passports/{serial}` z pustą listą `versions` oznacza błąd potoku renderowania. |
