# business-api surface (v1) — contract for the web app

Base `/api/v1`. JSON, camelCase, dates ISO-8601 UTC (`2026-09-15T04:00:00Z`), date-only as `YYYY-MM-DD`.
Errors: RFC 7807 Problem Details (`type`, `title`, `status`, `detail`, `errors?`, `traceId`).
Auth: `Authorization: Bearer <jwt>`. Lists return `{ items: T[], total: number }`. Every mutating endpoint accepts optional `Idempotency-Key` header and returns `ETag`/expects `If-Match` (rowVersion) on status edits → `412` on conflict.
SignalR hub `/hubs/live`, server→client method `DomainEvent(event: { name: string; occurredAt: string; correlationId: string; payload: object })`.

## Multi-site

The demonstrator runs four plants (`SITE-01` Kielce … `SITE-04` Leszno). Every listing and dashboard endpoint below
accepts an optional **`?siteCode=`**; omitting it uses the caller's default plant. Unknown plant → `404`, a plant the
caller may not see → `403`. `GET /sites` lists the plants available to the caller and `GET /auth/me` returns
`siteCode` plus `availableSites`. Full contract and the per-plant scenarios: `docs/architecture/multi-site.md`.

Exceptions: `/notifications` and `/audit` are organisation-wide (neither entity carries a plant) — see
`docs/adr/0007-multi-site-scoping.md`.

- `GET /sites` → `[{ code, name, city, country, lat, lon, timeZone, profileKey, featuredScenarioKey, isDefault }]`

## Identity
- `POST /auth/login { username, password } → { accessToken, expiresAt, user: UserContext }`
- `GET /auth/me → UserContext { id, username, displayName, role, supplierId?, supplierName?, siteId, locale, demoMode }`
- `GET /auth/demo-login?role=ProductionPlanner&supplierCode=SUP-02` (demo profile only) → same as login
- `GET /auth/demo-accounts` (demo only) → `[{ username, role, supplierCode?, description }]`

## Dashboard
- `GET /dashboard/kpis → { asOf, items: Kpi[] }` where `Kpi { code: 'MATERIAL_READINESS'|'OTIF'|'HIGH_RISK_DELIVERIES'|'PREDICTED_DOWNTIME_H'|'ORDER_ON_TIME'|'PASSPORT_COMPLETENESS', value, unit: '%'|'h'|'count', trend: number (delta vs prev period), status: 'ok'|'warn'|'critical', definitionKey }`
- `GET /dashboard/map → { site: {code,name,lat,lon}, suppliers: [{code,name,country,city,lat,lon,riskScore,riskCategory}], shipments: [{code, poCode, supplierCode, partCode, quantity, eta, requiredDate, status, riskScore, riskCategory, progress:0..1, lat, lon, route:[[lon,lat]...] }] }`
- `GET /dashboard/risk-heatmap → { rows: ['PL','DE','CZ',...regions], cols: ['Mechanika','Elektronika','Materiały','Optyka','Zasilanie'] (category codes), cells:[{row,col,score,count}] }`
- `GET /dashboard/quality-status → { passports: {draft,pendingReview,approved,generated,invalidated}, documents:{pending,verifying,accepted,rejected,requiresCompletion}, openNonConformances, lotsBlocked, readyForAcceptance }`
- `GET /dashboard/plan → GanttData` (current baseline)  — `GanttData { horizonStart, horizonEnd, workCenters:[{code,name,lineCode}], orders:[{code,productCode,productName,priority,dueDate,status,materialComplete,riskFlag:'none'|'warn'|'critical'}], operations:[{orderCode,code,sequence,workCenterCode,start,end,frozen,status,materialWait:boolean,changed?:boolean,shiftDays?:number}], dependencies:[{from,to}], conflicts:[{operationCode, reasonCode, params}] }`

## Suppliers & inbound (supplier users see only own supplierId; others see all)
- `GET /suppliers`, `GET /suppliers/{code}` → `{ code,name,country,city,lat,lon,otif,qualityScore,riskScore,openOrders,activeShipments }`
- `GET /purchase-orders?status&supplierCode&riskCategory&siteCode&dueFrom&dueTo&q` → `PurchaseOrderSummary[]`
- `GET /purchase-orders/{code}` → `{ code, supplier, status, orderedAt, lines: PurchaseOrderLine[] , history: ChangeEntry[] }`
  `PurchaseOrderLine { id, lineNo, partCode, partName, quantity, unit, requiredDate, eta, progressPercent, status: 'Confirmed'|'InProduction'|'QualityControl'|'ReadyToShip'|'Shipped'|'Delivered'|'OnHold', lotNumber?, heatNumber?, producedOn?, expiresOn?, risk: RiskSummary, documents: DocumentSummary[], shipmentCode?, rowVersion }`
  `RiskSummary { score, category:'Low'|'Medium'|'High'|'Critical', factors:[{code, raw, weight, contribution}], endangeredOrders:[{orderCode, requiredOn, shortage}] }`
