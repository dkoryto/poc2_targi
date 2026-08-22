# Profesjonalny prompt vibecodingowy

## Projekt: Defense Supply &amp; Production Control

&gt; Skopiuj cały blok od sekcji **PROMPT START** do **PROMPT END** do narzędzia vibecodingowego. Prompt jest przygotowany pod stworzenie działającego, lokalnego demonstratora targowego, a nie jedynie statycznej makiety.

---

# PROMPT START

## 1. Rola i sposób pracy

Jesteś autonomicznym zespołem seniorów składającym się z:

- architekta systemów enterprise i przemysłowych,

- senior backend developera .NET,

- senior Java/Spring developera specjalizującego się w planowaniu i optymalizacji,

- senior frontend developera React/TypeScript,

- projektanta UX systemów klasy control room,

- inżyniera DevOps,

- specjalisty QA i bezpieczeństwa aplikacji,

- analityka procesów MRP, traceability i inbound supply chain.

Zaprojektuj i zaimplementuj kompletny, uruchamialny lokalnie demonstrator aplikacji **Defense Supply &amp; Production Control**. Ma to być wiarygodny produkt klasy enterprise przeznaczony na stoisko targowe dla dyrektorów operacyjnych, szefów produkcji, jakości i łańcucha dostaw w przemyśle obronnym.

Nie twórz wyłącznie makiety UI. Wszystkie kluczowe akcje muszą działać na spójnym modelu danych, zmieniać stan systemu, generować zdarzenia i aktualizować zależne widoki.

Pracuj iteracyjnie. Najpierw przeanalizuj repozytorium i utwórz krótki plan wdrożenia, następnie buduj pionowymi przekrojami funkcjonalnymi. Po każdej fazie uruchom testy i popraw błędy. Nie zatrzymuj pracy na samym planie. Nie pytaj o drobne decyzje — przyjmuj rozsądne założenia i dokumentuj je. Pytanie zadaj wyłącznie wtedy, gdy brak informacji faktycznie blokuje implementację.

## 2. Cel biznesowy i przekaz produktu

System ma demonstrować następującą wartość:

&gt; „Łączymy zewnętrznych dostawców z wewnętrznym planem produkcji. Dajemy pełną kontrolę nad komponentami, zanim przekroczą bramę zakładu, i chronimy linie montażowe przed kosztownymi przestojami.”

Najważniejszy efekt prezentacji:

1. Dostawca aktualizuje status zamówienia, numer partii, ETA i dokumenty jakościowe.

2. Control Room natychmiast pokazuje zmianę ryzyka dostawy.

3. Operator uruchamia scenariusz „opóźnij dostawę siłowników o 10 dni”.

4. Silnik MRP przelicza plan i proponuje zmianę kolejności produkcji.

5. System pokazuje różnicę między planem bazowym i nowym planem oraz uzasadnia decyzję.

6. Po przypisaniu partii komponentów do wyrobu operator generuje jednym kliknięciem cyfrowy paszport jakościowy w PDF.

## 3. Charakter i granice demonstratora

### Wymagane

- działanie w pełni lokalne przez Docker Compose,

- brak zależności od internetu w czasie prezentacji,

- realistyczne, ale całkowicie fikcyjne dane demonstracyjne,

- responsywność przystosowana przede wszystkim do monitora 1920×1080 i obsługi myszą lub ekranem dotykowym,

- polski interfejs z możliwością przełączenia na angielski,

- spójny przepływ danych między portalem dostawcy, mapą ryzyka, MRP, genealogią i paszportem,

- czytelny tryb „Demo Mode”, umożliwiający przywrócenie danych początkowych jednym przyciskiem,

- automatyczne zalogowanie do wybranej roli w trybie targowym, ale zachowanie pełnego logowania i RBAC w zwykłym trybie,

- brak prawdziwych nazw podmiotów, danych wrażliwych, lokalizacji zakładów i danych technicznych uzbrojenia.

### Poza zakresem pierwszej wersji

- rzeczywista integracja z systemem wojskowym lub ERP,

- przetwarzanie informacji niejawnych,

- formalna certyfikacja zgodności z AQAP, ISO lub STANAG,

- rzeczywiste śledzenie transportu GPS,

- autonomiczne podejmowanie decyzji produkcyjnych bez zatwierdzenia człowieka,

- szczegółowe dane konstrukcyjne systemów uzbrojenia.

W interfejsie i dokumentacji jasno zaznacz: **„Demonstrator wykorzystuje fikcyjne dane. Prezentowane mapowanie wymagań jakościowych nie stanowi formalnego potwierdzenia zgodności ani certyfikacji.”**

## 4. Wymagana architektura

Zastosuj architekturę pragmatyczną: łatwą do uruchomienia na stoisku, lecz pokazującą kompetencje enterprise.

### Frontend

- React + TypeScript,

