# Demo scenario — seed numbers (source of truth)

All seed dates are offsets from **T0 = Monday of the current ISO week, 06:00 site time (Europe/Warsaw)**,
computed at seed/reset time so the demo always looks "current" while staying deterministic.
Java engine tests pin `T0 = 2026-09-07`. Working day = Mon–Fri, 16 h (06:00–22:00, two shifts).
`T0+n` = n calendar days after T0. T0+4 = Fri w1, T0+7 = Mon w2, T0+11 = Fri w2, T0+14 = Mon w3, T0+18 = Fri w3.

## Site, lines, work centers

- `SITE-01` "Zakład Centralny" — fictional, lat 52.05 lon 19.45 (rural central PL). TZ Europe/Warsaw.
- `LINE-1` mechanical line: `WC-CUT` (cięcie/obróbka), `WC-WELD` (spawanie), `WC-TEST` (testy końcowe, shared).
- `LINE-2` electronics/integration line: `WC-ELEC` (montaż elektroniki), `WC-INT` (gniazdo integracji).
- All work centers: 16 h/day Mon–Fri, calendar exceptions: none in seed (keeps engine test simple).

## Products (3) and BOM (8–15 items each, per unit)

| Product | Name PL / EN | BOM (part × qty per unit) |
|---|---|---|
| `P-OBS-01` | Bezzałogowa platforma obserwacyjna / Unmanned observation platform | ACT-40×2, MCU-X7×1, OPT-12×1, BAT-9×2, FRM-3×1, CON-5×6, HRN-8×2, ANT-2×1, PCB-11×2, SNS-4×3, FAS-1×40, GBX-7×2 |
| `P-COM-02` | Moduł bezpiecznej łączności / Secure communications module | MCU-X7×1, ENC-4×1, ANT-2×2, PSU-6×1, PCB-11×3, CON-5×8, HRN-8×1, FAS-1×20, DSP-2×1 |
| `P-MOB-03` | Pojazd chronionej mobilności / Protected mobility vehicle | HTS-22×200 (kg), ACT-40×2, MCU-X7×1, ARM-2×4, WHL-1×4, GBX-7×1, SEAL-3×10, FAS-1×120, HRN-8×3, PSU-6×1, CBL-3×30 (m), SNS-4×4, CON-5×12, BAT-9×1 |

Routing templates (operation code = `<WO>/<seq>`), hours are per order and are set explicitly per WO below.
- P-OBS-01: 10 WC-CUT → 20 WC-ELEC → 30 WC-INT (needs ACT-40, MCU-X7, OPT-12, GBX-7) → 40 WC-TEST
- P-COM-02: 10 WC-ELEC (needs MCU-X7, PCB-11, ENC-4) → 20 WC-INT (needs ANT-2, PSU-6, CON-5) → 30 WC-TEST
- P-MOB-03: 10 WC-CUT (needs HTS-22) → 20 WC-WELD (needs ARM-2) → 30 WC-INT (needs ACT-40, MCU-X7, WHL-1, GBX-7) → 40 WC-TEST
Material requirements are attached to the *first operation that consumes the part*; parts not listed above are attached to op 10.

## Parts (criticality 1–5, alt supplier?)

| Code | Name PL | Unit | Crit | Alt supplier | Primary supplier |
|---|---|---|---|---|---|
| ACT-40 | Siłownik elektromechaniczny ACT-40 | szt | 5 | no | SUP-02 |
| MCU-X7 | Moduł sterujący MCU-X7 | szt | 5 | no | SUP-03 |
| HTS-22 | Stal wysokowytrzymała HTS-22 | kg | 4 | yes (SUP-07) | SUP-01 |
| OPT-12 | Moduł optyczny OPT-12 | szt | 4 | no | SUP-04 |
| SNS-4 | Czujnik środowiskowy SNS-4 | szt | 2 | yes | SUP-04 |
| BAT-9 | Pakiet bateryjny BAT-9 | szt | 3 | yes | SUP-08 |
| PSU-6 | Zasilacz PSU-6 | szt | 2 | yes | SUP-08 |
| FRM-3 | Rama kompozytowa FRM-3 | szt | 3 | no | SUP-05 |
| ARM-2 | Panel ochronny ARM-2 | szt | 4 | no | SUP-05 |
| CON-5 | Złącze hermetyczne CON-5 | szt | 2 | yes | SUP-06 |
| HRN-8 | Wiązka przewodów HRN-8 | szt | 2 | yes | SUP-06 |
| CBL-3 | Kabel ekranowany CBL-3 | m | 1 | yes | SUP-06 |
| GBX-7 | Przekładnia GBX-7 | szt | 3 | no | SUP-07 |
| WHL-1 | Zespół kołowy WHL-1 | szt | 3 | yes | SUP-07 |
| SEAL-3 | Zestaw uszczelnień SEAL-3 | kpl | 1 | yes | SUP-07 |
| FAS-1 | Elementy złączne FAS-1 | szt | 1 | yes | SUP-07 |
| ANT-2 | Antena szerokopasmowa ANT-2 | szt | 3 | no | SUP-03 |
| ENC-4 | Moduł szyfrujący ENC-4 | szt | 5 | no | SUP-03 |
| PCB-11 | Płytka PCB-11 | szt | 2 | yes | SUP-03 |
| DSP-2 | Panel wyświetlacza DSP-2 | szt | 2 | yes | SUP-04 |

