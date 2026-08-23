# Demo scenario — seed numbers for `SITE-01` (Kielce)

**Scope.** This document describes the **Kielce plant (`SITE-01`) only** — the plant the golden path, the engine
fixtures and the e2e specs are written against. The other three plants are described in
[`multi-site.md`](multi-site.md); their data is smaller and follows the same rules.

**Authority.** Where this document and the engine fixture
[`packages/contracts/examples/baseline.json`](../../packages/contracts/examples/baseline.json) disagree, **the fixture
wins** — the seeder is generated from it, so it cannot drift. The tables below have been reconciled with the fixture
and with the running system; treat any future mismatch as a bug in this document.

All seed dates are offsets from **T0 = Monday of the current ISO week, 06:00 site time (Europe/Warsaw)**,
computed at seed/reset time so the demo always looks "current" while staying deterministic.
Java engine tests pin `T0 = 2026-09-07`. Working day = Mon–Fri, 16 h (06:00–22:00, two shifts).
`T0+n` = n calendar days after T0. T0+4 = Fri w1, T0+7 = Mon w2, T0+11 = Fri w2, T0+14 = Mon w3, T0+18 = Fri w3.

## Site, lines, work centers

- `SITE-01` "Zakład Kielce" — fictional plant, Kielce, lat 50.87 lon 20.63. TZ Europe/Warsaw.
- `LINE-1` mechanical line: `WC-CUT` (cięcie/obróbka), `WC-WELD` (spawanie), `WC-TEST` (testy końcowe, shared).
- `LINE-2` electronics/integration line: `WC-ELEC` (montaż elektroniki), `WC-INT` (gniazdo integracji).
- All work centers: 16 h/day Mon–Fri, calendar exceptions: none in seed (keeps engine test simple).

**Material allocation order** (used by the engine, the fallback evaluator and the endangered-orders rule alike):
frozen operations first, then by desired start (baseline start, else release date), then priority descending,
then due date ascending, then order code — **need date first, not priority first**.

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
| **WO-2026-014** | P-OBS-01 | 4 | **5** | T0 | **T0+18** | Released | 10 CUT 16h: T0+2 06:00→22:00; 20 ELEC 32h: T0+4 06:00→T0+7 22:00 (Fri+Mon); **30 INT 36h: T0+9 06:00→T0+11 10:00** (needs ACT-40×8, MCU-X7×4, OPT-12×4, GBX-7×8); **40 TEST 12h: T0+14 06:00→18:00** (12 h, not 16 — this is what makes the scenario lateness exactly 4 days) |
| WO-2026-015 | P-MOB-03 | 1 | 3 | T0 | T0+32 | Released | 10 CUT 16h: T0+3 06:00→22:00 (needs HTS-22×200); 20 WELD 24h: T0+8 06:00→T0+9 14:00; **30 INT 16h: T0+21 06:00→22:00** (needs ACT-40×2); **40 TEST 8h: T0+22 06:00→14:00** |
| WO-2026-016 | P-OBS-01 | 2 | 2 | T0+7 | T0+39 | Planned | 10 CUT 8h: T0+15 06:00→14:00; 20 ELEC 16h: T0+16 06:00→22:00; 30 INT 24h: T0+23 06:00→T0+24 14:00 (needs ACT-40×4 from PO-2026-0012); 40 TEST 8h: T0+25 06:00→14:00 |
| WO-2026-017 | P-COM-02 | 8 | 3 | T0+14 | T0+46 | Planned | 10 ELEC 20h: T0+29 06:00→T0+30 10:00 (needs MCU-X7×8 from PO-2026-0009); 20 INT 12h: T0+31 06:00→18:00; 30 TEST 8h: T0+32 06:00→14:00 |
| WO-2026-018 | P-MOB-03 | 2 | 4 | T0+21 | T0+60 | Planned | 10 CUT 32h: T0+30 06:00→T0+31 22:00 (needs HTS-22×400 → reserved on lot **HTS-22-2608**; also ACT-40×4, MCU-X7×2); **20 WELD 48h: T0+32 06:00→T0+36 22:00**; **30 INT 32h: T0+37 06:00→T0+38 22:00**; **40 TEST 16h: T0+39** |
| **WO-2026-019** | P-COM-02 | 6 | 2 | **T0** | T0+53 | Planned, **material 100% on hand** | 10 ELEC 24h: T0+37 06:00→T0+38 14:00; **20 INT 28h: T0+39 06:00→T0+42 18:00**; **30 TEST 8h: T0+43 06:00→14:00** |

## Inventory (accepted, unblocked on-hand at T0) — the parts that matter

