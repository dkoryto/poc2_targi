# Do zrobienia

Stan na 23.08.2026. Pozycje zweryfikowane na uruchomionym środowisku, nie z pamięci —
przy każdej podano, skąd wiadomo, że problem istnieje.

Stan testów w chwili spisania: .NET **142** (47 domain + 95 API), web **78**, E2E **44/47**.
Trzy padające testy E2E to pozycja [B1] — są celowe i opisują poprawne zachowanie.

---

## A. Blokujące demonstrację

Brak. Obie ścieżki targowe przechodzą end-to-end na wszystkich czterech zakładach.

---

## B. Widoczne dla widza na stoisku

### B1. Kafel „Zwiększ priorytet zlecenia" nie robi nic na trzech zakładach
**Dowód:** `tests/e2e/specs/09-qa-scenarios.spec.ts` — 3 testy padają dla `SITE-02`, `SITE-03`, `SITE-04`.
Na Kielcach naprawione (3 przesunięte operacje).
**Przyczyna:** plany bazowe tych zakładów nie mają zapasu czasu ani konkurencji o gniazdo, więc podniesienie
priorytetu nie ma czego przestawić. To brak w danych, nie w kodzie — silnik i preset działają poprawnie.
**Zakres:** dla każdego z trzech zakładów dodać w `packages/demo-data/plants.json` zlecenie z realnym zapasem
czasu, konkurujące o gniazdo ze zleceniem o wyższym priorytecie; po każdej zmianie przeliczyć scenariusz
na żywym silniku. **Kielce muszą zostać nietknięte** — od ich liczb zależą fikstury silnika, skrypt
prezentera i cały zestaw E2E.
**Ryzyko:** średnie. Łatwo przy okazji zepsuć inny scenariusz tego samego zakładu.

### B2. „Bez zmian" mimo przesuniętych operacji na Gantcie
**Dowód:** Zamość, kafel priorytetu — 4 operacje przesunięte, komunikat `NO_CHANGE`.
**Przyczyna:** obsłużono wyłącznie przypadek wciągnięcia zlecenia do przodu; przesunięcie wyłącznie w tył
nie ma własnego kodu uzasadnienia.
**Zakres:** nowy kod powodu (np. `ORDER_PUSHED_BACK`) w `ScenarioService`, komunikaty PL/EN w `apps/web/src/i18n`.
Mały, dobrze odgraniczony.

### B3. Zrzuty ekranu w dokumentacji są sprzed ostatnich poprawek
**Dowód:** `docs/screenshots/` (16 plików, 3,8 MB) powstało przed poprawkami etykiet mapy, oznaczeń zakładu
na rekordach i tytułów kafli — pokazują poprzedni wygląd.
**Zakres:** `node docs/screenshots/capture.mjs` po resecie demo; sprawdzić, czy nic nie zmieniło układu.

### B4. Skrypt prezentera nie ma zrzutów przy krokach
**Dowód:** `grep -c screenshots docs/demo-script/presenter.md` → 0. README linkuje galerię (4 odwołania),
skrypt prezentera nie — ta część zadania nie została dokończona.
**Zakres:** wstawić po jednym zrzucie przy każdym z ośmiu kroków scenariusza.

---

## C. Poprawność i spójność

### C1. Powiadomienia i audyt nie są zakresowane zakładem
**Dowód:** `GET /notifications?siteCode=SITE-01` i `?siteCode=SITE-03` zwracają identyczną treść; to samo dla `/audit`.
**Przyczyna:** żadna z encji nie ma `SiteId` — powiadomienia adresują rolę, wpisy audytu odnoszą się do encji
ze wszystkich modułów.
**Zakres:** kolumna `SiteId` (nullable) w obu, migracja z backfillem, ustawianie przy zapisie tam, gdzie kontekst
jest znany, filtrowanie w zapytaniach. Wpisy bez zakładu (logowanie, operacje globalne) traktować jako widoczne
wszędzie. Udokumentowane w `docs/adr/0007-multi-site-scoping.md` jako świadomie odłożone.

