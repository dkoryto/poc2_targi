import { addDays, formatISO, startOfISOWeek, set } from 'date-fns';
import type { DemoScriptStep, GanttData, KpiResponse, LogisticsEvent, MapData, Notification, PurchaseOrderDetail, PurchaseOrderLine, PurchaseOrderSummary, QualityStatus, RiskHeatmap, RiskSummary, Shipment, Supplier, UserContext } from '@/api/types';

/** T0 = Monday of current ISO week, 06:00 (tests pin via T0_OVERRIDE). */
export const T0: Date = set(startOfISOWeek(new Date()), { hours: 6, minutes: 0, seconds: 0, milliseconds: 0 });
export const t0 = (days: number, hour = 6): string => formatISO(set(addDays(T0, days), { hours: hour, minutes: 0, seconds: 0, milliseconds: 0 }));
export const t0d = (days: number): string => formatISO(addDays(T0, days), { representation: 'date' });

export const presenter: UserContext = { id: 'u-presenter', username: 'presenter', displayName: 'Prezenter demo', role: 'DemoPresenter', siteId: 'SITE-01', locale: 'pl', demoMode: true };
export const supplierUser: UserContext = { id: 'u-sup2', username: 'supplier.hydromech', displayName: 'Hydromech Actuators GmbH', role: 'SupplierUser', supplierId: 'SUP-02', supplierName: 'Hydromech Actuators GmbH', siteId: 'SITE-01', locale: 'pl', demoMode: true };

export const kpis: KpiResponse = {
  asOf: t0(0, 9),
  items: [
    { code: 'MATERIAL_READINESS', value: 75, unit: '%', trend: -12.5, status: 'warn', definitionKey: 'kpi.def.MATERIAL_READINESS' },
    { code: 'OTIF', value: 86, unit: '%', trend: 2, status: 'ok', definitionKey: 'kpi.def.OTIF' },
    { code: 'HIGH_RISK_DELIVERIES', value: 3, unit: 'count', trend: 1, status: 'warn', definitionKey: 'kpi.def.HIGH_RISK_DELIVERIES' },
    { code: 'PREDICTED_DOWNTIME_H', value: 0, unit: 'h', trend: 0, status: 'ok', definitionKey: 'kpi.def.PREDICTED_DOWNTIME_H' },
    { code: 'ORDER_ON_TIME', value: 100, unit: '%', trend: 0, status: 'ok', definitionKey: 'kpi.def.ORDER_ON_TIME' },
    { code: 'PASSPORT_COMPLETENESS', value: 50, unit: '%', trend: 0, status: 'warn', definitionKey: 'kpi.def.PASSPORT_COMPLETENESS' },
  ],
};

export const suppliers: Supplier[] = [
  { code: 'SUP-01', name: 'Nordstal Sp. z o.o.', country: 'PL', city: 'Gdańsk', lat: 54.35, lon: 18.65, otif: 94, qualityScore: 92, riskScore: 18, openOrders: 3, activeShipments: 1 },
  { code: 'SUP-02', name: 'Hydromech Actuators GmbH', country: 'DE', city: 'Stuttgart', lat: 48.78, lon: 9.18, otif: 88, qualityScore: 90, riskScore: 44, openOrders: 2, activeShipments: 1 },
  { code: 'SUP-03', name: 'Vistula Electronics S.A.', country: 'PL', city: 'Kraków', lat: 50.06, lon: 19.94, otif: 91, qualityScore: 85, riskScore: 58, openOrders: 3, activeShipments: 2 },
  { code: 'SUP-04', name: 'Baltic Optics OÜ', country: 'EE', city: 'Tallinn', lat: 59.44, lon: 24.75, otif: 82, qualityScore: 88, riskScore: 55, openOrders: 2, activeShipments: 1 },
  { code: 'SUP-05', name: 'Carpathia Composites s.r.o.', country: 'CZ', city: 'Brno', lat: 49.19, lon: 16.61, otif: 90, qualityScore: 91, riskScore: 20, openOrders: 2, activeShipments: 1 },
  { code: 'SUP-06', name: 'Rhône Connectique SAS', country: 'FR', city: 'Lyon', lat: 45.76, lon: 4.84, otif: 96, qualityScore: 80, riskScore: 52, openOrders: 2, activeShipments: 2 },
  { code: 'SUP-07', name: 'Silesia Precision Sp. z o.o.', country: 'PL', city: 'Gliwice', lat: 50.29, lon: 18.67, otif: 97, qualityScore: 95, riskScore: 10, openOrders: 3, activeShipments: 2 },
  { code: 'SUP-08', name: 'Iberia Power Systems S.L.', country: 'ES', city: 'Zaragoza', lat: 41.65, lon: -0.89, otif: 85, qualityScore: 89, riskScore: 24, openOrders: 1, activeShipments: 2 },
];
const SITE = { code: 'SITE-01', name: 'Zakład Centralny', lat: 52.05, lon: 19.45 };
const cat = (score: number) => (score >= 75 ? 'Critical' : score >= 50 ? 'High' : score >= 25 ? 'Medium' : 'Low') as RiskSummary['category'];