Values below are what `GET /api/v1/inventory?siteCode=SITE-01` returns after a reset.

| Part | On hand | Reserved | Free | Note |
|---|---|---|---|---|
| ACT-40 | 4 | 4 | **0** | WO-014 (8) and WO-015 (2) depend entirely on PO-2026-0007 — this is what makes scenario 1 bite |
| MCU-X7 | 26 | 20 | 6 | WO-016 (2) & WO-017 (8) still depend on PO-2026-0009 |
| OPT-12 | 6 | 4 | 2 | WO-016 needs 2 → covered |
| HTS-22 | 1000 kg | 1000 kg | 0 | lots `HTS-22-2607` (600 kg, Accepted) and `HTS-22-2608` (800 kg, Accepted — scenario 2 target, also consumed by WO-011) |
| everything else | ≥ total demand of all active orders | | | so only ACT-40 / MCU-X7 / HTS-22 drive the scenarios |

Exactly one lot is **Blocked** in seed: `CON-5-1142` (SUP-06, 200 szt, NCR "wymiar poza tolerancją"). Not reserved by any order (stock still sufficient), so it colours the quality panel without breaking a scenario.

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

4 documents missing/rejected in seed: PO-0007 cert pending, PO-0009 report rejected, PO-0011 declaration missing, one inbound HTS-22 cert "requires completion".
Delivered history gives **OTIF 84.2 %** on the Kielce dashboard, and `HIGH_RISK_DELIVERIES` = 3 before any scenario runs.

## Scenario 1 — "Opóźnij siłowniki ACT-40 o 10 dni" (must hold exactly)

Change: PO-2026-0007/1 ETA T0+8 → **T0+18**.
1. Risk of PO-2026-0007/1 goes ≈44 → ≈79 (Critical). Dashboard "high-risk deliveries" 3 → 4. Endangered orders: **WO-2026-014** (needs 8 on T0+9), WO-2026-015 (needs 2 on T0+16).
2. Impact without re-planning ("Przed" in What-If = baseline + delay, no resequencing): WO-014 op 30 cannot start before T0+18 06:00 → ends T0+21 10:00 (Mon w4; weekend skipped) → op 40 T0+21 10:00→T0+22 10:00 → WO-014 **late 4 days** (due T0+18, ends T0+22). WO-015 op 30 → T0+21 10:00 onward, still on time. **Predicted downtime 36 h** (WC-INT slot T0+9→T0+11 baseline-busy, now idle).
3. Engine result ("Po"): WO-014 as above (unavoidable; ORDER_DELAYED_MATERIAL_SHORTAGE partCode ACT-40 missingQty 8 days 4 availableOn T0+18). **WO-2026-019 pulled forward**: op 10 ELEC 24h → T0+7 06:00→T0+8 14:00 (ELEC free after WO-014 op 20 ends T0+7 22:00? — no: WO-014 op20 occupies ELEC T0+4→T0+7 22:00, so 019 op10 → T0+8 06:00→T0+9 14:00), op 20 INT 28h → **T0+9 14:00→T0+11 10:00** (fills the freed WC-INT slot; remaining idle 36−28 = **8 h**), op 30 TEST 8h → T0+12? weekend → T0+14 06:00→14:00 then WO-014 op40 is pushed to T0+22 anyway. Explanations: ORDER_PULLED_FORWARD {orderCode WO-2026-019, lineCode LINE-2, days 29, materialCompleteness 1.0, workCenters [WC-ELEC, WC-INT]}, DOWNTIME_REDUCED {fromHours 36, toHours 8}.
4. KPI delta: downtime 36 → 8 h, late orders 1 (WO-014, 4 d). **Two different "moved" counts exist and must not be conflated** (see `docs/adr/0008-moved-operations-semantics.md`): the screen's headline — operations the solver moved relative to *before* (baseline + delay, un-resequenced) — and the engine's own count relative to the *approved baseline*, which additionally includes the operations the delay alone pushed. WO-015 op 30/40 may also move (T0+16 → T0+21..22).

Engine test `Act40DelayScenarioTest` asserts: WO-014 latenessDays == 4, WO-019 op 20 starts on T0+9 at WC-INT, kpi.downtimeHours == 8, explanations contain ORDER_PULLED_FORWARD for WO-2026-019 and DOWNTIME_REDUCED 36→8, baseline run (no delay) returns zero changed operations.

## Scenario 2 — "Zablokuj partię HTS-22-2608"