## Suppliers (8, fictional)

| Code | Name | Country/City (fictional HQ) | lat, lon | OTIF % | Parts |
|---|---|---|---|---|---|
| SUP-01 | Nordstal Sp. z o.o. | PL, Gdańsk | 54.35, 18.65 | 94 | HTS-22 |
| SUP-02 | Hydromech Actuators GmbH | DE, Stuttgart | 48.78, 9.18 | 88 | ACT-40 |
| SUP-03 | Vistula Electronics S.A. | PL, Kraków | 50.06, 19.94 | 91 | MCU-X7, ANT-2, ENC-4, PCB-11 |
| SUP-04 | Baltic Optics OÜ | EE, Tallinn | 59.44, 24.75 | 82 | OPT-12, SNS-4, DSP-2 |
| SUP-05 | Carpathia Composites s.r.o. | CZ, Brno | 49.19, 16.61 | 90 | FRM-3, ARM-2 |
| SUP-06 | Rhône Connectique SAS | FR, Lyon | 45.76, 4.84 | 96 | CON-5, HRN-8, CBL-3 |
| SUP-07 | Silesia Precision Sp. z o.o. | PL, Gliwice | 50.29, 18.67 | 97 | GBX-7, WHL-1, SEAL-3, FAS-1, HTS-22 (alt) |
| SUP-08 | Iberia Power Systems S.L. | ES, Zaragoza | 41.65, -0.89 | 85 | BAT-9, PSU-6 |

Routes on the map: straight great-circle-ish polylines supplier → SITE-01 (local GeoJSON, Europe outline from `public/geo/europe.geojson`).

## Production orders (baseline, hand-placed, feasible — engine must reproduce it unchanged when inputs are unchanged)

Times are `T0+d hh:mm`. WC hours 06:00–22:00. Priority 5 = highest.

| WO | Product | Qty | Prio | Release | Due | Status | Ops (seq WC hours: start → end) |
|---|---|---|---|---|---|---|---|
| WO-2026-011 | P-MOB-03 | 2 | 4 | T0-30 | T0-5 | Completed | all ops done T0-28..T0-7; consumed lot **HTS-22-2608**; serials `PMV-2026-0007`, `PMV-2026-0008` with **generated passports** |
| WO-2026-012 | P-COM-02 | 10 | 3 | T0-2 | T0+9 | InProgress, **frozen** | 10 ELEC 24h: T0 06:00→T0+1 14:00; 20 INT 16h: T0+2 06:00→T0+2 22:00; 30 TEST 8h: T0+3 06:00→14:00 |
| WO-2026-013 | P-MOB-03 | 2 | 4 | T0 | T0+18 | Released (op10 frozen) | 10 CUT 32h: T0 06:00→T0+1 22:00; 20 WELD 48h: T0+2 06:00→T0+4 22:00; 30 INT 32h: T0+7 06:00→T0+8 22:00; 40 TEST 16h: T0+9 06:00→T0+9 22:00 |
| **WO-2026-014** | P-OBS-01 | 4 | **5** | T0 | **T0+18** | Released | 10 CUT 16h: T0+2 06:00→22:00; 20 ELEC 32h: T0+4 06:00→T0+7 22:00 (Fri+Mon); **30 INT 36h: T0+9 06:00→T0+11 10:00** (needs ACT-40×8, MCU-X7×4, OPT-12×4, GBX-7×8); 40 TEST 16h: T0+14 06:00→22:00 |
| WO-2026-015 | P-MOB-03 | 1 | 3 | T0 | T0+32 | Released | 10 CUT 16h: T0+3 06:00→22:00; 20 WELD 24h: T0+8 06:00→T0+9 14:00; 30 INT 16h: T0+16 06:00→22:00 (needs ACT-40×2); 40 TEST 8h: T0+18 06:00→14:00 |
| WO-2026-016 | P-OBS-01 | 2 | 2 | T0+7 | T0+39 | Planned | 10 CUT 8h: T0+15 06:00→14:00; 20 ELEC 16h: T0+16 06:00→22:00; 30 INT 24h: T0+23 06:00→T0+24 14:00 (needs ACT-40×4 from PO-2026-0012); 40 TEST 8h: T0+25 06:00→14:00 |
| WO-2026-017 | P-COM-02 | 8 | 3 | T0+14 | T0+46 | Planned | 10 ELEC 20h: T0+29 06:00→T0+30 10:00 (needs MCU-X7×8 from PO-2026-0009); 20 INT 12h: T0+31 06:00→18:00; 30 TEST 8h: T0+32 06:00→14:00 |
| WO-2026-018 | P-MOB-03 | 2 | 4 | T0+21 | T0+60 | Planned | 10 CUT 32h: T0+30 06:00→T0+31 22:00 (needs HTS-22×400 → reserved on lot **HTS-22-2608**); 20 WELD 48h: T0+32→T0+34 22:00; 30 INT 32h: T0+36 06:00→T0+37 22:00; 40 TEST 16h: T0+38 |
| **WO-2026-019** | P-COM-02 | 6 | 2 | **T0** | T0+53 | Planned, **material 100% on hand** | 10 ELEC 24h: T0+37 06:00→T0+38 14:00; **20 INT 28h: T0+39 06:00→T0+40 18:00**; 30 TEST 8h: T0+41 06:00→14:00 |