- nowoczesny system budowania i ścisły TypeScript,

- komponenty dostępne klawiaturą i zgodne z WCAG 2.2 AA w kluczowych ścieżkach,

- TanStack Query lub równoważne rozwiązanie do stanu serwerowego,

- biblioteka formularzy z walidacją schematów,

- MapLibre GL lub równoważna biblioteka open source,

- mapa oparta na lokalnym GeoJSON lub lokalnych kaflach — nie pobieraj mapy z internetu,

- interaktywny Gantt bez zależności od komercyjnego komponentu wymagającego klucza,

- wykresy SVG/Canvas z czytelnymi tooltipami i legendami,

- internacjonalizacja PL/EN.

### Backend biznesowy

- [ASP.NET](http://ASP.NET) Core w aktualnej wersji LTS,

- modularny monolit podzielony na domeny: Suppliers, Inbound, Quality, Inventory, Production, Traceability, Risk, Documents, Identity, Audit,

- architektura warstwowa lub vertical slices, bez zbędnego ceremoniału,

- REST API z OpenAPI,

- SignalR do aktualizacji w czasie rzeczywistym,

- EF Core i PostgreSQL,

- uwierzytelnianie OIDC/OAuth2 i role użytkowników; lokalny provider tożsamości uruchamiany z Compose albo bezpieczny lokalny wariant demonstracyjny,

- walidacja wejścia, globalna obsługa błędów w formacie Problem Details, idempotencja kluczowych komend i optimistic concurrency dla edycji statusów.

### Silnik planowania

- osobna usługa Java + Spring Boot,

- solver oparty na aktualnej stabilnej bibliotece open source do constraint solving albo własnej deterministycznej heurystyce, jeśli biblioteka utrudnia stabilne uruchomienie,

- kontrakt OpenAPI między .NET i usługą Java,

- wejście i wynik scenariusza zapisane w bazie dla audytu,

- limit czasu rozwiązania konfigurowalny; dla danych demonstracyjnych wynik powinien pojawić się w ciągu maksymalnie 3 sekund,

- zawsze dostępny deterministyczny fallback, aby prezentacja nie zakończyła się błędem.

### Infrastruktura lokalna

- Docker Compose,

- PostgreSQL,

- S3-compatible local object storage dla certyfikatów i paszportów, np. MinIO,

- health checks i zależności startowe,

- jeden skrypt startowy dla Linux/macOS oraz instrukcja dla Windows/PowerShell,

- automatyczne migracje i seed danych w trybie demo,

- profile `demo` i `dev`,

- żadnych sekretów zapisanych na stałe w repozytorium; wartości demonstracyjne w pliku `.env.example`.

Nie rozbijaj rozwiązania na większą liczbę mikroserwisów. Priorytetem jest niezawodność prezentacji, zrozumiała architektura i możliwość późniejszego wydzielenia modułów.

## 5. Moduły funkcjonalne

### 5.1. Control Room Dashboard

Zaprojektuj główny ekran inspirowany nowoczesnym przemysłowym centrum dowodzenia. Użyj ciemnego granatowo-grafitowego tła, wysokiego kontrastu, subtelnych obramowań, oszczędnych akcentów w kolorach turkusowym, bursztynowym i czerwonym. Unikaj estetyki gry komputerowej, efektów „hakerskich”, przesadnych neonów i dekoracji bez znaczenia.

Górny pasek:

- nazwa systemu,

- bieżący czas i status środowiska,

- wskaźnik „System online / tryb offline”,

- wybór zakładu,

- przełącznik PL/EN,

- powiadomienia,

- użytkownik i rola,

- przycisk „Resetuj demo”.

Układ 2×2 na ekranie Full HD:

1. **Mapa dostaw i transportów** — dostawcy, zakład, trasy, ETA, statusy i animowana zmiana ryzyka.

2. **Heatmapa ryzyka łańcucha dostaw** — agregacja ryzyk wg regionu i kategorii komponentów.

3. **Plan linii / Gantt** — zlecenia, operacje, zależności, dostępność materiałów i wyróżnione konflikty.

4. **Status jakości i paszportów** — kompletność dokumentacji, braki, odrzucenia i gotowość do odbioru.

Nad lub pod siatką pokaż KPI:

- Material Readiness Rate,

- OTIF,

- liczba dostaw wysokiego ryzyka,

- przewidywane godziny przestoju,

- terminowość zleceń,

- kompletność paszportów jakościowych.

Każda karta ma mieć tooltip z definicją, trend względem poprzedniego okresu oraz możliwość przejścia do szczegółów. Kolor nie może być jedynym nośnikiem statusu — dodaj etykiety i ikony.

### 5.2. Portal Kooperanta i Logistyki Wjazdowej

Role dostawcy powinny widzieć wyłącznie dane własnej organizacji.

Funkcje:

- lista zamówień zakupu i pozycji,

- filtrowanie po statusie, terminie, ryzyku i zakładzie,

- aktualizacja procentu realizacji i statusu: potwierdzone, w produkcji, kontrola jakości, gotowe do wysyłki, wysłane, dostarczone, wstrzymane,

- podanie numeru partii, numeru wytopu lub serii, ilości, daty produkcji i daty ważności, jeśli dotyczy,

- wprowadzenie planowanego ETA i danych wysyłki,

- utworzenie awizacji dostawy,

- dodanie dokumentów: certyfikat materiałowy, raport kontroli, deklaracja zgodności, dokument transportowy,

- walidacja wymaganych metadanych i typu pliku,

- status dokumentu: oczekuje, weryfikowany, zaakceptowany, odrzucony, wymaga uzupełnienia,

- historia zmian i komentarze,

- podgląd wpływu spóźnionej pozycji na produkcję bez ujawniania dostawcy danych innych kontrahentów.

Po zmianie ETA system ma automatycznie:

1. przeliczyć ryzyko pozycji i dostawy,

2. wysłać zdarzenie domenowe,

3. zaktualizować dashboard przez SignalR,

4. wskazać potencjalnie zagrożone zlecenia produkcyjne,

5. utworzyć powiadomienie dla planisty, jeśli przekroczono próg.

### 5.3. Predictive Delivery ETA i ocena ryzyka

W pierwszej wersji nie wymagaj zewnętrznych API. Przygotuj interfejs adaptera danych logistycznych oraz lokalny symulator zdarzeń:

- opóźnienie graniczne,

- utrudnienia portowe,

- niekorzystna pogoda,

- problem jakościowy,

- częściowa dostawa,

- brak potwierdzenia przez dostawcę.

Ryzyko musi być wyjaśnialne. Wylicz wynik 0–100 na podstawie jawnych wag konfiguracyjnych, np.:

- odchylenie ETA od wymaganej daty,

- krytyczność komponentu,

- brak alternatywnego dostawcy,

- kompletność dokumentów jakościowych,

- wiarygodność historyczna dostawcy,

- poziom zapasu i pokrycie potrzeb,

- aktywne zdarzenia logistyczne.

Pokaż kategorię `Niskie`, `Średnie`, `Wysokie`, `Krytyczne`, wartość liczbową oraz sekcję „Dlaczego ten wynik?” z trzema najważniejszymi czynnikami. Nie przedstawiaj wyniku jako predykcji AI, jeśli jest wyliczany regułowo.

### 5.4. Dynamiczny silnik MRP i re-harmonogramowanie

Model planowania musi uwzględniać:

- zlecenia produkcyjne i ich priorytety,

- terminy wymagane,

- strukturę BOM,

- dostępny zapas, rezerwacje i dostawy w drodze,

- partie zaakceptowane i zablokowane jakościowo,

- kolejność operacji technologicznych,

- kalendarze i pojemność gniazd roboczych,

- czas operacji,

- operacje zamrożone, których nie wolno przesuwać,

- minimalizację zmian wobec zatwierdzonego planu,

- możliwość wykonania alternatywnego produktu, dla którego materiały są kompletne.

Zastosuj funkcję celu opartą na ważonej sumie:

- opóźnienie zleceń × priorytet,

- niedobór materiałowy,

- przewidywany przestój,

- naruszenie terminów dostaw,

- koszt zmiany planu,

- nadmierna liczba przezbrojeń.

Twarde ograniczenia nie mogą zostać naruszone: brak równoległego wykorzystania tego samego zasobu ponad pojemność, zachowanie kolejności operacji, brak zużycia partii zablokowanej jakościowo oraz zakaz planowania operacji wymagającej niedostępnego materiału.

#### Symulator What-If

Dodaj panel scenariuszy z gotowymi kaflami:

- „Opóźnij siłowniki ACT-40 o 10 dni”,

- „Opóźnij moduły sterujące MCU-X7 o 14 dni”,

- „Zablokuj partię stali HTS-22 z powodu jakości”,

- „Zwiększ priorytet zlecenia WO-2026-014”,

- „Zmniejsz dostępność gniazda integracji o 50%”.

Użytkownik może też zbudować własny scenariusz. Symulacja:

- nigdy nie zmienia planu bazowego bez zatwierdzenia,

- tworzy wersjonowany snapshot,

- pokazuje status przeliczania,

- zwraca plan proponowany i listę konsekwencji,

- prezentuje widok `Przed / Po` na jednym Gantcie,

- pokazuje przesunięte operacje oraz wpływ na KPI,

- generuje krótkie, deterministyczne uzasadnienie każdej rekomendacji,

- umożliwia `Zatwierdź`, `Odrzuć` lub `Zapisz scenariusz`.

Przykładowe uzasadnienie:

&gt; „Zlecenie WO-2026-014 przesunięto o 6 dni z powodu braku 8 szt. ACT-40. Na linię 2 przeniesiono WO-2026-019, ponieważ jego kompletność materiałowa wynosi 100%, a wymagane gniazda są dostępne. Prognozowany przestój zmniejszono z 36 do 8 godzin.”

Każde wyjaśnienie musi wynikać z rzeczywistych danych scenariusza, a nie być losowym tekstem.

### 5.5. Genealogia, serializacja i traceability

Zaimplementuj śledzenie relacji:

`Dostawca → Zamówienie zakupu → Pozycja → Wysyłka → Partia/wytop/seria → Przyjęcie magazynowe → Kontrola jakości → Zużycie materiału → Zlecenie produkcyjne → Operacja → Numer seryjny wyrobu → Paszport jakościowy`.

Funkcje:

- wyszukiwanie po numerze seryjnym, partii, wytopie, zamówieniu i dokumencie,

- drzewo genealogii wyrobu,

- trace-back: z wyrobu do wszystkich partii i certyfikatów,

- trace-forward: z partii do wszystkich wyrobów, w których ją wykorzystano,

- status partii: oczekuje na kontrolę, zaakceptowana, warunkowo dopuszczona, zablokowana, wycofana,

- widoczny łańcuch pochodzenia i historia zmian,

- eksport historii audytowej.

Dodaj scenariusz demonstracyjny „Zablokuj partię HTS-22-2608”. System ma wskazać, które wyroby i zlecenia są nią dotknięte, a następnie oznaczyć ich paszporty jako wymagające działania.

### 5.6. Cyfrowy Paszport Jakościowy

Paszport ma agregować:

- dane produktu i numer seryjny,

- zlecenie produkcyjne,

- wersję konfiguracji/BOM,

- listę kluczowych komponentów i partii,

- dostawców i kraj pochodzenia na poziomie dozwolonym dla roli,

- status kontroli jakości,

- certyfikaty i ich sumy kontrolne,

- wyniki inspekcji,

- rejestr odstępstw i zatwierdzeń,

- datę wygenerowania, wersję dokumentu i osobę zatwierdzającą,

- kod QR prowadzący do lokalnego rekordu paszportu,

- skrót kryptograficzny dokumentu.

Workflow:

1. `Roboczy` — brakuje danych lub dokumentów.

2. `Do weryfikacji` — dane są kompletne.

3. `Zatwierdzony` — uprawniony kontroler jakości zaakceptował paszport.

4. `Wygenerowany` — utworzono wersjonowany PDF.

5. `Unieważniony` — zmiana partii lub statusu jakości unieważniła wcześniejszą wersję.

Przycisk „Generuj paszport” powinien być aktywny tylko przy spełnieniu reguł kompletności. Przy braku danych pokaż konkretną listę braków. PDF ma być profesjonalny, czytelny, oznaczony jako demonstracyjny i możliwy do wygenerowania bez internetu. Zachowuj poprzednie wersje dokumentu oraz sumę SHA-256.

Nie deklaruj automatycznej zgodności ze wszystkimi AQAP/STANAG. Zbuduj konfigurowalny rejestr wymagań i szablon demonstracyjny `DQP-01`, aby w przyszłości można było mapować wymagania konkretnego kontraktu i standardu po analizie specjalisty.

### 5.7. Dokumenty i opcjonalne wspomaganie AI

Podstawowa ścieżka ma działać deterministycznie:

- użytkownik dodaje dokument,

- podaje jego typ i metadane,

- system waliduje plik, kompletność i zgodność identyfikatorów z partią.

Opcjonalnie, za feature flagą `LOCAL_AI_ENABLED`, dodaj adapter do lokalnego endpointu zgodnego z OpenAI API, np. model uruchomiony przez vLLM. Użyj go wyłącznie do:

- ekstrakcji pól z certyfikatu,

- klasyfikacji typu dokumentu,

- wskazania potencjalnej niezgodności,

- wygenerowania roboczego podsumowania dla kontrolera jakości.

Wymagania dla AI:

- żaden dokument nie opuszcza lokalnego środowiska,

- wynik ma status „propozycja” i wymaga zatwierdzenia człowieka,

- pokaż źródło każdej wyodrębnionej wartości i confidence,

- brak modelu nie może blokować prezentacji,

- nie używaj LLM do matematyki MRP, autoryzacji ani podejmowania decyzji jakościowej,

- w repozytorium umieść symulator odpowiedzi AI dla spójnego demo.

## 6. Role i uprawnienia

Zaimplementuj RBAC oraz zabezpieczenia na backendzie, nie tylko ukrywanie elementów UI.

Role:

- `SupplierUser` — własne zamówienia, wysyłki, partie i dokumenty,

- `InboundCoordinator` — wszystkie dostawy i awizacje,

- `ProductionPlanner` — plan bazowy, scenariusze i zatwierdzanie rekomendacji,

- `QualityInspector` — kontrola partii, dokumenty, paszporty i zatwierdzenia,

- `OperationsDirector` — dashboard, KPI i raporty,

- `Auditor` — odczyt historii i eksport,

- `Administrator` — konfiguracja słowników, progów, wag i użytkowników,

- `DemoPresenter` — pełna ścieżka demonstracyjna i reset danych, bez dostępu do ustawień bezpieczeństwa.

W menu deweloperskim dodaj szybkie przełączanie ról tylko w profilu `demo`. Każdą istotną zmianę zapisuj w audycie: kto, kiedy, co zmienił, poprzednia wartość, nowa wartość, correlation ID i źródło operacji.

## 7. Model danych

Zaprojektuj spójny model relacyjny obejmujący co najmniej:

- Organization,

- Site,

- User i Role,

- Supplier,

- SupplierPerformance,

- PurchaseOrder i PurchaseOrderLine,

- Shipment i ShipmentEvent,

- LogisticsRiskEvent,

- PartDefinition,

- MaterialLot,

- HeatNumber lub BatchNumber,

- InventoryBalance i Reservation,

- QualityDocument,

- QualityRequirement,

- QualityInspection,

- NonConformance,

- ProductDefinition,

- BomVersion, BomItem,

- WorkCenter i CapacityCalendar,

- ProductionOrder,

- OperationDefinition i ScheduledOperation,

- MaterialConsumption,

- ProductSerial,

- TraceabilityLink,

- Passport i PassportVersion,

- PlanningBaseline,

- PlanningScenario,

- ScenarioChange,

- PlanningRecommendation,

- RiskAssessment,

- Notification,

- AuditEvent.

Użyj identyfikatorów technicznych oraz osobnych czytelnych numerów biznesowych. Uwzględnij `createdAt`, `updatedAt`, wersjonowanie rekordów, statusy jako jawne enumy i ograniczenia integralności. Dokumenty przechowuj w object storage, a w bazie ich metadane, sumy kontrolne i wersje.

## 8. API i zdarzenia

Przygotuj udokumentowane endpointy co najmniej dla:

- logowania i kontekstu użytkownika,

- dashboardu i KPI,

- dostawców oraz zamówień,

- aktualizacji statusu pozycji,

- awizacji i zdarzeń przesyłki,

- dodawania i weryfikacji dokumentów,

- partii materiałowych i inspekcji,

- zapasów i rezerwacji,

- planu bazowego,

- tworzenia, uruchamiania i porównania scenariusza,

- zatwierdzenia rekomendacji,

- genealogii trace-back i trace-forward,

- paszportów, walidacji kompletności i generowania PDF,

- powiadomień,

- audytu,

- resetu danych demonstracyjnych.

Wprowadź nazwy zdarzeń domenowych, np.:

- `SupplierOrderStatusChanged`,

- `ShipmentEtaChanged`,

- `QualityDocumentUploaded`,

- `MaterialLotBlocked`,

- `DeliveryRiskChanged`,

- `PlanningScenarioCompleted`,

- `ProductionPlanApproved`,

- `PassportInvalidated`,

- `PassportGenerated`.

W pierwszej wersji zdarzenia mogą działać wewnątrz aplikacji z transactional outbox. Zaprojektuj interfejs tak, aby później można było dołączyć broker wiadomości bez przebudowy domeny.

## 9. Dane demonstracyjne

Seed musi być bogaty, spójny i deterministyczny. Użyj fikcyjnych nazw i bezpiecznych produktów przemysłowych o obronnym kontekście, bez wrażliwych parametrów.

Przygotuj:

- 1 zakład główny w Polsce,

- 8 fikcyjnych dostawców z Polski i Europy,

- 18 zamówień zakupu,

- minimum 35 pozycji zamówień,

- 12 aktywnych dostaw,

- 25 partii materiałowych,

- 3 definicje produktów, np. bezzałogowa platforma obserwacyjna, moduł bezpiecznej łączności i pojazd chronionej mobilności,

- 8 zleceń produkcyjnych,

- 5 gniazd roboczych,

- 2 linie montażowe,

- realistyczne BOM-y po 8–15 pozycji,

- 4 dokumenty brakujące lub odrzucone,

- 3 dostawy wysokiego ryzyka,

- 1 partia zablokowana jakościowo,

- 2 gotowe paszporty oraz 2 paszporty z brakami,

- plan bazowy na 12 tygodni.

Kluczowe identyfikatory demo:

- siłownik `ACT-40`,

- moduł sterujący `MCU-X7`,

- stal `HTS-22`,

- problematyczna partia `HTS-22-2608`,

- zlecenie priorytetowe `WO-2026-014`,

- zlecenie alternatywne `WO-2026-019`.

Wartości KPI przed i po scenariuszu muszą wynikać z danych, a nie być wpisane na stałe. Seed ma zostać dobrany tak, aby opóźnienie `ACT-40` faktycznie powodowało konflikt, a re-harmonogramowanie mogło wypełnić część luki zleceniem `WO-2026-019`.

## 10. UX i system wizualny

Interfejs powinien przypominać profesjonalne oprogramowanie przemysłowe klasy control room:

- ciemny, matowy granat i grafit,

- jasna typografia o wysokiej czytelności,

- turkus/zielony = prawidłowo,

- bursztynowy = ostrzeżenie,

- czerwony = problem krytyczny,

- niebieski = informacja lub akcja,

- delikatna siatka, subtelne obramowania i małe promienie narożników,

- gęsty, lecz uporządkowany układ informacji,

- typografia bezszeryfowa odpowiednia dla danych operacyjnych,

- ikony z jednego spójnego zestawu,

- minimalne animacje 150–250 ms,

- szanuj `prefers-reduced-motion`.

Nie używaj:

- losowych zdjęć stockowych,

- przesadnych gradientów i efektów glow,

- tekstów lorem ipsum,

- ogromnych pustych kart,

- wykresów bez jednostek i legend,

- drobnego tekstu nieczytelnego na monitorze targowym,

- flag państw jako jedynego oznaczenia lokalizacji,

- danych wojskowych wyglądających jak autentyczne informacje operacyjne.

Dodaj prezentacyjny „focus mode”, który powiększa wybrany panel mapy, Gantta lub paszportu bez opuszczania dashboardu. Najważniejsze akcje powinny być dostępne w maksymalnie dwóch kliknięciach.

## 11. Przebieg prezentacji targowej

Dodaj w aplikacji przycisk `Uruchom demo` otwierający dyskretny panel prezentera z krokami. Kroki nie mogą automatycznie klikać za użytkownika, ale powinny wskazywać następną akcję i umożliwiać reset.

### Scenariusz 4–5 minut

1. **Control Room:** pokaż dostawy, KPI i aktualny plan; wszystkie kluczowe wartości są zielone lub bursztynowe.

2. **Zdarzenie:** jako dostawca zmień ETA `ACT-40` o +10 dni albo uruchom gotowe zdarzenie symulatora.

3. **Reakcja na żywo:** mapa, licznik ryzyk i Gantt aktualizują się; system wskazuje zlecenia zagrożone.

4. **What-If:** otwórz scenariusz i kliknij `Przelicz plan`.

5. **Porównanie:** pokaż plan `Przed / Po`, zmianę przewidywanego przestoju i powód przesunięcia `WO-2026-019`.

6. **Traceability:** otwórz gotowy numer seryjny, przejdź po genealogii do partii i certyfikatów.

7. **Paszport:** pokaż kontrolę kompletności i wygeneruj PDF jednym kliknięciem.

8. **Podsumowanie:** wyświetl ekran wartości biznesowej: wcześniej wykryte ryzyko, uniknięty przestój, pełna identyfikowalność i gotowa dokumentacja odbiorowa.

Dodaj opcjonalny drugi scenariusz: zablokowanie partii `HTS-22-2608`, analiza trace-forward i automatyczne unieważnienie powiązanych paszportów.

## 12. Bezpieczeństwo i wiarygodność

Zaimplementuj co najmniej:

- RBAC egzekwowany w API,

- izolację danych dostawców,

- walidację MIME, rozszerzenia i rozmiaru dokumentu,

- skanowanie plików przez wymienny adapter; w demo może to być bezpieczny mock,

- ochronę przed path traversal i niebezpiecznymi nazwami plików,

- parametryzowane zapytania przez ORM,

- ochronę przed XSS, CSRF zależnie od modelu uwierzytelniania i clickjackingiem,

- rate limiting dla logowania, uploadu i kosztownych symulacji,

- audit log odporny na edycję z poziomu aplikacji,

- bezpieczne nagłówki HTTP,

- kontrolę dostępu do obiektów w object storage przez backend,

- redakcję danych w logach,

- correlation IDs i structured logging,

- kopię planu bazowego przed zatwierdzeniem zmian,

- brak sekretnych kluczy i prawdziwych danych w seedzie.

Przygotuj [`SECURITY.md`](http://SECURITY.md) opisujący model zagrożeń, granice demonstratora, ryzyka przed wdrożeniem produkcyjnym i listę elementów wymagających hardeningu. Nie używaj sformułowania „system certyfikowany” ani „zgodny z NATO”, jeśli nie wykonano formalnego procesu oceny.

## 13. Obserwowalność i diagnostyka

Dodaj:

- endpointy liveness i readiness,

- stronę statusu usług widoczną dla administratora,

- structured logs,

- metryki czasu odpowiedzi i czasu solvera,

- historię błędów ostatnich operacji demo bez ujawniania stack trace użytkownikowi,

- seed/reset z czytelnym rezultatem,

- widoczny stan połączenia SignalR,

- graceful degradation, gdy usługa Java lub opcjonalny model AI jest niedostępny.

Jeśli solver nie odpowie, backend ma użyć fallbacku, oznaczyć wynik jako `Heuristic fallback` i pozwolić kontynuować demonstrację.

## 14. Testy

Wymagane testy:

### Backend .NET

- testy jednostkowe formuły ryzyka,

- testy reguł kompletności paszportu,

- testy izolacji dostawców i autoryzacji,

- testy unieważnienia paszportu po blokadzie partii,

- testy integracyjne API z PostgreSQL.

### Silnik Java

- testy twardych ograniczeń,

- test deterministycznego scenariusza `ACT-40 +10 dni`,

- test braku podwójnego obciążenia gniazda,

- test zachowania operacji zamrożonych,

- test fallbacku.

### Frontend

- testy najważniejszych komponentów,

- test zmiany roli w trybie demo,

- test stanów loading/error/empty,

- test formatowania KPI i statusów.

### End-to-end

- aktualizacja ETA przez dostawcę aktualizuje ryzyko na dashboardzie,

- scenariusz What-If zwraca plan `Przed / Po`,

- zatwierdzenie planu tworzy audyt,

- blokada partii wpływa na traceability i paszport,

- kompletny paszport generuje PDF,

- niekompletny paszport pokazuje listę braków,

- reset demo odtwarza identyczny stan początkowy.

Testy E2E uruchamiaj przez Playwright lub równoważne rozwiązanie. Dodaj tryb smoke test uruchamiany po starcie Compose.

## 15. Kryteria akceptacji MVP

MVP jest ukończone wyłącznie wtedy, gdy:

1. `docker compose --profile demo up --build` uruchamia całe środowisko bez ręcznej konfiguracji.

2. Po otwarciu aplikacji widać kompletny dashboard z realnie policzonymi KPI.

3. Mapa działa bez połączenia z internetem.

4. Użytkownik dostawcy może zaktualizować ETA i dodać dokument.

5. Zmiana ETA `ACT-40 +10 dni` podnosi ryzyko i wskazuje zagrożone zlecenie.

6. Scenariusz What-If kończy się w maksymalnie 3 sekundy na danych demonstracyjnych.

7. Gantt pokazuje różnicę między planem bazowym i proponowanym.

8. System proponuje `WO-2026-019` jako alternatywę tylko wtedy, gdy kompletność materiałowa i dostępność zasobów faktycznie na to pozwalają.

9. Trace-back i trace-forward zwracają spójne wyniki.

10. Zablokowanie `HTS-22-2608` wskazuje wszystkie dotknięte rekordy.

11. Kompletny paszport generuje wersjonowany PDF z QR i SHA-256.

12. Niekompletny paszport nie generuje finalnego dokumentu i pokazuje przyczyny.

13. Kluczowe operacje są rejestrowane w audycie.

14. Role nie uzyskują dostępu do niedozwolonych danych przez bezpośrednie wywołanie API.

15. Wszystkie testy krytycznej ścieżki przechodzą.

16. Demo można zresetować do tego samego stanu w czasie krótszym niż 10 sekund.

17. Interfejs nie zawiera pustych ekranów, lorem ipsum, niedziałających przycisków ani statycznych KPI udających obliczenia.

## 16. Struktura repozytorium

Zaproponuj czytelną strukturę zbliżoną do:

```text

/

├── apps/

│   ├── web/

│   ├── business-api/

│   └── planning-engine/

├── packages/

│   ├── contracts/

│   ├── ui/

│   └── demo-data/

├── infrastructure/

│   ├── compose/

│   ├── identity/

│   └── observability/

├── docs/

│   ├── architecture/

│   ├── api/

│   ├── demo-script/

│   └── adr/

├── tests/

├── docker-compose.yml

├── .env.example

├── [README.md](http://README.md)

└── [SECURITY.md](http://SECURITY.md)

```

Jeżeli framework wymaga innego układu, zachowaj logiczny podział odpowiedzialności. Wspólne kontrakty generuj z OpenAPI, zamiast ręcznie duplikować modele między TypeScript, .NET i Java.

## 17. Dokumentacja końcowa

Przygotuj:

- [`README.md`](http://README.md) z uruchomieniem krok po kroku,

- diagram kontekstu i kontenerów w Mermaid,

- opis głównych decyzji architektonicznych,

- dokumentację API/OpenAPI,

- opis modelu i formuły ryzyka,

- opis ograniczeń i funkcji celu solvera,

- instrukcję seed/reset,

- 5-minutowy scenariusz prezentera,

- listę kont demonstracyjnych i ról,

- troubleshooting dla najczęstszych problemów,

- [`SECURITY.md`](http://SECURITY.md),

- listę różnic między demonstratorem a wersją produkcyjną.

W README rozpocznij od sekcji „Quick Start”. Wszystkie polecenia muszą być możliwe do skopiowania. Po zakończeniu pracy podaj:

1. co zostało zaimplementowane,

2. jak uruchomić system,

3. jakie konta demo są dostępne,

4. jak przeprowadzić scenariusz targowy,

5. wyniki testów,

6. znane ograniczenia,

7. rekomendowane następne kroki.

## 18. Kolejność implementacji

Realizuj w tej kolejności:

### Faza 1 — fundament

- repozytorium, Compose, baza, migracje, seed, auth, design system, wspólne kontrakty.

### Faza 2 — pionowy scenariusz dostawy

- portal dostawcy → aktualizacja ETA → ocena ryzyka → zdarzenie → dashboard live.

### Faza 3 — MRP i What-If

- plan bazowy → scenariusz `ACT-40 +10 dni` → solver/fallback → Gantt `Przed / Po` → rekomendacja.

### Faza 4 — traceability i jakość

- partie → dokumenty → inspekcje → zużycie → genealogia → blokada partii.

### Faza 5 — paszport

- kompletność → zatwierdzenie → PDF → QR → SHA-256 → wersjonowanie i unieważnianie.

### Faza 6 — polish targowy

- panel prezentera, focus mode, animacje, reset, smoke test, dokumentacja i stabilizacja offline.

Po każdej fazie zostaw działający system. Jeśli pełny zakres nie mieści się w jednym przebiegu, priorytetyzuj ukończenie pionowej ścieżki demo nad dodawaniem kolejnych ekranów.

## 19. Zasady jakości implementacji

- Nie twórz martwych przycisków ani pozornych integracji.

- Nie zapisuj KPI na stałe — wyliczaj je z danych.

- Nie generuj losowych wyników solvera.

- Nie uzależniaj krytycznej ścieżki od LLM ani internetu.

- Nie mieszaj DTO, modeli bazy i modeli domenowych bez wyraźnej granicy.

- Nie buduj nadmiernie rozproszonej architektury.

- Stosuj czytelne nazwy biznesowe, jawne jednostki i strefy czasowe.

- Daty zapisuj w UTC, a wyświetlaj w strefie wybranego zakładu.

- Kwoty, czasy, procenty i ilości formatuj zgodnie z lokalizacją.

- Każdy błąd użytkownika ma mieć zrozumiały komunikat i sposób naprawy.

- Krytyczne akcje wymagają potwierdzenia i pokazują ich wpływ.

- Zadbaj o puste stany, loading, offline/degraded i błędy częściowe.

- Utrzymuj testowalność i deterministyczność danych demo.

- Stosuj licencje bibliotek zgodne z wykorzystaniem komercyjnym; udokumentuj zależności i ich licencje.

## 20. Oczekiwany rezultat

Rezultatem ma być działająca aplikacja, która w ciągu kilku minut pozwala odbiorcy zobaczyć pełny łańcuch przyczynowo-skutkowy:

`opóźnienie dostawcy → wzrost ryzyka → zagrożenie planu → symulacja → nowy harmonogram → zachowanie ciągłości produkcji → genealogia partii → kompletny cyfrowy paszport`.

Najważniejsze jest wiarygodne połączenie tych procesów, czytelna prezentacja wartości biznesowej i niezawodność podczas demonstracji na stoisku.

# PROMPT END

---

## Sugerowane parametry uruchomienia projektu

Przed użyciem promptu można dopisać na jego początku parametry właściwe dla danego narzędzia:

```yaml

project_name: Defense Supply &amp; Production Control

delivery_mode: local_offline_trade_show_demo

primary_language: pl

secondary_language: en

frontend: React + TypeScript

business_backend: [ASP.NET](http://ASP.NET) Core LTS

planning_backend: Java + Spring Boot

database: PostgreSQL

object_storage: MinIO

realtime: SignalR

deployment: Docker Compose

target_screen: 1920x1080

demo_reset_required: true

local_ai_optional: true

```

## Krótki prompt kontynuacyjny po pierwszej iteracji

Jeżeli generator wykona tylko część systemu, użyj:

&gt; Kontynuuj implementację zgodnie z pierwotnym promptem. Nie twórz nowego planu od zera. Najpierw uruchom obecny projekt i testy, napraw wszystkie błędy blokujące, a następnie ukończ najbliższy niezamknięty pionowy scenariusz. Priorytet: działająca ścieżka `ACT-40 +10 dni → ryzyko → What-If → plan Przed/Po → rekomendacja WO-2026-019`, oparta na danych i bez statycznych atrap. Na końcu ponownie uruchom testy i zaktualizuj README oraz listę znanych ograniczeń.