export const act40Risk: RiskSummary = {
  score: 44,
  category: 'Medium',
  factors: [
    { code: 'ETA_DEVIATION', raw: 0, weight: 0.35, contribution: 0 },
    { code: 'CRITICALITY', raw: 100, weight: 0.15, contribution: 15 },
    { code: 'NO_ALTERNATIVE', raw: 100, weight: 0.1, contribution: 10 },
    { code: 'DOC_COMPLETENESS', raw: 50, weight: 0.15, contribution: 7.5 },
    { code: 'SUPPLIER_RELIABILITY', raw: 12, weight: 0.1, contribution: 1.2 },
    { code: 'COVERAGE', raw: 100, weight: 0.1, contribution: 10 },
    { code: 'LOGISTICS_EVENTS', raw: 0, weight: 0.05, contribution: 0 },
  ],
  endangeredOrders: [],
};
export const act40RiskAfter: RiskSummary = {
  ...act40Risk,
  score: 79,
  category: 'Critical',
  factors: act40Risk.factors.map((f) => (f.code === 'ETA_DEVIATION' ? { ...f, raw: 100, contribution: 35 } : f)),
  endangeredOrders: [
    { orderCode: 'WO-2026-014', requiredOn: t0d(9), shortage: 8 },
    { orderCode: 'WO-2026-015', requiredOn: t0d(16), shortage: 2 },
  ],
};

export const act40Line: PurchaseOrderLine = {
  id: 'line-0007-1', lineNo: 1, partCode: 'ACT-40', partName: 'Siłownik elektromechaniczny ACT-40', quantity: 12, unit: 'szt', requiredDate: t0d(9), eta: t0d(8), progressPercent: 100, status: 'Shipped', lotNumber: 'ACT-40-0911', heatNumber: null, producedOn: t0d(-6), expiresOn: null,
  risk: act40Risk,
  documents: [
    { id: 'doc-1', type: 'MATERIAL_CERT', fileName: 'cert-ACT-40-0911.pdf', sizeBytes: 182_000, sha256: 'a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6', status: 'Pending', uploadedAt: t0(-1, 12), uploadedBy: 'supplier.hydromech', lotNumber: 'ACT-40-0911', documentNumber: 'CERT-2026-0911' },
    { id: 'doc-2', type: 'TRANSPORT_DOC', fileName: 'cmr-31.pdf', sizeBytes: 90_000, sha256: 'ffeeddccbbaa99887766554433221100', status: 'Accepted', uploadedAt: t0(-1, 13), uploadedBy: 'supplier.hydromech', documentNumber: 'CMR-0031' },
  ],
  shipmentCode: 'SHP-2026-0031', rowVersion: '1',
};