- `PATCH /purchase-orders/{code}/lines/{lineId} { status?, progressPercent?, lotNumber?, heatNumber?, producedOn?, expiresOn?, quantity?, eta?, comment? }` (If-Match) → line; raises `SupplierOrderStatusChanged` / `ShipmentEtaChanged`
- `POST /purchase-orders/{code}/lines/{lineId}/eta { eta, reason, comment? }` → `{ line, risk: RiskSummary, endangeredOrders }` (the demo-critical call)
- `GET /purchase-orders/{code}/lines/{lineId}/impact` → `{ risk, endangeredOrders, predictedDowntimeHours }` (supplier sees only own line + counts, never other suppliers' data)
- `POST /shipments (advice) { poCode, lineIds[], carrier, vehicle, plannedDeparture, eta }` → Shipment; `GET /shipments`, `GET /shipments/{code}`, `POST /shipments/{code}/events { type, occurredAt, note }`
- `GET /logistics-events`, `POST /logistics-events { type:'BORDER_DELAY'|'PORT_DISRUPTION'|'WEATHER'|'QUALITY_ISSUE'|'PARTIAL_DELIVERY'|'NO_CONFIRMATION', severity, supplierCode?, shipmentCode?, description }` (simulator, demo + InboundCoordinator)

## Documents
- `POST /documents (multipart: file, type:'MATERIAL_CERT'|'INSPECTION_REPORT'|'DECLARATION_OF_CONFORMITY'|'TRANSPORT_DOC', poLineId?, lotNumber?, heatNumber?, documentNumber, issuedOn)` → `DocumentSummary { id, type, fileName, sizeBytes, sha256, status:'Pending'|'Verifying'|'Accepted'|'Rejected'|'RequiresCompletion', uploadedAt, uploadedBy, lotNumber?, aiSuggestion? }`
- `GET /documents?poLineId|lotNumber`, `GET /documents/{id}/download` (backend streams from storage), `POST /documents/{id}/verify { status, comment }` (QualityInspector)
- `POST /documents/{id}/ai-extract` (feature flag `LocalAi:Enabled`; otherwise 404) → `{ fields:[{name,value,confidence,source:{page,snippet}}], suggestedType, issues:[] , status:'Proposal' }`

## Quality / lots / inventory
- `GET /lots?partCode&status&q`, `GET /lots/{lotNumber}` → `{ lotNumber, heatNumber?, partCode, supplierCode, poLineId, quantity, unit, receivedOn, producedOn?, expiresOn?, status:'AwaitingInspection'|'Accepted'|'ConditionallyReleased'|'Blocked'|'Recalled', documents, inspections, consumedBy:[{orderCode, serials[]}], reservedBy:[orderCode] }`
- `POST /lots/{lotNumber}/block { reason, ncrTitle }` → `{ lot, affected: { orders:[], serials:[], passports:[] } }` raises `MaterialLotBlocked`
- `POST /lots/{lotNumber}/inspections { result:'Passed'|'Failed'|'Conditional', notes, inspectedAt }`
- `GET /inventory?partCode` → `[{ partCode, partName, unit, onHand, reserved, blocked, free, inbound:[{poLine, qty, eta}], coverageDays }]`
- `GET /non-conformances`

## Planning
- `GET /planning/baseline` → `{ id, version, approvedAt, approvedBy, gantt: GanttData, kpi: PlanKpi }`
- `GET /planning/scenarios`, `GET /planning/scenarios/{id}`
- `POST /planning/scenarios { name, changes: ScenarioChange[] }` → scenario (status `Draft`)
  `ScenarioChange = { type:'DELAY_INBOUND', poLineId, days } | { type:'BLOCK_LOT', lotNumber } | { type:'PRIORITY', orderCode, priority } | { type:'CAPACITY', workCenterCode, factor } | { type:'DELAY_ORDER', orderCode, days }`
- `GET /planning/scenarios/presets` → the 5 tiles `[{ key, titleKey, changes }]`
- `POST /planning/scenarios/{id}/run` → `202 { id, status:'Running' }`; completion pushed via `PlanningScenarioCompleted`; `GET /planning/scenarios/{id}` then returns `{ ..., status:'Completed'|'Failed', solver:'dspc-heuristic/1.0'|'Heuristic fallback', elapsedMs, before: GanttData (baseline+changes, unreplanned), after: GanttData, kpiBefore: PlanKpi, kpiAfter: PlanKpi, explanations:[{reasonCode, orderCode, params}], consequences:[{kind, text params}] }`
- `POST /planning/scenarios/{id}/approve` (ProductionPlanner) → new baseline version, `ProductionPlanApproved`; `POST .../reject`; `POST .../save`
- `GET /planning/scenarios/{id}/compare` → `{ movedOperations:[{operationCode, orderCode, workCenterCode, before:{start,end}, after:{start,end}, shiftDays}], kpiDelta }`

## Traceability & passports
- `GET /trace/search?q=` → `[{ kind:'Serial'|'Lot'|'Heat'|'PurchaseOrder'|'Document'|'Order', code, label }]`
- `GET /trace/serials/{serial}` → `{ serial, productCode, productName, orderCode, bomVersion, status, genealogy: TraceNode }` where `TraceNode { kind, code, label, status?, children: TraceNode[] }` (trace-back: serial → order → operations → consumptions → lots → PO line → shipment → supplier → documents)
- `GET /trace/lots/{lotNumber}/forward` → `{ lot, orders:[], serials:[], passports:[{serial, status}] }`
- `GET /trace/audit?entity&code&from&to` (+ `?format=csv` export)
- `GET /passports?status`, `GET /passports/{serial}` → `{ serial, productCode, orderCode, status:'Draft'|'PendingReview'|'Approved'|'Generated'|'Invalidated', templateCode:'DQP-01', completeness:{ complete:boolean, missing:[{code, labelKey, params}] , requirements:[{code, satisfied, evidence}] }, components:[{partCode, lotNumber, supplierCode, country, certSha256?}], inspections:[], deviations:[], versions:[{version, generatedAt, generatedBy, sha256, fileSize, status}] }`
- `POST /passports/{serial}/approve` (QualityInspector) ; `POST /passports/{serial}/generate` → `{ version, sha256, downloadUrl }` (only if complete; else `422` with `missing[]`); `GET /passports/{serial}/versions/{v}/pdf`; `GET /passports/{serial}/qr` (PNG)

## Notifications, audit, demo, admin, health
- `GET /notifications?unreadOnly`, `POST /notifications/{id}/read`
- `GET /audit?entity&code&user&from&to&page` → `{ items:[{ id, occurredAt, user, action, entity, entityCode, before, after, correlationId, source }] }`
- `POST /demo/reset` (DemoPresenter/Administrator, demo only) → `{ durationMs, seedVersion, counts:{...} }`; `GET /demo/script` → presenter steps; `GET /demo/status` → `{ demoMode, seedVersion, seededAt, lastResetMs }`
- `GET /admin/settings` → `{ riskWeights: [{code, weight}], riskWeightsSum, riskNotifyThreshold, objectiveWeights: [{code, weight}], solverTimeLimitMs, horizonWeeks, demoMode, localAiEnabled, storageProvider, timeZone }` (read-only v1; weight codes are the canonical upper-snake codes from `docs/architecture/risk-model.md` and the objective keys `LATENESS_PER_DAY_PER_PRIORITY`, `SHORTAGE_PER_UNIT`, `DOWNTIME_PER_HOUR`, `DELIVERY_BREACH_PER_ORDER`, `CHANGE_PER_MOVED_OPERATION`, `CHANGEOVER_PER_SWITCH` — the UI localises them by code); `GET /admin/status` → `{ services:[{name:'postgres'|'minio'|'planning-engine'|'local-ai', status:'up'|'down'|'disabled', latencyMs}] , recentErrors:[{at, operation, message}] }`
- `GET /health/live`, `GET /health/ready`

## Added by web (wave 1) — assumptions the UI relies on; backend must honour or adjust `apps/web/src/api/types.ts`
- `POST /purchase-orders/{code}/lines/{lineId}/eta` `reason` enum: `PRODUCTION_DELAY|LOGISTICS|QUALITY|CAPACITY|MATERIAL_SHORTAGE|OTHER`.
- `POST /shipments/{code}/events` `type` enum: `Departed|BorderCrossed|Delayed|Arrived|Note`; `Shipment` DTO includes `supplierName`, `requiredDate?`, `progress` 0..1, `lines:[{lineId, partCode, quantity}]`, `events[]`.
- `PurchaseOrderSummary` fields used by the list: `supplierCode, supplierName, status, orderedAt, requiredDate (min of lines), eta (max of lines), lineCount, riskScore (max), riskCategory, progressPercent (avg), siteCode`.
- `PurchaseOrderLine.risk` is a full `RiskSummary` (score, category, factors[], endangeredOrders[]); `documents[].documentNumber` optional.
- `ChangeEntry { id, occurredAt, user, action, field?, before?, after?, comment? }` for PO `history`.
- `LogisticsEvent { id, type, severity, supplierCode?, shipmentCode?, description, raisedAt, active }`; `GET /logistics-events` returns `{items,total}`. Event name `LogisticsRiskEventRaised` is expected on the hub.
- `Notification { id, createdAt, title, message, severity:'info'|'warn'|'critical', read, route?, eventName? }`; `POST /notifications/{id}/read` → 204.
- `DomainEvent.payload` keys the UI reads: `code | poCode | shipmentCode | supplierCode`, `category` + `previousCategory` (for `DeliveryRiskChanged` pulse/toast), `lotNumber`, `serial`.
- `GET /demo/script` items: `{ step, titleKey, descriptionKey, route, action? }` where keys are i18n keys (`demo.script.N.title` / `.desc` exist in the web bundle for N=1..9). If the API returns an empty list the UI falls back to its built-in 9 steps.
- `GET /health/live` is polled by the top bar every 10 s (any 2xx = online).

## Added by web (wave 2) — shapes the planning / trace / passport / audit / admin screens consume (see `apps/web/src/api/types.ts`)
- `GET /planning/scenarios/presets` → `[{ key, titleKey, changes }]` with keys `ACT40_DELAY | MCU_X7_DELAY | HTS22_BLOCK | WO014_PRIORITY | WC_INT_CAPACITY` and `titleKey = "planning.presets.<key>"` (UI localises; falls back to the raw key). `DELAY_INBOUND` changes may carry `poCode` and `partCode` for display.
- `GET /planning/scenarios` → `{ items: [{ id, name, status, createdAt, createdBy, solver?, changeCount, kpiAfter? }], total }`. `status ∈ Draft|Running|Completed|Failed|Approved|Rejected|Saved`.
- `GET /planning/scenarios/{id}` → `{ id, name, status, createdAt, createdBy, changes, solver?, elapsedMs?, before?, after?, kpiBefore?, kpiAfter?, explanations?: [{reasonCode, orderCode, params}], consequences?: [{ kind:'info'|'warn'|'critical', text?, textKey?, params? }], approvedAt?, approvedBy?, baselineVersion?, errorMessage? }`. The UI polls every 1 s while `Running` and also reacts to `PlanningScenarioCompleted` (payload `scenarioId`).
- `POST .../approve|reject|save` return the updated scenario. `approve` is `403` for roles other than ProductionPlanner/DemoPresenter.
- `GET /planning/scenarios/{id}/compare` → `{ movedOperations:[{operationCode, orderCode, workCenterCode, before:{start,end}, after:{start,end}, shiftDays}], kpiDelta }`.

**Two "moved operations" counts, deliberately named apart** (see `docs/adr/0008-moved-operations-semantics.md`):
- `kpiAfter.movedOperations` — operations **re-planning** moved, i.e. `after` vs `before` (the un-resequenced plan). This is the headline on the result screen, matches `compare.movedOperations.length`, and matches the operations the `after` Gantt flags as `changed` (their `shiftDays` are measured against `before` too). `kpiBefore.movedOperations` is `0` by definition — "before" is the reference.
- `changesVsBaseline` on the scenario — the engine's own count against the **approved baseline**, which also includes operations the scenario's event alone pushed. Shown separately and labelled as such.

The planning engine's contract is unchanged: it still reports `changed`/`shiftDays` against the baseline it is handed. The business API re-anchors those markers onto `before` before serving them, so every consumer sees one definition.
- `GET /trace/search?q=` → `[{ kind: Serial|Lot|Heat|PurchaseOrder|Document|Order|Passport, code, label }]`.
- `GET /trace/serials/{serial}` → `{ serial, productCode, productName, orderCode, bomVersion, status, passportStatus?, components?: [{partCode, partName?, lotNumber, heatNumber?, supplierCode, supplierName?, country?, certSha256?}], genealogy: TraceNode }` with `TraceNode { kind, code, label, status?, children[], meta? }`; `meta` keys the UI understands: `documentId`, `fileName`, `sha256`, `partCode`, `partName`, `heatNumber`, `country`, `quantity`, `unit`, `*At/*On` dates. Node kinds rendered: Serial, Order, Operation, Consumption, Lot, Heat, PurchaseOrder, Shipment, Supplier, Document, Inspection, Passport. If `components` is omitted the UI derives the table from the tree.
- `GET /lots` → `{ items: LotSummary[], total }` (`lotNumber, heatNumber?, partCode, partName?, supplierCode, supplierName?, quantity, unit, status, receivedOn`). `GET /lots/{lot}` adds `poCode?, producedOn?, expiresOn?, documents[], inspections[{id,result:Passed|Failed|Conditional,notes?,inspectedAt,inspector?}], consumedBy[{orderCode, serials[]}], reservedBy[], nonConformances?[{id,code,title,status,raisedAt}]`.
- `GET /trace/lots/{lot}/forward` → `{ lot: LotSummary, orders:[{orderCode,status,relation:Consumed|Reserved}], serials:[{serial,orderCode,productCode}], passports:[{serial,status}] }`.
- `POST /lots/{lot}/block { reason, ncrTitle }` → `{ lot, affected:{ orders[], serials[], passports[] } }` (passports = serials whose passport was invalidated).
- `GET /passports` → `{ items: [{ serial, productCode, productName?, orderCode, status, templateCode, complete, missingCount?, updatedAt?, latestVersion? }], total }`.
- `GET /passports/{serial}` → `{ …, completeness:{ complete, missing:[{code, labelKey?, params?}], requirements:[{code, satisfied, evidence?}] }, components[], inspections[], deviations[{id,code?,title,status,approvedBy?,approvedAt?}], versions[{version, generatedAt, generatedBy, sha256, fileSize, status:Current|Superseded|Invalidated}], approvedBy?, approvedAt?, invalidatedAt?, invalidationReason? }`. Requirement codes = DQP-01 rows (`PRODUCT_DATA … APPROVAL`); missing codes the UI localises: `INSPECTION_RESULT`, `CERT {partCode, lotNumber}`, `APPROVAL`, `QC_STATUS`, `DOCUMENT {type}`, `COMPONENT_LOT {partCode}`, `DEVIATION_APPROVAL`, `BOM_VERSION`, `PRODUCT_DATA`, `ORDER_REF`, `SUPPLIER_ORIGIN`, `CERT_HASH` (or send `labelKey`).
- `POST /passports/{serial}/generate` → `201 { version, sha256, downloadUrl }`; `422` Problem Details with `missing[]`; `409` when not approved. `GET .../versions/{v}/pdf` and `GET .../qr` are fetched with the bearer token (blob → object URL).
- `GET /audit` / `GET /trace/audit` → `{ items: [{ id, occurredAt, user, action, entity, entityCode, before?, after?, correlationId, source }], total }`, filters `entity, code, user, from, to, page, pageSize`; `format=csv` returns `text/csv`.
- `GET /admin/settings` → `{ riskWeights:[{code, weight}], objectiveWeights:[{code, value}], thresholds:[{code, value, unit?}] }` (threshold codes `RISK_MEDIUM|RISK_HIGH|RISK_CRITICAL|NOTIFY_RISK|SOLVER_TIMEOUT_MS|DEMO_RESET_MS`).
- `GET /documents/{id}/download` returns the file with `Content-Disposition: attachment`.

## Added by web (multi-site)

The plant contract itself lives in `docs/architecture/multi-site.md`. The UI additionally consumes two
**optional** fields; both degrade to "not shown" when the API omits them, so neither is required:

- `DemoScriptStep.siteCode?` — the plant a presenter step is told against. When present and different from
  the active plant, the presenter panel offers a one-click switch (`presenter-switch-site-<step>`).
- `TraceSearchHit.siteCode?` — lets a search hit from another plant be labelled with its plant chip.

`Site.highRiskDeliveries?` (optional) is rendered as a count badge in the plant switcher when supplied;
without it the switcher shows name + city only.
