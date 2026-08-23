# Wdrożenie produkcyjne pod domeną

Instrukcja uruchomienia demonstratora na serwerze, pod własną domeną, z certyfikatem TLS.
Pliki: `docker-compose.prod.yml`, `.env.prod.example`, `infrastructure/production/Caddyfile`.

> **Zanim zaczniesz — decyzja o trybie demo.** W trybie targowym (`DEMO_ENABLED=true`) aplikacja loguje
> użytkownika automatycznie, pozwala przełączać role bez hasła i resetować dane. To jest wygodne na stoisku
> i **nie może być wystawione publicznie**. Domyślna konfiguracja produkcyjna wyłącza te funkcje
> (`DEMO_ENABLED=false`): logowanie odbywa się hasłem, endpointy demo zwracają 404. Jeśli mimo to chcesz
> pełny tryb targowy pod domeną, włącz bramkę HTTP w Caddy (sekcja „Ograniczenie dostępu").

## 1. Wymagania

- Serwer z publicznym adresem IP, Docker Engine 24+ i wtyczką Compose.
- ~4 GB RAM i ~10 GB dysku (obrazy, baza, dokumenty).
- Rekord **A** (i opcjonalnie **AAAA**) domeny wskazujący na ten serwer — musi już działać, zanim wystartujesz,
  bo Let's Encrypt weryfikuje domenę przez port 80.
- Porty **80** i **443** otwarte z internetu. Port 80 jest wymagany także po wydaniu certyfikatu, do jego odnawiania.

Sprawdzenie DNS przed startem:

```bash
dig +short demo.twojafirma.pl        # musi zwrócić IP serwera
```

## 2. Konfiguracja

```bash
git clone <repo> dspc && cd dspc
cp .env.prod.example .env.prod
```

Uzupełnij `.env.prod`. Każdy sekret wygeneruj — Compose odmówi startu, jeśli któryś jest pusty:

```bash
openssl rand -base64 48 | tr -d '\n'              # JWT_KEY
openssl rand -base64 24 | tr -dc 'A-Za-z0-9'      # POSTGRES_PASSWORD, MINIO_ROOT_PASSWORD
```

Pola wymagające uwagi:

| Zmienna | Znaczenie |
|---|---|
| `DSPC_DOMAIN` | Domena, pod którą serwis odpowiada, np. `demo.twojafirma.pl` |
| `DSPC_EXTRA_HOSTS` | Dodatkowe nazwy, np. `www.demo.twojafirma.pl`. **Nazwa spoza tej listy kończy się błędem `ERR_SSL_PROTOCOL_ERROR`** — patrz „Rozwiązywanie problemów" |
| `DSPC_ACME_EMAIL` | Adres do powiadomień Let's Encrypt o wygasających certyfikatach |
| `DSPC_TLS_MODE` | **Zostaw puste w produkcji** — Caddy sam pobierze certyfikat. `tls internal` służy wyłącznie do próby lokalnej |
| `DEMO_ACCOUNT_PASSWORD` | Hasło wszystkich kont demonstracyjnych. Nie zostawiaj wbudowanego `demo` |
| `DEMO_ENABLED` | `false` w produkcji |
| `SEED_FORCE` | `true` — bez tego baza zmigruje się pusta i **nie będzie żadnych kont do zalogowania** |
| `TRUSTED_PROXY_NETWORKS` | Sieć Dockera (`172.16.0.0/12`). Dzięki temu limity zapytań i audyt widzą adres klienta, a nie proxy |

## 3. Uruchomienie

```bash
docker compose -f docker-compose.prod.yml --env-file .env.prod up -d --build
```

Pierwszy start buduje obrazy (kilka minut), migruje bazę i wykonuje seed danych demonstracyjnych
(4 zakłady, ~2–3 s). Caddy w tym czasie pobiera certyfikat.

Obserwacja postępu:

```bash
docker compose -f docker-compose.prod.yml --env-file .env.prod ps
docker compose -f docker-compose.prod.yml --env-file .env.prod logs -f caddy business-api
```

Gotowe, gdy `business-api` ma stan `healthy`, a w logach Caddy pojawi się `certificate obtained successfully`.
Wejdź na `https://<DSPC_DOMAIN>`.

Weryfikacja z linii poleceń:

```bash
curl -I https://demo.twojafirma.pl                       # 200, nagłówek Strict-Transport-Security
curl -I http://demo.twojafirma.pl                        # 308 -> https
curl -o /dev/null -w '%{http_code}\n' https://demo.twojafirma.pl/swagger   # 404, dokumentacja API zablokowana
```

## 4. Logowanie

Konta pochodzą z seeda, hasłem jest `DEMO_ACCOUNT_PASSWORD` z `.env.prod`:
`director`, `planner`, `quality`, `inbound`, `auditor`, `admin`, `presenter`
oraz konta dostawców `supplier.hydromech`, `supplier.nordstal`, `supplier.vistula`.
Pełna lista ról: [`docs/demo-script/accounts.md`](demo-script/accounts.md).

Zmiana hasła: zmień `DEMO_ACCOUNT_PASSWORD`, następnie odtwórz dane
(`docker compose -f docker-compose.prod.yml --env-file .env.prod down` , usuń wolumen `dspc-prod_pgdata`, uruchom ponownie).

## 5. Ograniczenie dostępu

Demonstrator zawiera fikcyjne dane, ale zwykle nie ma powodu wystawiać go anonimowo. Najprostsza bramka
to HTTP Basic w Caddy — odkomentuj blok `basic_auth` w `infrastructure/production/Caddyfile` i ustaw:

```bash
docker run --rm caddy caddy hash-password --plaintext 'twoje-haslo'
# wynik wpisz do DSPC_BASIC_AUTH_HASH, login do DSPC_BASIC_AUTH_USER
docker compose -f docker-compose.prod.yml --env-file .env.prod up -d --force-recreate caddy
```

Dopiero z taką bramką ma sens `DEMO_ENABLED=true` pod publiczną domeną.

## 6. Aktualizacja

```bash
git pull
docker compose -f docker-compose.prod.yml --env-file .env.prod up -d --build
```

Migracje bazy wykonują się automatycznie przy starcie. Jeśli zmieniły się dane demonstracyjne,
seed odtworzy je, gdy zmieni się wersja seeda (`Seed:Force` + znacznik `seed_metadata`).
Aktualizacja nie kasuje wolumenów.

## 7. Kopie zapasowe

Dane trwałe to dwa wolumeny: `dspc-prod_pgdata` (baza) i `dspc-prod_miniodata` (dokumenty i paszporty PDF).

```bash
# baza
docker compose -f docker-compose.prod.yml --env-file .env.prod exec -T postgres \
  pg_dump -U dspc dspc | gzip > dspc-$(date +%F).sql.gz

# dokumenty
docker run --rm -v dspc-prod_miniodata:/data -v "$PWD":/backup alpine \
  tar czf /backup/dspc-minio-$(date +%F).tar.gz -C /data .
```

## 8. Rozwiązywanie problemów

| Objaw | Przyczyna i rozwiązanie |
|---|---|
| **`ERR_SSL_PROTOCOL_ERROR`** | Wszedłeś pod nazwą, której ta instalacja nie obsługuje (np. `https://localhost` albo adres IP). Caddy ma certyfikat wyłącznie dla `DSPC_DOMAIN`, więc uzgodnienie TLS nie ma czego przedstawić. Użyj właściwej domeny albo dopisz nazwę do `DSPC_EXTRA_HOSTS` i odtwórz kontener `caddy`. Wejście po HTTP pod nieobsługiwaną nazwą zwraca komunikat wyjaśniający (421) |
| Certyfikat nie zostaje wydany | Sprawdź, czy DNS wskazuje na serwer i czy port 80 jest otwarty z internetu. W logach Caddy szukaj `challenge failed`. Do testów odkomentuj `acme_ca` (środowisko staging), żeby nie wyczerpać limitów Let's Encrypt |
| Komunikat o nieprawidłowej nazwie lub haśle przy koncie, które działa na stoisku | Wdrożenie produkcyjne **nie używa** wbudowanego hasła `demo` — obowiązuje `DEMO_ACCOUNT_PASSWORD` z `.env.prod`. Hasło jest zapisywane jako hash podczas seeda, więc jego zmiana zaczyna działać dopiero po ponownym seedzie: zatrzymaj stack, usuń wolumen `dspc-prod_pgdata`, uruchom ponownie |
| Aplikacja wstaje, ale nie da się zalogować | `SEED_FORCE=false` przy `DEMO_ENABLED=false` — baza jest pusta, bo konta pochodzą z seeda. Ustaw `SEED_FORCE=true` i zrestartuj `business-api` |
| `business-api` nie osiąga stanu healthy | `docker compose ... logs business-api`. Deterministyczny błąd schematu kończy się jednym komunikatem `LogCritical` z podpowiedzią; `/health/ready` zwraca pole `remediation` |
| Limity zapytań blokują wielu użytkowników naraz | `TRUSTED_PROXY_NETWORKS` nie obejmuje sieci Dockera, więc wszyscy dzielą jeden kubełek per adres proxy. Ustaw właściwy zakres |
| Brak podglądu na żywo (pasek pokazuje „Tryb offline") | Proxy musi przepuszczać WebSocket na `/hubs`. Caddy robi to domyślnie; jeśli stoi przed nim kolejne proxy, sprawdź jego konfigurację |

Pozostałe objawy: [`docs/troubleshooting.md`](troubleshooting.md).

## 9. Czego ta konfiguracja nie zapewnia

Wymienione w [`SECURITY.md`](../SECURITY.md) i [`docs/architecture/demo-vs-production.md`](architecture/demo-vs-production.md):
brak MFA i zewnętrznego dostawcy tożsamości, mock skanera antywirusowego, brak wysokiej dostępności,
brak retencji i automatycznych kopii zapasowych, QuestPDF na licencji Community.
