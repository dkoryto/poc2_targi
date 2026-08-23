# Responsywność i obsługa dotykowa

Demonstrator jest projektowany pod monitor targowy 1920×1080, ale musi być **czytelny i w pełni funkcjonalny**
na tablecie i telefonie — na stoisku rozmówca często dostaje urządzenie do ręki. Poniższy kontrakt jest wiążący
dla wszystkich ekranów; nowe widoki muszą go spełniać od początku.

## Punkty łamania

| Nazwa | Zakres | Układ |
|---|---|---|
| `mobile` | < 768 px | jedna kolumna, nawigacja w szufladzie, tabele jako karty |
| `tablet` | 768–1199 px | dwie kolumny, zwinięty pasek nawigacji (rail) |
| `desktop` | ≥ 1200 px | pełny układ 2×2, rozwinięte menu (stan obecny) |
| `wall` | ≥ 1600 px | układ docelowy stoiska (bez zmian) |

Tokeny: `--bp-sm: 480px`, `--bp-md: 768px`, `--bp-lg: 1200px`, `--bp-xl: 1600px`.

## Reguły bezwzględne

1. **Strona nigdy nie przewija się w poziomie.** Szeroka treść (Gantt, tabele, heatmapa) przewija się
   wewnątrz własnego kontenera z `overflow-x: auto`, nigdy `body`.
2. **Żadna funkcja nie może zniknąć** przy małym ekranie. Element, który się nie mieści, przenosi się do
   menu nadmiarowego („⋯"), a nie jest ukrywany.
3. **Brak ucinania etykiet.** Nagłówki KPI i kart zawijają się; `text-overflow: ellipsis` dozwolony wyłącznie
   tam, gdzie pełna wartość jest dostępna w inny sposób (tooltip + odpowiednik dotykowy).
4. **Cele dotykowe ≥ 44×44 px**, odstęp ≥ 8 px. Minimalny rozmiar tekstu danych na mobile: 14 px.
5. **Brak interakcji wyłącznie hover.** Każdy tooltip/popover ma odpowiednik po tapnięciu.
6. Wysokości liczone przez `100dvh` (nie `100vh`); marginesy bezpieczne przez `env(safe-area-inset-*)`.
7. `prefers-reduced-motion` respektowany także dla przejść szuflady i arkuszy.
8. Oba motywy (jasny i ciemny) muszą być poprawne na każdym punkcie łamania.

## Wzorce (wspólne prymitywy w `src/components/ui`)

- **`Sheet`** — na mobile dialogi i szuflady stają się arkuszem pełnoekranowym (wysuwanym od dołu),
  z pułapką fokusu, zamykaniem gestem/Esc i przyciskiem zamknięcia w zasięgu kciuka.
- **`DataTable`** z `responsive="cards"` — poniżej `--bp-md` renderuje listę kart zamiast wierszy:
  pole wiodące jako tytuł, pozostałe jako pary etykieta/wartość, ten sam cel kliknięcia co wiersz.
  Sortowanie dostępne przez `<select>`; stany loading/empty/error zachowane.
- **`ScrollArea`** — kontener z własnym przewijaniem poziomym i widocznym cieniem krawędzi,
  używany przez Gantt i heatmapę.
- **`OverflowMenu`** — menu „⋯" w pasku górnym gromadzące akcje, które nie mieszczą się na mobile.
- **`FilterBar`** — na mobile filtry chowają się pod przyciskiem „Filtry" z licznikiem aktywnych filtrów.

## Wymagania per obszar

- **Pasek górny (mobile):** logo/skrót nazwy, status online, selektor zakładu, „⋯" (motyw, PL/EN,
  Uruchom demo, Resetuj demo, powiadomienia, użytkownik i rola). Zegar i wersja seeda mogą zniknąć.
- **Nawigacja (mobile):** szuflada spod przycisku hamburgera, pułapka fokusu, zamknięcie po wyborze trasy.
- **Control Room (mobile):** KPI jako siatka 2 kolumn z pełnymi etykietami (lub karuzela z przyciąganiem),
  panele jedna pod drugą, mapa min. 260 px z legendą zwijaną pod przyciskiem, Gantt w `ScrollArea`
  z przyklejoną kolumną nazw gniazd.
- **Gantt (mobile):** przewijanie poziome wewnątrz kontenera, dostępne przełączniki zakresu,
  alternatywny widok listy operacji dla bardzo wąskich ekranów.
- **Tabele (dostawy, partie, paszporty, audyt):** wzorzec `responsive="cards"`.
- **Planowanie (mobile):** kafle scenariuszy jedna kolumna, wynik Przed/Po jako przełącznik widoku
  (Przed | Po | Porównanie) zamiast dwóch Ganttów obok siebie.
- **Genealogia (mobile):** drzewo z wcięciami i zwijaniem, panel szczegółów jako `Sheet`.
- **Panel prezentera i tryb focus:** działają na mobile (arkusz pełnoekranowy).

## Weryfikacja

Każda zmiana UI sprawdzana przy szerokościach **390 px** (telefon), **768 px** (tablet pionowo),
**1024 px** (tablet poziomo) i **1920 px** (stoisko), w obu motywach, bez błędów w konsoli
i bez poziomego przewijania `body`.