export const po0007: PurchaseOrderDetail = {
  code: 'PO-2026-0007', supplier: suppliers[1]!, status: 'Shipped', orderedAt: t0d(-30), siteCode: 'SITE-01',
  lines: [act40Line],
  history: [
    { id: 'h1', occurredAt: t0(-1, 12), user: 'supplier.hydromech', action: 'StatusChanged', field: 'status', before: 'ReadyToShip', after: 'Shipped', comment: 'Wysyłka SHP-2026-0031' },
    { id: 'h2', occurredAt: t0(-7, 9), user: 'supplier.hydromech', action: 'StatusChanged', field: 'status', before: 'InProduction', after: 'ReadyToShip' },
  ],
};

export const poList: PurchaseOrderSummary[] = [
  { code: 'PO-2026-0007', supplierCode: 'SUP-02', supplierName: 'Hydromech Actuators GmbH', status: 'Shipped', orderedAt: t0d(-30), requiredDate: t0d(9), eta: t0d(8), lineCount: 1, riskScore: 44, riskCategory: 'Medium', progressPercent: 100, siteCode: 'SITE-01' },
  { code: 'PO-2026-0009', supplierCode: 'SUP-03', supplierName: 'Vistula Electronics S.A.', status: 'InProduction', orderedAt: t0d(-20), requiredDate: t0d(22), eta: t0d(25), lineCount: 2, riskScore: 58, riskCategory: 'High', progressPercent: 40, siteCode: 'SITE-01' },
  { code: 'PO-2026-0010', supplierCode: 'SUP-04', supplierName: 'Baltic Optics OÜ', status: 'Shipped', orderedAt: t0d(-25), requiredDate: t0d(12), eta: t0d(16), lineCount: 1, riskScore: 55, riskCategory: 'High', progressPercent: 100, siteCode: 'SITE-01' },
  { code: 'PO-2026-0011', supplierCode: 'SUP-06', supplierName: 'Rhône Connectique SAS', status: 'Confirmed', orderedAt: t0d(-15), requiredDate: t0d(10), eta: t0d(10), lineCount: 3, riskScore: 52, riskCategory: 'High', progressPercent: 20, siteCode: 'SITE-01' },
  { code: 'PO-2026-0012', supplierCode: 'SUP-02', supplierName: 'Hydromech Actuators GmbH', status: 'InProduction', orderedAt: t0d(-10), requiredDate: t0d(22), eta: t0d(20), lineCount: 1, riskScore: 22, riskCategory: 'Low', progressPercent: 30, siteCode: 'SITE-01' },
  { code: 'PO-2026-0013', supplierCode: 'SUP-01', supplierName: 'Nordstal Sp. z o.o.', status: 'Confirmed', orderedAt: t0d(-5), requiredDate: t0d(25), eta: t0d(24), lineCount: 1, riskScore: 15, riskCategory: 'Low', progressPercent: 0, siteCode: 'SITE-01' },
];

export const shipments: Shipment[] = [
  { code: 'SHP-2026-0031', poCode: 'PO-2026-0007', supplierCode: 'SUP-02', supplierName: 'Hydromech Actuators GmbH', carrier: 'TransEuro', vehicle: 'S-TR 4410', plannedDeparture: t0(-1, 8), eta: t0d(8), requiredDate: t0d(9), status: 'InTransit', riskScore: 44, riskCategory: 'Medium', progress: 0.35, lines: [{ lineId: 'line-0007-1', partCode: 'ACT-40', quantity: 12 }], events: [{ id: 'e1', type: 'Departed', occurredAt: t0(-1, 8), note: 'Stuttgart', user: 'supplier.hydromech' }] },
  { code: 'SHP-2026-0032', poCode: 'PO-2026-0010', supplierCode: 'SUP-04', supplierName: 'Baltic Optics OÜ', carrier: 'NordLink', vehicle: 'EST 221', plannedDeparture: t0(-3, 8), eta: t0d(16), requiredDate: t0d(12), status: 'Delayed', riskScore: 55, riskCategory: 'High', progress: 0.2, lines: [{ lineId: 'line-0010-1', partCode: 'OPT-12', quantity: 4 }], events: [] },
  { code: 'SHP-2026-0033', poCode: 'PO-2026-0005', supplierCode: 'SUP-07', supplierName: 'Silesia Precision Sp. z o.o.', carrier: 'Silesia Log', vehicle: 'SG 1234', plannedDeparture: t0(0, 7), eta: t0d(1), requiredDate: t0d(2), status: 'InTransit', riskScore: 8, riskCategory: 'Low', progress: 0.8, lines: [{ lineId: 'line-0005-1', partCode: 'GBX-7', quantity: 20 }], events: [] },
];

