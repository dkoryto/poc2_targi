# Wiele zakładów (multi-site)

Demonstrator prezentuje cztery zakłady jednej fikcyjnej organizacji `ORG-01`. Każdy ma własne gniazda robocze,
linie, zlecenia produkcyjne, zamówienia zakupu, partie, plan bazowy i **własny scenariusz wiodący**, dzięki czemu
na stoisku można pokazać cztery różne historie bez resetu między nimi.

> Nazwy zakładów i wszystkie dane są fikcyjne; użyto nazw istniejących miast wyłącznie jako czytelnych etykiet na mapie.

| Kod | Nazwa | Miasto (lat, lon) | Profil | Scenariusz wiodący |
|---|---|---|---|---|
| `SITE-01` | Zakład Kielce | Kielce (50.87, 20.63) | montaż i integracja platform | `DELAY_ACT40_10D` — opóźnienie siłowników ACT-40 o 10 dni |
| `SITE-02` | Zakład Piła | Piła (53.15, 16.74) | elektronika i łączność | `DELAY_MCUX7_14D` — opóźnienie modułów MCU-X7 o 14 dni |
| `SITE-03` | Zakład Zamość | Zamość (50.72, 23.25) | konstrukcje i opancerzenie | `BLOCK_LOT_HTS22` — blokada partii stali, unieważnienie paszportów |
| `SITE-04` | Zakład Leszno | Leszno (51.84, 16.58) | integracja i testy końcowe | `CAPACITY_INT_50` — ograniczenie pojemności gniazda integracji o 50 % |

## Zasada nadrzędna

**`SITE-01` pozostaje bez zmian merytorycznych** (poza współrzędnymi i nazwą): te same kody gniazd (`WC-CUT`…`WC-TEST`),
zleceń (`WO-2026-011`…`019`), zamówień (`PO-2026-0001`…`0018`), partii i paszportów oraz **te same liczby**
(ryzyko 44 → 79, przestój 36 → 8 h, `WO-2026-014` +4 dni, `WO-2026-019` wciągnięte). Scenariusz targowy,
`docs/architecture/demo-scenario.md`, fikstury silnika i wszystkie testy E2E odnoszą się do `SITE-01`.

## Przestrzenie kodów (unikalne globalnie)

| Zasób | SITE-01 | SITE-02 | SITE-03 | SITE-04 |
|---|---|---|---|---|
| Gniazda | `WC-*` | `PIL-WC-*` | `ZAM-WC-*` | `LES-WC-*` |
| Linie | `LINE-1/2` | `PIL-LINE-1/2` | `ZAM-LINE-1/2` | `LES-LINE-1/2` |
| Zlecenia | `WO-2026-0xx` | `WO-2026-1xx` | `WO-2026-2xx` | `WO-2026-3xx` |
| Zamówienia | `PO-2026-0xxx` | `PO-2026-1xxx` | `PO-2026-2xxx` | `PO-2026-3xxx` |
| Numery seryjne | jak dotąd | sufiks `-P` | sufiks `-Z` | sufiks `-L` |

Dostawcy, definicje części, produkty i BOM-y są **wspólne** dla organizacji — ten sam dostawca zaopatruje kilka
zakładów, co jest widoczne na mapie i w heatmapie ryzyka.

## Zakres danych pozostałych zakładów

Mniejszy niż `SITE-01`, tak aby seed i `POST /demo/reset` mieściły się w budżecie < 10 s: na zakład 3 gniazda,
2 linie, 5–7 zleceń, 6–9 zamówień (w tym 1–2 ryzykowne), 8–12 partii (w tym jedna problematyczna w `SITE-03`),
plan bazowy 12 tygodni, 1 gotowy i 1 niekompletny paszport.

## Kontrakt API

- `GET /api/v1/sites` → `[{ code, name, city, country, lat, lon, timeZone, profileKey, featuredScenarioKey, isDefault }]`
- `GET /api/v1/auth/me` → dodatkowo `siteCode` (domyślny zakład użytkownika) oraz `availableSites: string[]`
  (dla `SupplierUser` — zakłady, do których dostarcza; dla pozostałych ról — wszystkie).
- Wszystkie endpointy listujące i pulpitowe przyjmują opcjonalny `?siteCode=`; brak parametru = zakład domyślny
  użytkownika. Dotyczy: `/dashboard/*`, `/purchase-orders`, `/shipments`, `/logistics-events`, `/inventory`,
  `/lots`, `/non-conformances`, `/passports`, `/notifications`, `/planning/baseline`, `/planning/scenarios`,
  `/planning/scenarios/presets`, `/trace/search`, `/audit`.
- `GET /planning/scenarios/presets?siteCode=` → presety dotyczące **tego** zakładu; dokładnie jeden ma
  `featured: true` (scenariusz wiodący z tabeli powyżej).
- `POST /planning/scenarios` — zakład wynika ze zmian (pozycje zamówień/partie/gniazda należą do jednego zakładu);
  próba zmieszania zakładów w jednym scenariuszu → `400` z Problem Details.
- Plan bazowy, KPI i przestój liczone są **per zakład**; `PlanningBaseline` ma `SiteId`.
- Nieznany `siteCode` → `404`; `siteCode` spoza `availableSites` użytkownika → `403`.

## UI

Selektor zakładu w pasku górnym przełącza kontekst całej aplikacji (KPI, mapa, Gantt, dostawy, jakość, planowanie),
wybór jest zapamiętywany w `localStorage`. Mapa centruje się na wybranym zakładzie i pokazuje trasy do niego,
a pozostałe zakłady jako drugorzędne znaczniki. Na ekranie planowania kafel scenariusza wiodącego danego zakładu
jest wyróżniony.