## Inventory (accepted, unblocked on-hand at T0) — the parts that matter

| Part | On hand | Reserved (by) | Note |
|---|---|---|---|
| ACT-40 | 4 | 4 (WO-2026-013) | **0 free** → WO-014 (8) and WO-015 (2) depend on PO-2026-0007 |
| MCU-X7 | 24 | 10 (WO-012) + 2 (WO-013) + 4 (WO-014) + 1 (WO-015) + 6 (WO-019) = 23 | 1 free; WO-016 (2) & WO-017 (8) depend on PO-2026-0009 |
| OPT-12 | 6 | 4 (WO-014) | WO-016 needs 2 → ok |
| HTS-22 | 900 kg | 400 (WO-013, lot 2607) + 400 (WO-018, lot 2608) | lots: HTS-22-2607 (500 kg, Accepted), HTS-22-2608 (400 kg, Accepted — scenario target; also consumed by WO-011), HTS-22-2611 (inbound) |
| everything else | ≥ total demand of all active orders | | so only ACT-40 / MCU-X7 / HTS-22 drive scenarios |

Exactly one lot is **Blocked** in seed: `CON-5-1142` (SUP-06, 200 szt, NCR-2026-004 "wymiar poza tolerancją"). Not reserved by any order (stock still sufficient).

## Inbound (the lines that drive risk) — 18 POs, ≥35 lines, 12 active shipments total

| PO / line | Supplier | Part | Qty | Required | ETA | Docs | Status | Risk (seed) |
|---|---|---|---|---|---|---|---|---|
| **PO-2026-0007/1** | SUP-02 | ACT-40 | 12 | T0+9 | **T0+8** | cert pending (completeness 0.5) | Shipped (shipment SHP-2026-0031, in transit DE→PL) | ≈44 Medium → after +10 d ≈79 **Critical** |
| PO-2026-0009/1 | SUP-03 | MCU-X7 | 12 | T0+22 | T0+25 | inspection report **rejected** | InProduction | ≈58 High |
| PO-2026-0010/1 | SUP-04 | OPT-12 | 4 | T0+12 | T0+16 | ok | Shipped, active event `PORT_DISRUPTION` (Tallinn) | ≈55 High |
| PO-2026-0011/2 | SUP-06 | CON-5 | 400 | T0+10 | T0+10 | **missing** declaration of conformity; supplier not confirmed | Confirmed (no ack) | ≈52 High |
| PO-2026-0012/1 | SUP-02 | ACT-40 | 10 | T0+22 | T0+20 | ok | InProduction | ≈22 Low |
| PO-2026-0013/1 | SUP-01 | HTS-22 | 600 | T0+25 | T0+24 | ok | Confirmed | Low |
| others (PO-2026-0001…0006 delivered in past 90 days, 0014…0018 future) | | | | | | | | Low |

4 documents missing/rejected in seed: PO-0007 cert pending, PO-0009 report rejected, PO-0011 declaration missing, HTS-22-2611 cert "requires completion".
Delivered history (PO-0001…0006, 14 lines) gives OTIF ≈ 86 % on dashboard.

## Scenario 1 — "Opóźnij siłowniki ACT-40 o 10 dni" (must hold exactly)