export const mapData: MapData = {
  site: SITE,
  suppliers: suppliers.map((s) => ({ code: s.code, name: s.name, country: s.country, city: s.city, lat: s.lat, lon: s.lon, riskScore: s.riskScore, riskCategory: cat(s.riskScore) })),
  shipments: shipments.map((sh) => {
    const sup = suppliers.find((s) => s.code === sh.supplierCode)!;
    return { code: sh.code, poCode: sh.poCode, supplierCode: sh.supplierCode, partCode: sh.lines[0]!.partCode, quantity: sh.lines[0]!.quantity, eta: sh.eta, requiredDate: sh.requiredDate ?? sh.eta, status: sh.status, riskScore: sh.riskScore, riskCategory: sh.riskCategory, progress: sh.progress, lat: sup.lat, lon: sup.lon, route: [[sup.lon, sup.lat], [SITE.lon, SITE.lat]] };
  }),
};

export const heatmap: RiskHeatmap = {
  rows: ['PL', 'DE', 'CZ', 'EE', 'FR', 'ES'],
  cols: ['Mechanika', 'Elektronika', 'Materiały', 'Optyka', 'Zasilanie'],
  cells: [
    { row: 'PL', col: 'Elektronika', score: 58, count: 2 }, { row: 'PL', col: 'Materiały', score: 15, count: 2 }, { row: 'PL', col: 'Mechanika', score: 10, count: 4 },
    { row: 'DE', col: 'Mechanika', score: 44, count: 2 }, { row: 'CZ', col: 'Materiały', score: 20, count: 2 }, { row: 'EE', col: 'Optyka', score: 55, count: 2 },
    { row: 'FR', col: 'Elektronika', score: 52, count: 3 }, { row: 'ES', col: 'Zasilanie', score: 24, count: 2 },
  ],
};

export const qualityStatus: QualityStatus = { passports: { draft: 8, pendingReview: 2, approved: 0, generated: 2, invalidated: 0 }, documents: { pending: 1, verifying: 2, accepted: 18, rejected: 1, requiresCompletion: 1 }, openNonConformances: 1, lotsBlocked: 1, readyForAcceptance: 2 };

