# Delivery risk model (rule-based, explainable)

Score 0–100 per purchase-order line (and aggregated per shipment = max of its lines, per supplier = weighted mean).
**Not an AI prediction** — UI must label it "Ocena regułowa / Rule-based score".

| Factor | Code | Raw → 0..100 | Weight |
|---|---|---|---|
| ETA deviation vs required date | `ETA_DEVIATION` | daysLate ≤ 0 → 0; else min(100, daysLate × 12) | 0.35 |
| Component criticality | `CRITICALITY` | (crit − 1) × 25 | 0.15 |
| No alternative supplier | `NO_ALTERNATIVE` | 100 if none else 0 | 0.10 |
| Quality document completeness | `DOC_COMPLETENESS` | (1 − accepted/required) × 100; rejected doc counts as missing | 0.15 |
| Supplier reliability | `SUPPLIER_RELIABILITY` | 100 − OTIF% (last 90 d) | 0.10 |
| Stock coverage of open demand | `COVERAGE` | shortage/demand × 100 where shortage = max(0, demand − freeOnHand) for that part | 0.10 |
| Active logistics events | `LOGISTICS_EVENTS` | Σ severity (LOW 25, MEDIUM 50, HIGH 100) capped 100 | 0.05 |

`score = round(Σ weight × raw)`. Category: `Low` < 25, `Medium` < 50, `High` < 75, `Critical` ≥ 75.
Weights configurable in `appsettings` `Risk:Weights` (Administrator UI shows them read-only in v1).

"Dlaczego ten wynik?" = top 3 factors by contribution (weight × raw), each with raw value, contribution and a localised label.

Worked example (PO-2026-0007/1, ACT-40): deviation −1 d → 0; crit 5 → 100 → 15; no alt → 10; docs 1/2 → 50 → 7.5; OTIF 88 → 12 → 1.2; coverage demand 14 free 0 → 100 → 10; events 0 → 0. **Score 44 → Medium.**
After +10 d: deviation 9 d → 100 → +35 → **79 → Critical**.

Re-scoring triggers: `ShipmentEtaChanged`, `SupplierOrderStatusChanged`, `QualityDocumentUploaded`/verified, `MaterialLotBlocked`, `LogisticsRiskEventRaised`, inventory change. Result stored as `RiskAssessment` row (history kept) and `DeliveryRiskChanged` raised when category changes or |Δscore| ≥ 5.

Endangered production orders for a line = orders whose operations require the part with material requirement date < new ETA and that are not covered by free stock (same allocation order as engine: frozen first, priority desc, due asc).
