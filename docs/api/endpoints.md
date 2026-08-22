# business-api surface (v1) — contract for the web app

Base `/api/v1`. JSON, camelCase, dates ISO-8601 UTC (`2026-09-15T04:00:00Z`), date-only as `YYYY-MM-DD`.
Errors: RFC 7807 Problem Details (`type`, `title`, `status`, `detail`, `errors?`, `traceId`).
Auth: `Authorization: Bearer <jwt>`. Lists return `{ items: T[], total: number }`. Every mutating endpoint accepts optional `Idempotency-Key` header and returns `ETag`/expects `If-Match` (rowVersion) on status edits → `412` on conflict.
SignalR hub `/hubs/live`, server→client method `DomainEvent(event: { name: string; occurredAt: string; correlationId: string; payload: object })`.

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
- `GET /admin/settings` → risk weights, objective weights, thresholds (read-only v1); `GET /admin/status` → `{ services:[{name:'postgres'|'minio'|'planning-engine'|'local-ai', status:'up'|'down'|'disabled', latencyMs}] , recentErrors:[{at, operation, message}] }`
- `GET /health/live`, `GET /health/ready`