export const plan: GanttData = {
  horizonStart: t0d(0), horizonEnd: t0d(84),
  workCenters: [
    { code: 'WC-CUT', name: 'Cięcie / obróbka', lineCode: 'LINE-1' }, { code: 'WC-WELD', name: 'Spawanie', lineCode: 'LINE-1' }, { code: 'WC-ELEC', name: 'Montaż elektroniki', lineCode: 'LINE-2' }, { code: 'WC-INT', name: 'Gniazdo integracji', lineCode: 'LINE-2' }, { code: 'WC-TEST', name: 'Testy końcowe', lineCode: 'LINE-1' },
  ],
  orders: [
    { code: 'WO-2026-012', productCode: 'P-COM-02', productName: 'Moduł bezpiecznej łączności', priority: 3, dueDate: t0d(9), status: 'InProgress', materialComplete: true, riskFlag: 'none' },
    { code: 'WO-2026-013', productCode: 'P-MOB-03', productName: 'Pojazd chronionej mobilności', priority: 4, dueDate: t0d(18), status: 'Released', materialComplete: true, riskFlag: 'none' },
    { code: 'WO-2026-014', productCode: 'P-OBS-01', productName: 'Bezzałogowa platforma obserwacyjna', priority: 5, dueDate: t0d(18), status: 'Released', materialComplete: false, riskFlag: 'warn' },
    { code: 'WO-2026-015', productCode: 'P-MOB-03', productName: 'Pojazd chronionej mobilności', priority: 3, dueDate: t0d(32), status: 'Released', materialComplete: false, riskFlag: 'none' },
    { code: 'WO-2026-019', productCode: 'P-COM-02', productName: 'Moduł bezpiecznej łączności', priority: 2, dueDate: t0d(53), status: 'Planned', materialComplete: true, riskFlag: 'none' },
  ],
  operations: [
    { orderCode: 'WO-2026-012', code: 'WO-2026-012/10', sequence: 10, workCenterCode: 'WC-ELEC', start: t0(0, 6), end: t0(1, 14), frozen: true, status: 'InProgress', materialWait: false },
    { orderCode: 'WO-2026-012', code: 'WO-2026-012/20', sequence: 20, workCenterCode: 'WC-INT', start: t0(2, 6), end: t0(2, 22), frozen: true, status: 'Planned', materialWait: false },
    { orderCode: 'WO-2026-012', code: 'WO-2026-012/30', sequence: 30, workCenterCode: 'WC-TEST', start: t0(3, 6), end: t0(3, 14), frozen: true, status: 'Planned', materialWait: false },
    { orderCode: 'WO-2026-013', code: 'WO-2026-013/10', sequence: 10, workCenterCode: 'WC-CUT', start: t0(0, 6), end: t0(1, 22), frozen: true, status: 'InProgress', materialWait: false },
    { orderCode: 'WO-2026-013', code: 'WO-2026-013/20', sequence: 20, workCenterCode: 'WC-WELD', start: t0(2, 6), end: t0(4, 22), frozen: false, status: 'Planned', materialWait: false },
    { orderCode: 'WO-2026-013', code: 'WO-2026-013/30', sequence: 30, workCenterCode: 'WC-INT', start: t0(7, 6), end: t0(8, 22), frozen: false, status: 'Planned', materialWait: false },
    { orderCode: 'WO-2026-013', code: 'WO-2026-013/40', sequence: 40, workCenterCode: 'WC-TEST', start: t0(9, 6), end: t0(9, 22), frozen: false, status: 'Planned', materialWait: false },
    { orderCode: 'WO-2026-014', code: 'WO-2026-014/10', sequence: 10, workCenterCode: 'WC-CUT', start: t0(2, 6), end: t0(2, 22), frozen: false, status: 'Planned', materialWait: false },
    { orderCode: 'WO-2026-014', code: 'WO-2026-014/20', sequence: 20, workCenterCode: 'WC-ELEC', start: t0(4, 6), end: t0(7, 22), frozen: false, status: 'Planned', materialWait: false },
    { orderCode: 'WO-2026-014', code: 'WO-2026-014/30', sequence: 30, workCenterCode: 'WC-INT', start: t0(9, 6), end: t0(11, 10), frozen: false, status: 'Planned', materialWait: false },
    { orderCode: 'WO-2026-014', code: 'WO-2026-014/40', sequence: 40, workCenterCode: 'WC-TEST', start: t0(14, 6), end: t0(14, 22), frozen: false, status: 'Planned', materialWait: false },
    { orderCode: 'WO-2026-015', code: 'WO-2026-015/10', sequence: 10, workCenterCode: 'WC-CUT', start: t0(3, 6), end: t0(3, 22), frozen: false, status: 'Planned', materialWait: false },
    { orderCode: 'WO-2026-015', code: 'WO-2026-015/20', sequence: 20, workCenterCode: 'WC-WELD', start: t0(8, 6), end: t0(9, 14), frozen: false, status: 'Planned', materialWait: false },
    { orderCode: 'WO-2026-015', code: 'WO-2026-015/30', sequence: 30, workCenterCode: 'WC-INT', start: t0(16, 6), end: t0(16, 22), frozen: false, status: 'Planned', materialWait: false },
    { orderCode: 'WO-2026-015', code: 'WO-2026-015/40', sequence: 40, workCenterCode: 'WC-TEST', start: t0(18, 6), end: t0(18, 14), frozen: false, status: 'Planned', materialWait: false },
    { orderCode: 'WO-2026-019', code: 'WO-2026-019/10', sequence: 10, workCenterCode: 'WC-ELEC', start: t0(37, 6), end: t0(38, 14), frozen: false, status: 'Planned', materialWait: false },
    { orderCode: 'WO-2026-019', code: 'WO-2026-019/20', sequence: 20, workCenterCode: 'WC-INT', start: t0(39, 6), end: t0(40, 18), frozen: false, status: 'Planned', materialWait: false },
    { orderCode: 'WO-2026-019', code: 'WO-2026-019/30', sequence: 30, workCenterCode: 'WC-TEST', start: t0(41, 6), end: t0(41, 14), frozen: false, status: 'Planned', materialWait: false },
  ],
  dependencies: [
    { from: 'WO-2026-014/10', to: 'WO-2026-014/20' }, { from: 'WO-2026-014/20', to: 'WO-2026-014/30' }, { from: 'WO-2026-014/30', to: 'WO-2026-014/40' },
    { from: 'WO-2026-019/10', to: 'WO-2026-019/20' }, { from: 'WO-2026-019/20', to: 'WO-2026-019/30' },
  ],
  conflicts: [],
};