### C2. Scenariusz zostaje w stanie `Running` po restarcie API w trakcie przeliczania
**Przyczyna:** kolejka scenariuszy działa w procesie (`ScenarioRunQueue`).
**Zakres:** przy starcie oznaczyć osierocone scenariusze jako `Failed` z czytelnym powodem, albo wznowić.
Na stoisku nieistotne, przy dłuższej pracy — mylące.

### C3. Czas solvera zbierany, ale nieujawniony
**Dowód:** `/admin/status` zwraca `services`, `recentErrors`, `serverTime`, `version` — brak metryk.
`PlanningEngineMetrics` liczy opóźnienie i liczbę użyć fallbacku, nikt tego nie czyta.
**Zakres:** dopisać do `/admin/status` i pokazać na ekranie administratora.

### C4. Wersję seeda trzeba podbijać ręcznie
**Przyczyna:** ponowny seed po aktualizacji wyzwala zmiana stałej `SeedVersion`. Zmiana danych bez podbicia
stałej oznacza, że zaktualizowana instalacja po cichu zachowa stare dane.
**Zakres:** wyliczać wersję z sumy kontrolnej plików `packages/demo-data/*.json`.

---

## D. Wdrożenie

### D1. Wdrożenie przetestowane wyłącznie lokalnie
**Dowód:** próba generalna przeszła pod `dspc.localhost` z certyfikatem wewnętrznym Caddy (`tls internal`).
Ścieżka z prawdziwą domeną i Let's Encrypt **nie została wykonana** — wymaga publicznego DNS i portu 80.
**Zakres:** jedno wdrożenie na serwerze z realną domeną; zweryfikować wydanie i odnowienie certyfikatu.

### D2. Brak automatycznych kopii zapasowych
`docs/deployment.md` §7 podaje polecenia ręczne. Do dłuższego działania: zadanie cykliczne i sprawdzenie odtwarzania.

### D3. Skaner plików to atrapa
`NoOpFileScanner` tylko loguje. Przed jakimkolwiek użyciem produkcyjnym podpiąć realny silnik (ClamAV/ICAP).
Opisane w `SECURITY.md`.

---

## E. Dług techniczny

### E1. Bundle frontendu ~1,7 MB (419 KB gzip)
Głównie MapLibre. Podział kodu (`manualChunks`) skróciłby pierwsze wejście. Na stoisku bez znaczenia.

### E2. Brak przyklejonej kolumny gniazd przy poziomym przewijaniu Ganttu na telefonie
Wymaga rozbicia SVG na stałą kolumnę etykiet i przewijalną resztę. Obejście działa: przełącznik
„wykres / lista operacji" dla wąskich ekranów.

### E3. Adapter lokalnego modelu AI nieprzetestowany z prawdziwym modelem
Domyślnie wyłączony (`LOCAL_AI_ENABLED=false`), symulator zwraca deterministyczną odpowiedź z fikstury.
Przed włączeniem: sprawdzić z realnym endpointem zgodnym z OpenAI API i ocenić ryzyko wstrzyknięcia
przez treść dokumentu.

### E4. QuestPDF na licencji Community
Darmowa poniżej progu przychodowego. Przed zastosowaniem komercyjnym zweryfikować próg — `docs/licenses.md`.

---

## F. Świadome decyzje projektowe — nie do „naprawy"

Zapisane, żeby nie wracały jako zgłoszenia:

- **W trybie `Heuristic fallback` plan „Po" równa się planowi „Przed"** — lokalna heurystyka nie wciąga zleceń
  do przodu. Celowe, opisane w `docs/adr/0005-scenario-execution-and-fallback.md`.
- **Scenariusz Leszna pogarsza terminowość** (7 → 21 dni). Poprawny wynik twardego ograniczenia pojemności:
  „odcięcie połowy gniazda kosztuje 21 dni". Nie jest to historia „solver poprawia plan".
- **What-If `BLOCK_LOT` nie zmienia statusu partii** — to symulacja. Paszporty unieważnia dopiero realna
  blokada przyciskiem na ekranie partii.
- **Hasło kont produkcyjnych nie jest `demo`** — obowiązuje `DEMO_ACCOUNT_PASSWORD`, hashowane przy seedzie.
