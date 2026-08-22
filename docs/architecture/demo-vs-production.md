# Demonstrator vs. wersja produkcyjna

| Obszar | Demonstrator | Produkcja |
|---|---|---|
| Tożsamość | lokalny JWT, konta demo, przełącznik ról | OIDC/OAuth2 (Keycloak/Entra), MFA, SSO, PAM |
| Dane | fikcyjne, seed względem bieżącego tygodnia, reset jednym przyciskiem | integracja ERP/MES/WMS, migracja, brak resetu |
| Ryzyko dostaw | reguły z jawnymi wagami, symulator zdarzeń | adaptery do danych przewoźników/portów/pogody, kalibracja wag na historii |
| MRP | deterministyczna heurystyka, horyzont 12 tyg., < 3 s | solver CP (np. Timefold) z licencją, wielozakładowość, horyzont roczny, planowanie zmian |
| Dokumenty | MinIO, mock skanera, SHA-256 | WORM/retencja, AV/ICAP, podpis kwalifikowany PDF (PAdES), DMS |
| Paszport | szablon DQP-01, rejestr wymagań konfigurowalny | mapowanie wymagań kontraktu/AQAP po analizie specjalisty, podpisy, archiwizacja |
| Zdarzenia | outbox + in-process + SignalR | broker (RabbitMQ/Kafka), integracje wychodzące |
| Bezpieczeństwo | loopback, HTTP, rate limit, audyt append-only | TLS, WAF, SIEM, kopie zapasowe, DR, testy penetracyjne, klasyfikacja informacji |
| AI | opcjonalny lokalny LLM, tylko propozycje | izolowany host modelu, ocena ryzyka prompt-injection, rejestr decyzji |
| Obserwowalność | logi JSON, health, strona statusu | OTEL, Prometheus/Grafana, alerting |