/** "After" plan for the ACT-40 +10 d scenario (used by Gantt compare tests). */
export const planAfter: GanttData = {
  ...plan,
  operations: plan.operations.map((op) => {
    switch (op.code) {
      case 'WO-2026-014/30': return { ...op, start: t0(18, 6), end: t0(21, 10), changed: true, shiftDays: 9, materialWait: true };
      case 'WO-2026-014/40': return { ...op, start: t0(21, 10), end: t0(22, 10), changed: true, shiftDays: 7.2 };
      case 'WO-2026-019/10': return { ...op, start: t0(8, 6), end: t0(9, 14), changed: true, shiftDays: -29 };
      case 'WO-2026-019/20': return { ...op, start: t0(9, 14), end: t0(11, 10), changed: true, shiftDays: -29.7 };
      case 'WO-2026-019/30': return { ...op, start: t0(14, 6), end: t0(14, 14), changed: true, shiftDays: -27 };
      default: return op;
    }
  }),
  conflicts: [{ operationCode: 'WO-2026-014/30', reasonCode: 'ORDER_DELAYED_MATERIAL_SHORTAGE', params: { orderCode: 'WO-2026-014', partCode: 'ACT-40', missingQty: 8, days: 4, availableOn: t0d(18) } }],
};

export const logisticsEvents: LogisticsEvent[] = [
  { id: 'le-1', type: 'PORT_DISRUPTION', severity: 'MEDIUM', supplierCode: 'SUP-04', shipmentCode: 'SHP-2026-0032', description: 'Utrudnienia w porcie Tallinn', raisedAt: t0(-2, 10), active: true },
];

export const notifications: Notification[] = [
  { id: 'n-1', createdAt: t0(-1, 14), title: 'Dokument odrzucony', message: 'Raport kontroli PO-2026-0009/1 odrzucony przez kontrolera jakości.', severity: 'warn', read: false, route: '/supply/orders/PO-2026-0009', eventName: 'QualityDocumentRejected' },
  { id: 'n-2', createdAt: t0(-2, 10), title: 'Zdarzenie logistyczne', message: 'Utrudnienia portowe — SHP-2026-0032 (OPT-12).', severity: 'info', read: true, route: '/inbound/SHP-2026-0032', eventName: 'LogisticsRiskEventRaised' },
];

export const demoScript: DemoScriptStep[] = [1, 2, 3, 4, 5, 6, 7, 8, 9].map((n) => ({ step: n, titleKey: `demo.script.${n}.title`, descriptionKey: `demo.script.${n}.desc`, route: ['/', '/supply/orders/PO-2026-0007', '/', '/planning', '/planning', '/trace?q=PMV-2026-0007', '/passports/PMV-2026-0007', '/', '/trace?q=HTS-22-2608'][n - 1]! }));
