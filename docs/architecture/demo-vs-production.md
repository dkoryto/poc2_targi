# Demonstrator vs. wersja produkcyjna

| Obszar | Demonstrator | Produkcja |
|---|---|---|
| Tożsamość | lokalny JWT, konta demo, przełącznik ról | OIDC/OAuth2 (Keycloak/Entra), MFA, SSO, PAM |
| Dane | fikcyjne, seed względem bieżącego tygodnia, reset jednym przyciskiem | integracja ERP/MES/WMS, migracja, brak resetu |
| Zakłady | 4 zakłady demonstracyjne, zakresowanie przez `?siteCode=` (poza `/notifications` i `/audit`) | pełna wielozakładowość: zakresowanie wszystkich modułów, kalendarze i strefy czasowe per zakład, uprawnienia organizacyjne |
| Ryzyko dostaw | reguły z jawnymi wagami, symulator zdarzeń | adaptery do danych przewoźników/portów/pogody, kalibracja wag na historii |
| MRP | deterministyczna heurystyka, horyzont 12 tyg., limit solvera 2500 ms (klient czeka do 3 s) | solver CP (np. Timefold) z licencją, horyzont roczny, planowanie zmian i przezbrojeń |
| Dokumenty | MinIO, mock skanera, SHA-256 | WORM/retencja, AV/ICAP, podpis kwalifikowany PDF (PAdES), DMS |
| Paszport | szablon DQP-01, rejestr wymagań konfigurowalny | mapowanie wymagań kontraktu/AQAP po analizie specjalisty, podpisy, archiwizacja |
| Zdarzenia | outbox + in-process + SignalR | broker (RabbitMQ/Kafka), integracje wychodzące |
| Bezpieczeństwo | loopback, HTTP, rate limit, audyt append-only | TLS, WAF, SIEM, kopie zapasowe, DR, testy penetracyjne, klasyfikacja informacji |
| Ekstrakcja z dokumentów | opcjonalny lokalny model (`LocalAi__Enabled`, domyślnie wyłączony), wyłącznie propozycje do akceptacji człowieka | izolowany host modelu, ocena ryzyka prompt-injection, rejestr decyzji |
| Obserwowalność | logi JSON, health, strona statusu | OTEL, Prometheus/Grafana, alerting |

Pełna lista znanych ograniczeń demonstratora: [`multi-site.md`](multi-site.md#ograniczenia) oraz
[`SECURITY.md`](../../SECURITY.md#known-risks-before-any-production-use).
Instrukcja wdrożenia pod domeną: `docs/deployment.md`.