Change: PO-2026-0007/1 ETA T0+8 → **T0+18**.
1. Risk of PO-2026-0007/1 goes ≈44 → ≈79 (Critical). Dashboard "high-risk deliveries" 3 → 4. Endangered orders: **WO-2026-014** (needs 8 on T0+9), WO-2026-015 (needs 2 on T0+16).
2. Impact without re-planning ("Przed" in What-If = baseline + delay, no resequencing): WO-014 op 30 cannot start before T0+18 06:00 → ends T0+21 10:00 (Mon w4; weekend skipped) → op 40 T0+21 10:00→T0+22 10:00 → WO-014 **late 4 days** (due T0+18, ends T0+22). WO-015 op 30 → T0+21 10:00 onward, still on time. **Predicted downtime 36 h** (WC-INT slot T0+9→T0+11 baseline-busy, now idle).
3. Engine result ("Po"): WO-014 as above (unavoidable; ORDER_DELAYED_MATERIAL_SHORTAGE partCode ACT-40 missingQty 8 days 4 availableOn T0+18). **WO-2026-019 pulled forward**: op 10 ELEC 24h → T0+7 06:00→T0+8 14:00 (ELEC free after WO-014 op 20 ends T0+7 22:00? — no: WO-014 op20 occupies ELEC T0+4→T0+7 22:00, so 019 op10 → T0+8 06:00→T0+9 14:00), op 20 INT 28h → **T0+9 14:00→T0+11 10:00** (fills the freed WC-INT slot; remaining idle 36−28 = **8 h**), op 30 TEST 8h → T0+12? weekend → T0+14 06:00→14:00 then WO-014 op40 is pushed to T0+22 anyway. Explanations: ORDER_PULLED_FORWARD {orderCode WO-2026-019, lineCode LINE-2, days 29, materialCompleteness 1.0, workCenters [WC-ELEC, WC-INT]}, DOWNTIME_REDUCED {fromHours 36, toHours 8}.
4. KPI delta: downtime 36 → 8 h, late orders 1 (WO-014, 4 d), moved operations 6 (3 of WO-014, 3 of WO-019) — WO-015 op 30/40 may also move (T0+16 → T0+21..22), engine must report them as changed.

Engine test `Act40DelayScenarioTest` asserts: WO-014 latenessDays == 4, WO-019 op 20 starts on T0+9 at WC-INT, kpi.downtimeHours == 8, explanations contain ORDER_PULLED_FORWARD for WO-2026-019 and DOWNTIME_REDUCED 36→8, baseline run (no delay) returns zero changed operations.

## Scenario 2 — "Zablokuj partię HTS-22-2608"

Lot status Accepted → Blocked (NCR created). Trace-forward: consumed by WO-2026-011 → serials PMV-2026-0007, PMV-2026-0008 → both passports `Generated` → **Invalidated** (`PassportInvalidated`). Reserved by WO-2026-018 → reservation flagged, order materialComplete=false (shortage HTS-22 400 kg, availableOn = PO-2026-0013 ETA T0+24). Risk for PO-2026-0013/1 rises (coverage factor).

## Other scenario tiles

- "Opóźnij MCU-X7 o 14 dni": PO-2026-0009/1 ETA T0+25 → T0+39; WO-017 op10 (T0+29) delayed → late ~3 d; WO-016 unaffected (uses free stock? no — WO-016 needs 2 MCU-X7, 1 free → shortage 1 → also delayed). Engine just computes.
- "Zwiększ priorytet WO-2026-014": priority already 5 → scenario sets 5 and marks others ≤4; no visible change → explanation "no change needed" (ORDER_FROZEN_KEPT-style NO_CHANGE). Use `PRIORITY_CHANGE` scenario type; acceptable outcome is "plan unchanged".
- "Zmniejsz dostępność WC-INT o 50 %": capacityFactor 0.5 → 8 h/day → WO-013/014/015 INT ops stretch; engine reports CAPACITY_REDUCED and lateness.

## Serials & passports

- WO-2026-011: `PMV-2026-0007`, `PMV-2026-0008` — passports v1 Generated (PDF exists after seed; generated at seed time by the same PDF pipeline).
- WO-2026-012 (in progress): `SCM-2026-0101`…`0110` pre-assigned; passports Draft; `SCM-2026-0101` and `0102` have all data except inspection result → missing list = [INSPECTION_RESULT]; `0103` missing [CERT:MCU-X7 lot MCU-X7-0455, INSPECTION_RESULT].
- Demo "complete passport" path uses `PMV-2026-0007` (regenerate v2) or `SCM-2026-0101` after inspector records a passed inspection in UI.
- Passport template registry: `DQP-01` (demo) with requirement rows: PRODUCT_DATA, ORDER_REF, BOM_VERSION, KEY_COMPONENT_LOTS, SUPPLIER_ORIGIN, QC_STATUS, CERTIFICATES_WITH_HASH, INSPECTION_RESULTS, DEVIATIONS, APPROVAL.

## Demo accounts (password `demo`)

presenter (DemoPresenter), planner (ProductionPlanner), inbound (InboundCoordinator), quality (QualityInspector), director (OperationsDirector), auditor (Auditor), admin (Administrator), supplier.hydromech (SupplierUser, SUP-02), supplier.nordstal (SupplierUser, SUP-01), supplier.vistula (SupplierUser, SUP-03).