`HTS-22-2608` holds 800 kg. Lot status Accepted → Blocked (NCR created). Trace-forward: consumed by WO-2026-011 → serials PMV-2026-0007, PMV-2026-0008 → both passports `Generated` → **Invalidated** (`PassportInvalidated`). Reserved by WO-2026-018 → reservation flagged, order materialComplete=false (shortage HTS-22 400 kg, availableOn = PO-2026-0013 ETA T0+24). Risk for PO-2026-0013/1 rises (coverage factor).

**This is the real block** (`POST /lots/{lot}/block`, button **Zablokuj partię**), not the What-If tile of the same
name. The `BLOCK_LOT` scenario only simulates the material consequences in the plan and leaves every lot status
untouched — see the presenter note in [`multi-site.md`](multi-site.md).

## Other scenario tiles

All five presets exist on every plant, each resolved against that plant's own orders, lots and work centres.
Measured on Kielce (`SITE-01`) after a reset:

| Preset | Result on Kielce |
|---|---|
| `DELAY_ACT40_10D` (featured) | downtime 36 → 8 h, WO-2026-014 late 4 d, WO-2026-019 pulled forward 29 d; 3 moved by re-planning, 8 vs baseline |
| `DELAY_MCUX7_14D` | downtime 52 → 8 h; shortages on WO-2026-017 and WO-2026-018; WO-2026-019 pulled forward; no order goes late |
| `BLOCK_LOT_HTS22` | simulates the material loss only — **no lot status changes** (the real block is a separate action) |
| `PRIORITY_WO014` | WO-2026-014 is already priority 5 → single `NO_CHANGE` explanation, plan unchanged. This is a legitimate outcome, not a failure |
| `CAPACITY_INT_50` | `WC-INT` to 8 h/day → WO-2026-013 late 3 d, `CAPACITY_REDUCED` reported; 5 moved by re-planning |

Away from Kielce the `PRIORITY_WO014` tile targets that plant's own order, so its title (which names WO-2026-014)
reads wrong there — the subtitle shows the order actually used.

## Serials & passports

Kielce seeds **two Generated and two Draft** passports (each plant seeds one of each — see `multi-site.md`):

| Serial | Order | Status | Missing requirements |
|---|---|---|---|
| `PMV-2026-0007` | WO-2026-011 | Generated v1 | — (PDF rendered at seed by the production pipeline) |
| `PMV-2026-0008` | WO-2026-011 | Generated v1 | — |
| `SCM-2026-0101` | WO-2026-012 | Draft | `INSPECTION_RESULTS`, `APPROVAL` |
| `SCM-2026-0103` | WO-2026-012 | Draft | `KEY_COMPONENT_LOTS`, `SUPPLIER_ORIGIN`, `QC_STATUS`, `CERTIFICATES_WITH_HASH`, `INSPECTION_RESULTS`, `APPROVAL` |

A passport may only reach `Generated` through the render pipeline; seed data declaring `Generated` is mapped down to
`Approved` and the seed fails loudly if any `Generated` passport ends up without a version.

- Demo "complete passport" path uses `PMV-2026-0007` — generating again produces v2 and keeps v1.
- "Refused generation" path uses `SCM-2026-0103` → `422` with the missing list above.
- Passport template registry: `DQP-01` (demo) with requirement rows: PRODUCT_DATA, ORDER_REF, BOM_VERSION, KEY_COMPONENT_LOTS, SUPPLIER_ORIGIN, QC_STATUS, CERTIFICATES_WITH_HASH, INSPECTION_RESULTS, DEVIATIONS, APPROVAL.

## Demo accounts

Ten seeded accounts, listed with their roles in [`../demo-script/accounts.md`](../demo-script/accounts.md) and served
live by `GET /api/v1/auth/demo-accounts`. In the demo profile the UI logs in without a password (role switcher).
Each account's seeded password is `demo`, overridable for every account at once with `Demo__AccountPassword`.

## Definitions that are easy to get wrong

- **Predicted downtime** = idle hours inside the baseline windows of *material-waiting* operations only.
  Knock-on windows (operations pushed because a predecessor moved) are excluded, otherwise the figure double-counts.
- **`materialComplete`** shown in the UI and KPIs comes from business-api reservations (WO-2026-019 = 100 %),
  **not** from the engine's own allocation flag, which answers a narrower question and can read `false` for an order
  whose material is reserved and on hand.
- **Two "moved operations" counts exist and must not be conflated** — see
  [`ADR 0008`](../adr/0008-moved-operations-semantics.md). The screen's headline is *after vs before* (what
  re-planning changed); the engine's own `changed` flag is *after vs the approved baseline* and additionally counts
  operations the delay alone pushed. For scenario 1: **3** moved by re-planning, **8** differing from the baseline.
