# Demo accounts

All passwords: `demo`. In the demo profile the UI auto-logs in as **presenter** and the role switcher (user menu) calls `GET /api/v1/auth/demo-login?role=…&supplierCode=…`.

| Username | Role | Scope | Use in the script |
|---|---|---|---|
| `presenter` | DemoPresenter | full demo path + reset, no security settings | default login, steps 1, 3, 8 |
| `planner` | ProductionPlanner | baseline, scenarios, approvals | steps 4–5 |
| `inbound` | InboundCoordinator | all deliveries, advices, logistics simulator | alternative to step 2 |
| `quality` | QualityInspector | lots, documents, inspections, passports | steps 6–7, scenario 2 |
| `director` | OperationsDirector | dashboard, KPIs, reports | step 1 |
| `auditor` | Auditor | audit history and export (read-only) | after approval |
| `admin` | Administrator | settings, thresholds, users, status page | troubleshooting |
| `supplier.hydromech` | SupplierUser | **SUP-02** Hydromech Actuators GmbH (ACT-40) | **step 2 — ETA +10 days on PO-2026-0007/1** |
| `supplier.nordstal` | SupplierUser | SUP-01 Nordstal (HTS-22) | lot HTS-22-2608 supplier |
| `supplier.vistula` | SupplierUser | SUP-03 Vistula Electronics (MCU-X7, ENC-4, …) | rejected report on PO-2026-0009 |

Supplier users only ever see their own purchase orders, shipments, lots and documents — enforced in the API (`ISupplierScope`), covered by `SupplierIsolationTests`.
