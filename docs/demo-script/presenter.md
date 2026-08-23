# Scenariusz prezentera (4–5 minut)

Przed rozpoczęciem: `./scripts/start.sh`, otwórz http://localhost:5173 (auto-login jako **DemoPresenter**), kliknij **Resetuj demo**, sprawdź wskaźnik „System online”. Przycisk **Uruchom demo** otwiera panel z tymi krokami (panel nie klika za Ciebie — tylko wskazuje kolejną akcję).

| # | Krok | Ekran / akcja | Co powiedzieć |
|---|---|---|---|
| 1 | **Control Room** | `/` — KPI: Material Readiness, OTIF ≈ 86 %, 3 dostawy wysokiego ryzyka, przestój 0 h. Mapa, heatmapa, Gantt, status jakości. | „Wszystkie kluczowe dostawy są zielone lub bursztynowe. Linia 2 czeka na siłowniki ACT-40 w tygodniu 2.” |
| 2 | **Zdarzenie** | Przełącz rolę → `supplier.hydromech` (SUP-02). `/supply` → PO-2026-0007, pozycja ACT-40 → **Zmień ETA** +10 dni, powód „opóźnienie produkcji”. | „Dostawca sam aktualizuje status, partię, ETA i dokumenty — bez telefonów i maili.” |
| 3 | **Reakcja na żywo** | Wróć do roli Planner/Presenter. Dashboard odświeża się przez SignalR: ryzyko PO-0007 **44 → 79 Krytyczne**, licznik ryzyk 3 → 4, przestój prognozowany **36 h**, Gantt oznacza `WO-2026-014` jako zagrożone. Kliknij znacznik na mapie → „Dlaczego ten wynik?”. | „Wynik jest regułowy i wyjaśnialny: 3 najważniejsze czynniki.” |
| 4 | **What-If** | `/planning` → kafel **„Opóźnij siłowniki ACT-40 o 10 dni”** → **Przelicz plan** (< 3 s). | „Silnik MRP uwzględnia BOM, zapasy, partie zablokowane, kalendarze gniazd i operacje zamrożone.” |
| 5 | **Porównanie** | Gantt **Przed / Po**: `WO-2026-014` +4 dni (brak 8 szt. ACT-40), `WO-2026-019` wciągnięte o ~29 dni na gniazdo integracji, przestój **36 h → 8 h**. Uzasadnienie pod Ganttem. **Zatwierdź** → nowa wersja planu + wpis w audycie. | „System proponuje, człowiek zatwierdza. Plan bazowy nigdy nie zmienia się bez decyzji planisty.” |
| 6 | **Traceability** | `/trace` → wyszukaj `PMV-2026-0007` → drzewo genealogii → partia `HTS-22-2608` → certyfikat SHA-256 → dostawca. | „Pełna ścieżka: dostawca → PO → wysyłka → partia → kontrola → zużycie → zlecenie → numer seryjny.” |
| 7 | **Paszport** | `/passports/PMV-2026-0007` → kontrola kompletności (DQP-01, wszystkie pozycje spełnione) → **Generuj paszport** → PDF z QR i SHA-256, wersja v2. Pokaż też `SCM-2026-0103` z listą braków. | „Jedno kliknięcie — dokumentacja odbiorowa gotowa. Niekompletny paszport mówi dokładnie, czego brakuje.” |
| 8 | **Podsumowanie** | Panel prezentera → **Podsumowanie wartości**: ryzyko wykryte 10 dni wcześniej, uniknięty przestój 28 h, 100 % identyfikowalności, paszport w 1 klik. | |

## Cztery zakłady — cztery historie

Selektor zakładu w pasku górnym przełącza kontekst całej aplikacji. Każdy zakład ma **scenariusz wiodący**
(wyróżniony kafel na ekranie Planowania), więc na stoisku można pokazać cztery różne rozmowy bez resetu:

| Zakład | Profil | Scenariusz wiodący | Do kogo mówi |
|---|---|---|---|
| **Zakład Kielce** (`SITE-01`) | montaż i integracja platform | opóźnienie siłowników `ACT-40` o 10 dni | pełna ścieżka: ryzyko → What-If → paszport (scenariusz główny powyżej) |
| **Zakład Piła** (`SITE-02`) | elektronika i łączność | opóźnienie modułów `MCU-X7` o 14 dni | szef produkcji elektroniki — wąskie gardło komponentów |
| **Zakład Zamość** (`SITE-03`) | konstrukcje i opancerzenie | blokada partii stali | szef jakości — trace-forward i unieważnienie paszportów |
| **Zakład Leszno** (`SITE-04`) | integracja i testy końcowe | ograniczenie pojemności gniazda integracji o 50 % | dyrektor operacyjny — utrata zdolności produkcyjnej |

Wskazówka dla prezentera: zacznij od **Kielc** (pełna ścieżka wartości), a pozostałe zakłady trzymaj jako
odpowiedź na pytanie „a jak to wygląda u nas?" — wystarczy przełączyć zakład i kliknąć wyróżniony kafel.
Szczegóły danych: `docs/architecture/multi-site.md`.

## Scenariusz dodatkowy (1 min): blokada partii

`/trace/lots/HTS-22-2608` → **Zablokuj partię** (powód: NCR jakościowy) → trace-forward: `WO-2026-011` (PMV-2026-0007/0008 → paszporty **Unieważnione**), `WO-2026-018` (rezerwacja zagrożona, brak 400 kg do ETA PO-2026-0013). Dashboard: paszporty wymagające działania 2, partie zablokowane 2.

## Reset

Przycisk **Resetuj demo** (< 10 s) lub `./scripts/reset-demo.sh`. Stan po resecie jest identyczny (identyfikatory deterministyczne, daty względem poniedziałku bieżącego tygodnia).

## Jeśli coś pójdzie nie tak

- Brak odświeżenia na żywo → wskaźnik w pasku pokaże „Tryb offline”; odśwież stronę (F5) — dane i tak są w bazie.
- Silnik Java nie odpowiada → wynik oznaczony **„Heuristic fallback”**, demo idzie dalej.
- Więcej: `docs/troubleshooting.md`.
