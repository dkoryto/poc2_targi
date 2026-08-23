import { http, HttpResponse, delay } from 'msw';
import * as F from './fixtures';
import { planFor, presetsFor, siteOf } from './sites';
import type { AdminSettings, AuditEvent, BlockLotRequest, CreateScenarioRequest, GanttData, Inspection, InspectionRequest, Lot, LotForward, LotSummary, MovedOperation, Passport, PassportSummary, PlanKpi, PlanningBaseline, PlanningScenario, PlanningScenarioSummary, ScenarioPreset, SerialTrace, TraceNode, TraceSearchHit } from '@/api/types';

const B = '/api/v1';
const { t0, t0d } = F;

// ---------------- planning ----------------
export const baselineKpi: PlanKpi = { downtimeHours: 0, lateOrders: 0, totalLatenessDays: 0, movedOperations: 0, ordersWithShortage: 0, onTimeRate: 1 };
export const kpiBeforeAct40: PlanKpi = { downtimeHours: 36, lateOrders: 1, totalLatenessDays: 4, movedOperations: 4, ordersWithShortage: 1, onTimeRate: 0.875 };
export const kpiAfterAct40: PlanKpi = { downtimeHours: 8, lateOrders: 1, totalLatenessDays: 4, movedOperations: 7, ordersWithShortage: 1, onTimeRate: 0.875 };

export const presets: ScenarioPreset[] = [
  { key: 'DELAY_ACT40_10D', titleKey: 'ACT40_DELAY', changes: [{ type: 'DELAY_INBOUND', poLineId: 'line-0007-1', days: 10, poCode: 'PO-2026-0007', partCode: 'ACT-40' }] },
  { key: 'DELAY_MCUX7_14D', titleKey: 'MCU_X7_DELAY', changes: [{ type: 'DELAY_INBOUND', poLineId: 'line-0009-1', days: 14, poCode: 'PO-2026-0009', partCode: 'MCU-X7' }] },
  { key: 'BLOCK_LOT_HTS22', titleKey: 'HTS22_BLOCK', changes: [{ type: 'BLOCK_LOT', lotNumber: 'HTS-22-2608' }] },
  { key: 'PRIORITY_WO014', titleKey: 'WO014_PRIORITY', changes: [{ type: 'PRIORITY', orderCode: 'WO-2026-014', priority: 5 }] },
  { key: 'CAPACITY_INT_50', titleKey: 'WC_INT_CAPACITY', changes: [{ type: 'CAPACITY', workCenterCode: 'WC-INT', factor: 0.5 }] },
];

/** Baseline + ACT-40 delay with NO re-sequencing ("Przed"). */
export const planBeforeAct40: GanttData = {
  ...F.plan,
  orders: F.plan.orders.map((o) => (o.code === 'WO-2026-014' ? { ...o, riskFlag: 'critical' as const } : o)),
  operations: F.plan.operations.map((op) => {
    switch (op.code) {
      case 'WO-2026-014/30': return { ...op, start: t0(18, 6), end: t0(21, 10), changed: true, shiftDays: 9, materialWait: true };
      case 'WO-2026-014/40': return { ...op, start: t0(22, 6), end: t0(22, 18), changed: true, shiftDays: 8 };
      case 'WO-2026-015/30': return { ...op, start: t0(22, 6), end: t0(22, 22), changed: true, shiftDays: 1 };
      case 'WO-2026-015/40': return { ...op, start: t0(23, 6), end: t0(23, 14), changed: true, shiftDays: 1 };
      default: return op;
    }
  }),
  conflicts: [{ operationCode: 'WO-2026-014/30', reasonCode: 'ORDER_DELAYED_MATERIAL_SHORTAGE', params: { orderCode: 'WO-2026-014', partCode: 'ACT-40', missingQty: 8, days: 4, availableOn: t0d(18) } }],
};
export const planAfterAct40: GanttData = { ...F.planAfter, orders: planBeforeAct40.orders };

export function movedOps(before: GanttData, after: GanttData): MovedOperation[] {
  const b = new Map(before.operations.map((o) => [o.code, o]));
  return after.operations
    .filter((o) => { const p = b.get(o.code); return p && (p.start !== o.start || p.end !== o.end || p.workCenterCode !== o.workCenterCode); })
    .map((o) => { const p = b.get(o.code)!; return { operationCode: o.code, orderCode: o.orderCode, workCenterCode: o.workCenterCode, before: { start: p.start, end: p.end }, after: { start: o.start, end: o.end }, shiftDays: Math.round(((new Date(o.start).getTime() - new Date(p.start).getTime()) / 86_400_000) * 10) / 10 }; });
}

export const act40Explanations: PlanningScenario['explanations'] = [
  { reasonCode: 'ORDER_DELAYED_MATERIAL_SHORTAGE', orderCode: 'WO-2026-014', params: { orderCode: 'WO-2026-014', partCode: 'ACT-40', missingQty: 8, days: 4, availableOn: t0d(18) } },
  { reasonCode: 'ORDER_PULLED_FORWARD', orderCode: 'WO-2026-019', params: { orderCode: 'WO-2026-019', lineCode: 'LINE-2', days: 29, materialCompleteness: 1, workCenters: ['WC-ELEC', 'WC-INT'] } },
  { reasonCode: 'DOWNTIME_REDUCED', orderCode: '', params: { fromHours: 36, toHours: 8 } },
  { reasonCode: 'ORDER_LATE_DUE', orderCode: 'WO-2026-014', params: { orderCode: 'WO-2026-014', days: 4 } },
];

let baselineVersion = 1;
type MockScenario = PlanningScenario & { siteCode?: string };
let scenarios: MockScenario[] = [];
let seq = 0;

export function baseline(): PlanningBaseline {
  return { id: `bl-${baselineVersion}`, version: baselineVersion, approvedAt: baselineVersion === 1 ? t0(-7, 9) : new Date().toISOString(), approvedBy: baselineVersion === 1 ? 'planner' : 'presenter', gantt: baselineVersion === 1 ? F.plan : planAfterAct40, kpi: baselineVersion === 1 ? baselineKpi : kpiAfterAct40 };
}
function summary(s: PlanningScenario): PlanningScenarioSummary {
  return { id: s.id, name: s.name, status: s.status, createdAt: s.createdAt, createdBy: s.createdBy, solver: s.solver, changeCount: s.changes.length, kpiAfter: s.kpiAfter };
}
function complete(s: PlanningScenario): PlanningScenario {
  const isAct40 = s.changes.some((c) => c.type === 'DELAY_INBOUND' && c.poLineId === 'line-0007-1');
  const isCapacity = s.changes.some((c) => c.type === 'CAPACITY');
  if (isAct40) {
    return { ...s, status: 'Completed', solver: 'dspc-heuristic/1.0', elapsedMs: 187, before: planBeforeAct40, after: planAfterAct40, kpiBefore: kpiBeforeAct40, kpiAfter: kpiAfterAct40, explanations: act40Explanations, consequences: [{ kind: 'warn', text: 'WO-2026-014: termin przekroczony o 4 dni (brak 8 szt. ACT-40 do ' + t0d(18) + ').' }, { kind: 'info', text: 'WO-2026-019 wciągnięte na gniazdo integracji — luka po ACT-40 wypełniona w 78 %.' }] };
  }
  if (isCapacity) {
    return { ...s, status: 'Completed', solver: 'Heuristic fallback', elapsedMs: 3004, before: F.plan, after: F.plan, kpiBefore: baselineKpi, kpiAfter: { ...baselineKpi, lateOrders: 2, totalLatenessDays: 5, onTimeRate: 0.75 }, explanations: [{ reasonCode: 'FALLBACK_USED', orderCode: '', params: { reason: 'timeout' } }, { reasonCode: 'CAPACITY_REDUCED', orderCode: '', params: { workCenterCode: 'WC-INT', factor: 0.5 } }], consequences: [] };
  }
  return { ...s, status: 'Completed', solver: 'dspc-heuristic/1.0', elapsedMs: 92, before: F.plan, after: F.plan, kpiBefore: baselineKpi, kpiAfter: baselineKpi, explanations: [{ reasonCode: 'NO_CHANGE', orderCode: '', params: {} }], consequences: [] };
}

// ---------------- trace / lots ----------------
const SHA = (seed: string) => Array.from({ length: 64 }, (_, i) => '0123456789abcdef'[(seed.charCodeAt(i % seed.length) * (i + 7)) % 16]).join('');

export const lotsList: LotSummary[] = [
  { lotNumber: 'HTS-22-2607', heatNumber: 'H-44107', partCode: 'HTS-22', partName: 'Stal wysokowytrzymała HTS-22', supplierCode: 'SUP-01', supplierName: 'Nordstal Sp. z o.o.', quantity: 600, unit: 'kg', status: 'Accepted', receivedOn: t0d(-20) },
  { lotNumber: 'HTS-22-2608', heatNumber: 'H-44108', partCode: 'HTS-22', partName: 'Stal wysokowytrzymała HTS-22', supplierCode: 'SUP-01', supplierName: 'Nordstal Sp. z o.o.', quantity: 400, unit: 'kg', status: 'Accepted', receivedOn: t0d(-33) },
  { lotNumber: 'HTS-22-2611', heatNumber: 'H-44111', partCode: 'HTS-22', partName: 'Stal wysokowytrzymała HTS-22', supplierCode: 'SUP-01', supplierName: 'Nordstal Sp. z o.o.', quantity: 300, unit: 'kg', status: 'AwaitingInspection', receivedOn: t0d(-1) },
  { lotNumber: 'ACT-40-0911', partCode: 'ACT-40', partName: 'Siłownik elektromechaniczny ACT-40', supplierCode: 'SUP-02', supplierName: 'Hydromech Actuators GmbH', quantity: 4, unit: 'szt', status: 'Accepted', receivedOn: t0d(-14) },
  { lotNumber: 'MCU-X7-0455', partCode: 'MCU-X7', partName: 'Moduł sterujący MCU-X7', supplierCode: 'SUP-03', supplierName: 'Vistula Electronics S.A.', quantity: 26, unit: 'szt', status: 'Accepted', receivedOn: t0d(-10) },
  { lotNumber: 'CON-5-1142', partCode: 'CON-5', partName: 'Złącze hermetyczne CON-5', supplierCode: 'SUP-06', supplierName: 'Rhône Connectique SAS', quantity: 200, unit: 'szt', status: 'Blocked', receivedOn: t0d(-12) },
];
let lots: Record<string, Lot> = {};
function seedLots() {
  lots = {};
  for (const l of lotsList) {
    lots[l.lotNumber] = {
      ...l,
      poCode: l.partCode === 'HTS-22' ? 'PO-2026-0003' : l.partCode === 'ACT-40' ? 'PO-2026-0004' : l.partCode === 'MCU-X7' ? 'PO-2026-0006' : 'PO-2026-0011',
      producedOn: t0d(-40),
      expiresOn: null,
      documents: [{ id: `doc-${l.lotNumber}`, type: 'MATERIAL_CERT', fileName: `cert-${l.lotNumber}.pdf`, sizeBytes: 120_000, sha256: SHA(l.lotNumber), status: l.status === 'AwaitingInspection' ? 'RequiresCompletion' : 'Accepted', uploadedAt: t0(-30, 10), uploadedBy: 'supplier.nordstal', lotNumber: l.lotNumber, documentNumber: `CERT-${l.lotNumber}` }],
      inspections: l.status === 'Accepted' || l.status === 'Blocked' ? [{ id: `insp-${l.lotNumber}`, result: l.status === 'Blocked' ? 'Failed' : 'Passed', notes: l.status === 'Blocked' ? 'Wymiar poza tolerancją (NCR-2026-004)' : 'Zgodna z certyfikatem', inspectedAt: t0(-28, 11), inspector: 'quality' }] : [],
      consumedBy: l.lotNumber === 'HTS-22-2608' ? [{ orderCode: 'WO-2026-011', serials: ['PMV-2026-0007', 'PMV-2026-0008'] }] : l.lotNumber === 'MCU-X7-0455' ? [{ orderCode: 'WO-2026-012', serials: ['SCM-2026-0101', 'SCM-2026-0102', 'SCM-2026-0103'] }] : [],
      reservedBy: l.lotNumber === 'HTS-22-2608' ? ['WO-2026-018'] : l.lotNumber === 'HTS-22-2607' ? ['WO-2026-013', 'WO-2026-015'] : l.lotNumber === 'ACT-40-0911' ? ['WO-2026-013'] : [],
      nonConformances: l.status === 'Blocked' ? [{ id: 'ncr-4', code: 'NCR-2026-004', title: 'Wymiar poza tolerancją', status: 'Open', raisedAt: t0(-11, 9) }] : [],
      rowVersion: '1',
    };
  }
}
export function lotForward(lotNumber: string): LotForward {
  const l = lots[lotNumber]!;
  const orders = [...l.consumedBy.map((c) => ({ orderCode: c.orderCode, status: 'Completed', relation: 'Consumed' as const })), ...l.reservedBy.map((o) => ({ orderCode: o, status: 'Planned', relation: 'Reserved' as const }))];
  const serials = l.consumedBy.flatMap((c) => c.serials.map((s) => ({ serial: s, orderCode: c.orderCode, productCode: s.startsWith('PMV') ? 'P-MOB-03' : 'P-COM-02' })));
  return { lot: l, orders, serials, passports: serials.map((s) => ({ serial: s.serial, status: passports[s.serial]?.status ?? 'Draft' })) };
}

function node(kind: string, code: string, label: string, status: string | null, children: TraceNode[] = [], meta?: Record<string, unknown>): TraceNode {
  return { kind, code, label, status, children, meta: meta ?? null };
}
export function serialTrace(serial: string): SerialTrace | null {
  if (serial.startsWith('PMV-2026-000')) {
    const lot = lots['HTS-22-2608']!;
    const act = lots['ACT-40-0911']!;
    return {
      serial, productCode: 'P-MOB-03', productName: 'Pojazd chronionej mobilności', orderCode: 'WO-2026-011', bomVersion: 'BOM-P-MOB-03/v3', status: 'Completed', passportStatus: passports[serial]?.status ?? 'Draft',
      components: [
        { partCode: 'HTS-22', partName: 'Stal wysokowytrzymała HTS-22', lotNumber: 'HTS-22-2608', heatNumber: 'H-44108', supplierCode: 'SUP-01', supplierName: 'Nordstal Sp. z o.o.', country: 'PL', certSha256: SHA('HTS-22-2608') },
        { partCode: 'ACT-40', partName: 'Siłownik elektromechaniczny ACT-40', lotNumber: 'ACT-40-0911', supplierCode: 'SUP-02', supplierName: 'Hydromech Actuators GmbH', country: 'DE', certSha256: SHA('ACT-40-0911') },
        { partCode: 'MCU-X7', partName: 'Moduł sterujący MCU-X7', lotNumber: 'MCU-X7-0455', supplierCode: 'SUP-03', supplierName: 'Vistula Electronics S.A.', country: 'PL', certSha256: SHA('MCU-X7-0455') },
      ],
      genealogy: node('Serial', serial, 'Pojazd chronionej mobilności', 'Completed', [
        node('Order', 'WO-2026-011', 'P-MOB-03 × 2', 'Completed', [
          node('Operation', 'WO-2026-011/10', 'WC-CUT · cięcie', 'Completed', [
            node('Consumption', `${serial}/HTS-22`, 'HTS-22 × 200 kg', null, [
              node('Lot', 'HTS-22-2608', 'Stal HTS-22 · 400 kg', lot.status, [
                node('Inspection', 'insp-HTS-22-2608', 'Kontrola wejściowa', 'Passed', [], { inspectedAt: t0(-28, 11), result: 'Passed' }),
                node('Document', 'CERT-HTS-22-2608', 'Certyfikat materiałowy 3.1', 'Accepted', [], { documentId: 'doc-HTS-22-2608', sha256: SHA('HTS-22-2608'), fileName: 'cert-HTS-22-2608.pdf' }),
                node('PurchaseOrder', 'PO-2026-0003', 'Nordstal · HTS-22 1000 kg', 'Delivered', [
                  node('Shipment', 'SHP-2026-0019', 'Gdańsk → SITE-01', 'Delivered', [node('Supplier', 'SUP-01', 'Nordstal Sp. z o.o.', null, [], { country: 'PL' })]),
                ]),
              ], { partCode: 'HTS-22', partName: 'Stal wysokowytrzymała HTS-22', heatNumber: 'H-44108', quantity: 400, unit: 'kg' }),
            ]),
          ]),
          node('Operation', 'WO-2026-011/30', 'WC-INT · integracja', 'Completed', [
            node('Consumption', `${serial}/ACT-40`, 'ACT-40 × 2', null, [
              node('Lot', 'ACT-40-0911', 'Siłownik ACT-40', act.status, [
                node('Document', 'CERT-ACT-40-0911', 'Certyfikat materiałowy', 'Accepted', [], { documentId: 'doc-ACT-40-0911', sha256: SHA('ACT-40-0911'), fileName: 'cert-ACT-40-0911.pdf' }),
                node('PurchaseOrder', 'PO-2026-0004', 'Hydromech · ACT-40 4 szt', 'Delivered', [node('Supplier', 'SUP-02', 'Hydromech Actuators GmbH', null, [], { country: 'DE' })]),
              ], { partCode: 'ACT-40', quantity: 4, unit: 'szt' }),
            ]),
            node('Consumption', `${serial}/MCU-X7`, 'MCU-X7 × 1', null, [
              node('Lot', 'MCU-X7-0455', 'Moduł MCU-X7', 'Accepted', [node('Supplier', 'SUP-03', 'Vistula Electronics S.A.', null, [], { country: 'PL' })], { partCode: 'MCU-X7', quantity: 26, unit: 'szt' }),
            ]),
          ]),
        ]),
        node('Passport', serial, 'Paszport DQP-01', passports[serial]?.status ?? 'Draft', [], { version: passports[serial]?.versions.length ?? 0 }),
      ]),
    };
  }
  if (serial.startsWith('SCM-2026-01')) {
    return {
      serial, productCode: 'P-COM-02', productName: 'Moduł bezpiecznej łączności', orderCode: 'WO-2026-012', bomVersion: 'BOM-P-COM-02/v2', status: 'InProgress', passportStatus: passports[serial]?.status ?? 'Draft',
      components: [{ partCode: 'MCU-X7', partName: 'Moduł sterujący MCU-X7', lotNumber: 'MCU-X7-0455', supplierCode: 'SUP-03', supplierName: 'Vistula Electronics S.A.', country: 'PL', certSha256: serial === 'SCM-2026-0103' ? null : SHA('MCU-X7-0455') }],
      genealogy: node('Serial', serial, 'Moduł bezpiecznej łączności', 'InProgress', [
        node('Order', 'WO-2026-012', 'P-COM-02 × 10', 'InProgress', [
          node('Operation', 'WO-2026-012/10', 'WC-ELEC · montaż', 'InProgress', [
            node('Consumption', `${serial}/MCU-X7`, 'MCU-X7 × 1', null, [node('Lot', 'MCU-X7-0455', 'Moduł MCU-X7', 'Accepted', [node('Supplier', 'SUP-03', 'Vistula Electronics S.A.', null, [], { country: 'PL' })], { partCode: 'MCU-X7' })]),
          ]),
        ]),
        node('Passport', serial, 'Paszport DQP-01', passports[serial]?.status ?? 'Draft'),
      ]),
    };
  }
  return null;
}
export const searchIndex: TraceSearchHit[] = [
  { kind: 'Serial', code: 'PMV-2026-0007', label: 'Pojazd chronionej mobilności · WO-2026-011' },
  { kind: 'Serial', code: 'PMV-2026-0008', label: 'Pojazd chronionej mobilności · WO-2026-011' },
  { kind: 'Serial', code: 'SCM-2026-0101', label: 'Moduł bezpiecznej łączności · WO-2026-012' },
  { kind: 'Serial', code: 'SCM-2026-0103', label: 'Moduł bezpiecznej łączności · WO-2026-012' },
  { kind: 'Lot', code: 'HTS-22-2608', label: 'Stal HTS-22 · Nordstal · 400 kg' },
  { kind: 'Lot', code: 'HTS-22-2607', label: 'Stal HTS-22 · Nordstal · 600 kg' },
  { kind: 'Heat', code: 'H-44108', label: 'Wytop → HTS-22-2608' },
  { kind: 'PurchaseOrder', code: 'PO-2026-0007', label: 'Hydromech · ACT-40 × 12' },
  { kind: 'Order', code: 'WO-2026-014', label: 'Bezzałogowa platforma obserwacyjna × 4' },
  { kind: 'Document', code: 'CERT-HTS-22-2608', label: 'Certyfikat materiałowy 3.1' },
];

// ---------------- passports ----------------
const REQ = ['PRODUCT_DATA', 'ORDER_REF', 'BOM_VERSION', 'KEY_COMPONENT_LOTS', 'SUPPLIER_ORIGIN', 'QC_STATUS', 'CERTIFICATES_WITH_HASH', 'INSPECTION_RESULTS', 'DEVIATIONS', 'APPROVAL'];
let passports: Record<string, Passport> = {};
function mkPassport(serial: string, productCode: string, orderCode: string, status: Passport['status'], missing: Passport['completeness']['missing'], versions: Passport['versions']): Passport {
  const missingReq = new Set(missing.map((m) => (m.code === 'INSPECTION_RESULT' ? 'INSPECTION_RESULTS' : m.code === 'CERT' ? 'CERTIFICATES_WITH_HASH' : m.code)));
  if (status === 'Draft' || status === 'PendingReview') missingReq.add('APPROVAL');
  return {
    serial, productCode, productName: productCode === 'P-MOB-03' ? 'Pojazd chronionej mobilności' : 'Moduł bezpiecznej łączności', orderCode, bomVersion: productCode === 'P-MOB-03' ? 'BOM-P-MOB-03/v3' : 'BOM-P-COM-02/v2', status, templateCode: 'DQP-01',
    completeness: { complete: missing.length === 0, missing, requirements: REQ.map((code) => ({ code, satisfied: !missingReq.has(code) || (code === 'APPROVAL' && missing.length === 0 && status !== 'Draft' && status !== 'PendingReview'), evidence: code === 'APPROVAL' ? (status === 'Approved' || status === 'Generated' ? 'quality · ' + t0d(-5) : null) : code === 'CERTIFICATES_WITH_HASH' && !missingReq.has(code) ? '3 × SHA-256' : code === 'INSPECTION_RESULTS' && !missingReq.has(code) ? 'FAT ' + t0d(-6) : null })) },
    components: serialTrace(serial)?.components?.map((c) => ({ partCode: c.partCode, partName: c.partName, lotNumber: c.lotNumber, supplierCode: c.supplierCode, supplierName: c.supplierName, country: c.country, certSha256: c.certSha256 })) ?? [],
    inspections: missingReq.has('INSPECTION_RESULTS') ? [] : [{ id: `fat-${serial}`, result: 'Passed', notes: 'Test końcowy (FAT) zaliczony', inspectedAt: t0(-6, 13), inspector: 'quality' }],
    deviations: serial === 'PMV-2026-0008' ? [{ id: 'dev-1', code: 'DEV-2026-002', title: 'Zamienny wariant uszczelnień SEAL-3', status: 'Approved', approvedBy: 'quality', approvedAt: t0(-7, 10) }] : [],
    versions, approvedBy: status === 'Approved' || status === 'Generated' ? 'quality' : null, approvedAt: status === 'Approved' || status === 'Generated' ? t0(-5, 9) : null,
  };
}
function seedPassports() {
  passports = {};
  passports['PMV-2026-0007'] = mkPassport('PMV-2026-0007', 'P-MOB-03', 'WO-2026-011', 'Generated', [], [{ version: 1, generatedAt: t0(-5, 10), generatedBy: 'quality', sha256: SHA('PMV-2026-0007-v1'), fileSize: 248_113, status: 'Current' }]);
  passports['PMV-2026-0008'] = mkPassport('PMV-2026-0008', 'P-MOB-03', 'WO-2026-011', 'Generated', [], [{ version: 1, generatedAt: t0(-5, 10), generatedBy: 'quality', sha256: SHA('PMV-2026-0008-v1'), fileSize: 251_902, status: 'Current' }]);
  passports['SCM-2026-0101'] = mkPassport('SCM-2026-0101', 'P-COM-02', 'WO-2026-012', 'Draft', [{ code: 'INSPECTION_RESULT' }], []);
  passports['SCM-2026-0102'] = mkPassport('SCM-2026-0102', 'P-COM-02', 'WO-2026-012', 'Draft', [{ code: 'INSPECTION_RESULT' }], []);
  passports['SCM-2026-0103'] = mkPassport('SCM-2026-0103', 'P-COM-02', 'WO-2026-012', 'Draft', [{ code: 'CERT', params: { partCode: 'MCU-X7', lotNumber: 'MCU-X7-0455' } }, { code: 'INSPECTION_RESULT' }], []);
}
function passportSummary(p: Passport): PassportSummary {
  return { serial: p.serial, productCode: p.productCode, productName: p.productName, orderCode: p.orderCode, status: p.status, templateCode: p.templateCode, complete: p.completeness.complete, missingCount: p.completeness.missing.length, updatedAt: p.versions[0]?.generatedAt ?? t0(-2, 8), latestVersion: p.versions.length ? Math.max(...p.versions.map((v) => v.version)) : null };
}

// ---------------- audit / admin ----------------
let audit: AuditEvent[] = [];
function seedAudit() {
  audit = [
    { id: 'a-1', occurredAt: t0(-5, 10), user: 'quality', action: 'PassportGenerated', entity: 'Passport', entityCode: 'PMV-2026-0007', before: { status: 'Approved', version: 0 }, after: { status: 'Generated', version: 1, sha256: SHA('PMV-2026-0007-v1').slice(0, 16) }, correlationId: 'c0ffee01-0000-4000-8000-000000000001', source: 'web' },
    { id: 'a-2', occurredAt: t0(-7, 9), user: 'planner', action: 'BaselineApproved', entity: 'PlanningBaseline', entityCode: 'v1', before: null, after: { version: 1, operations: 21 }, correlationId: 'c0ffee01-0000-4000-8000-000000000002', source: 'web' },
    { id: 'a-3', occurredAt: t0(-1, 14), user: 'quality', action: 'DocumentRejected', entity: 'QualityDocument', entityCode: 'PO-2026-0009/1', before: { status: 'Verifying' }, after: { status: 'Rejected', comment: 'Brak podpisu' }, correlationId: 'c0ffee01-0000-4000-8000-000000000003', source: 'web' },
  ];
}
function pushAudit(action: string, entity: string, entityCode: string, before: unknown, after: unknown, user: string) {
  audit.unshift({ id: `a-${Date.now()}-${++seq}`, occurredAt: new Date().toISOString(), user, action, entity, entityCode, before, after, correlationId: crypto.randomUUID(), source: 'web' });
}
export const adminSettings: AdminSettings = {
  riskWeights: [{ code: 'ETA_DEVIATION', weight: 0.35 }, { code: 'CRITICALITY', weight: 0.15 }, { code: 'NO_ALTERNATIVE', weight: 0.1 }, { code: 'DOC_COMPLETENESS', weight: 0.15 }, { code: 'SUPPLIER_RELIABILITY', weight: 0.1 }, { code: 'COVERAGE', weight: 0.1 }, { code: 'LOGISTICS_EVENTS', weight: 0.05 }],
  objectiveWeights: [{ code: 'latenessPerDayPerPriority', value: 10 }, { code: 'shortagePerUnit', value: 5 }, { code: 'downtimePerHour', value: 20 }, { code: 'deliveryBreachPerOrder', value: 100 }, { code: 'changePerMovedOperation', value: 2 }, { code: 'changeoverPerSwitch', value: 8 }],
  thresholds: [{ code: 'RISK_MEDIUM', value: 25 }, { code: 'RISK_HIGH', value: 50 }, { code: 'RISK_CRITICAL', value: 75 }, { code: 'NOTIFY_RISK', value: 50 }, { code: 'SOLVER_TIMEOUT_MS', value: 3000, unit: 'ms' }, { code: 'DEMO_RESET_MS', value: 10000, unit: 'ms' }],
};

const PNG_1x1 = Uint8Array.from(atob('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=='), (c) => c.charCodeAt(0));

export function getPassport(serial: string): Passport | undefined {
  return passports[serial];
}

export function resetWave2() {
  baselineVersion = 1;
  scenarios = [];
  seedLots();
  seedPassports();
  seedAudit();
}
resetWave2();

export function userName(): string {
  return 'presenter';
}
let currentRole: () => { username: string; role: string } = () => ({ username: 'presenter', role: 'DemoPresenter' });
export function bindCurrentUser(fn: () => { username: string; role: string }) {
  currentRole = fn;
}

export const wave2Handlers = [
  // planning
  http.get(`${B}/planning/baseline`, ({ request }) => {
    const site = siteOf(request);
    const b = baseline();
    return HttpResponse.json(site === 'SITE-01' ? b : { ...b, id: `bl-${site}`, gantt: planFor(site, F.plan), kpi: baselineKpi });
  }),
  http.get(`${B}/planning/scenarios/presets`, ({ request }) => HttpResponse.json(presetsFor(siteOf(request), presets))),
  http.get(`${B}/planning/scenarios`, ({ request }) => {
    const site = siteOf(request);
    const items = scenarios.filter((sc) => (sc.siteCode ?? 'SITE-01') === site).map(summary);
    return HttpResponse.json({ items, total: items.length });
  }),
  http.post(`${B}/planning/scenarios`, async ({ request }) => {
    const body = (await request.json()) as CreateScenarioRequest;
    const sc: MockScenario = { id: `sc-${++seq}`, name: body.name, status: 'Draft', createdAt: new Date().toISOString(), createdBy: currentRole().username, changes: body.changes, siteCode: siteOf(request) };
    scenarios = [sc, ...scenarios];
    return HttpResponse.json(sc, { status: 201 });
  }),
  http.get(`${B}/planning/scenarios/:id`, ({ params }) => {
    const sc = scenarios.find((s) => s.id === params.id);
    return sc ? HttpResponse.json(sc) : HttpResponse.json({ title: 'Not found', status: 404 }, { status: 404 });
  }),
  http.post(`${B}/planning/scenarios/:id/run`, async ({ params }) => {
    const sc = scenarios.find((s) => s.id === params.id);
    if (!sc) return HttpResponse.json({ title: 'Not found', status: 404 }, { status: 404 });
    sc.status = 'Running';
    setTimeout(() => { const i = scenarios.findIndex((s) => s.id === sc.id); if (i >= 0) scenarios[i] = complete(scenarios[i]!); }, 700);
    return HttpResponse.json({ id: sc.id, status: 'Running' }, { status: 202 });
  }),
  http.get(`${B}/planning/scenarios/:id/compare`, ({ params }) => {
    const sc = scenarios.find((s) => s.id === params.id);
    if (!sc?.after || !sc.before) return HttpResponse.json({ movedOperations: [], kpiDelta: {} });
    const moved = movedOps(F.plan, sc.after);
    return HttpResponse.json({ movedOperations: moved, kpiDelta: { downtimeHours: (sc.kpiAfter?.downtimeHours ?? 0) - (sc.kpiBefore?.downtimeHours ?? 0) } });
  }),
  http.post(`${B}/planning/scenarios/:id/approve`, async ({ params }) => {
    const sc = scenarios.find((s) => s.id === params.id);
    if (!sc) return HttpResponse.json({ title: 'Not found', status: 404 }, { status: 404 });
    const u = currentRole();
    if (!['ProductionPlanner', 'DemoPresenter'].includes(u.role)) return HttpResponse.json({ title: 'Forbidden', status: 403 }, { status: 403 });
    await delay(150);
    baselineVersion += 1;
    Object.assign(sc, { status: 'Approved', approvedAt: new Date().toISOString(), approvedBy: u.username, baselineVersion });
    pushAudit('PlanApproved', 'PlanningScenario', sc.id, { baselineVersion: baselineVersion - 1 }, { baselineVersion, movedOperations: sc.kpiAfter?.movedOperations }, u.username);
    return HttpResponse.json(sc);
  }),
  http.post(`${B}/planning/scenarios/:id/reject`, ({ params }) => { const sc = scenarios.find((s) => s.id === params.id); if (sc) sc.status = 'Rejected'; return HttpResponse.json(sc ?? {}); }),
  http.post(`${B}/planning/scenarios/:id/save`, ({ params }) => { const sc = scenarios.find((s) => s.id === params.id); if (sc) sc.status = 'Saved'; return HttpResponse.json(sc ?? {}); }),

  // trace
  http.get(`${B}/trace/search`, ({ request }) => {
    const q = (new URL(request.url).searchParams.get('q') ?? '').toLowerCase();
    if (siteOf(request) !== 'SITE-01') return HttpResponse.json([]);
    return HttpResponse.json(searchIndex.map((h) => ({ ...h, siteCode: 'SITE-01' })).filter((h) => h.code.toLowerCase().includes(q) || h.label.toLowerCase().includes(q)));
  }),
  http.get(`${B}/trace/serials/:serial`, ({ params }) => {
    const tr = serialTrace(String(params.serial));
    return tr ? HttpResponse.json(tr) : HttpResponse.json({ title: 'Not found', status: 404, detail: String(params.serial) }, { status: 404 });
  }),
  http.get(`${B}/trace/lots/:lot/forward`, ({ params }) => (lots[String(params.lot)] ? HttpResponse.json(lotForward(String(params.lot))) : HttpResponse.json({ title: 'Not found', status: 404 }, { status: 404 }))),
  http.get(`${B}/trace/audit`, ({ request }) => {
    const url = new URL(request.url);
    if (url.searchParams.get('format') === 'csv') return new HttpResponse('id,occurredAt,user,action\n', { headers: { 'Content-Type': 'text/csv' } });
    const code = url.searchParams.get('code');
    const items = audit.filter((a) => !code || a.entityCode === code);
    return HttpResponse.json({ items, total: items.length });
  }),
  http.get(`${B}/lots`, ({ request }) => {
    const url = new URL(request.url);
    if (siteOf(request) !== 'SITE-01') return HttpResponse.json({ items: [], total: 0 });
    let items = Object.values(lots).map(({ lotNumber, heatNumber, partCode, partName, supplierCode, supplierName, quantity, unit, status, receivedOn }) => ({ lotNumber, heatNumber, partCode, partName, supplierCode, supplierName, quantity, unit, status, receivedOn }));
    const st = url.searchParams.get('status'); if (st) items = items.filter((l) => l.status === st);
    const pc = url.searchParams.get('partCode'); if (pc) items = items.filter((l) => l.partCode.toLowerCase().includes(pc.toLowerCase()));
    const q = url.searchParams.get('q'); if (q) items = items.filter((l) => l.lotNumber.toLowerCase().includes(q.toLowerCase()));
    return HttpResponse.json({ items, total: items.length });
  }),
  http.get(`${B}/lots/:lot`, ({ params }) => (lots[String(params.lot)] ? HttpResponse.json(lots[String(params.lot)]) : HttpResponse.json({ title: 'Not found', status: 404 }, { status: 404 }))),
  http.post(`${B}/lots/:lot/block`, async ({ params, request }) => {
    const l = lots[String(params.lot)];
    if (!l) return HttpResponse.json({ title: 'Not found', status: 404 }, { status: 404 });
    const body = (await request.json()) as BlockLotRequest;
    await delay(150);
    const fwd = lotForward(l.lotNumber);
    l.status = 'Blocked';
    l.nonConformances = [...(l.nonConformances ?? []), { id: `ncr-${++seq}`, code: `NCR-2026-00${seq}`, title: body.ncrTitle, status: 'Open', raisedAt: new Date().toISOString() }];
    const invalidated: string[] = [];
    for (const p of fwd.passports) {
      const pp = passports[p.serial];
      if (pp && (pp.status === 'Generated' || pp.status === 'Approved')) {
        pp.status = 'Invalidated'; pp.invalidatedAt = new Date().toISOString(); pp.invalidationReason = `Partia ${l.lotNumber} zablokowana: ${body.reason}`;
        pp.versions = pp.versions.map((v) => ({ ...v, status: 'Invalidated' }));
        invalidated.push(p.serial);
      }
    }
    pushAudit('MaterialLotBlocked', 'MaterialLot', l.lotNumber, { status: 'Accepted' }, { status: 'Blocked', reason: body.reason, ncr: body.ncrTitle }, currentRole().username);
    return HttpResponse.json({ lot: l, affected: { orders: fwd.orders.map((o) => o.orderCode), serials: fwd.serials.map((s) => s.serial), passports: invalidated } });
  }),
  http.post(`${B}/lots/:lot/inspections`, async ({ params, request }) => {
    const l = lots[String(params.lot)];
    if (!l) return HttpResponse.json({ title: 'Not found', status: 404 }, { status: 404 });
    const body = (await request.json()) as InspectionRequest;
    const insp: Inspection = { id: `insp-${++seq}`, result: body.result, notes: body.notes ?? null, inspectedAt: body.inspectedAt, inspector: currentRole().username };
    l.inspections = [insp, ...l.inspections];
    if (l.status === 'AwaitingInspection' && body.result === 'Passed') l.status = 'Accepted';
    if (body.result === 'Conditional') l.status = 'ConditionallyReleased';
    return HttpResponse.json(insp, { status: 201 });
  }),
  http.get(`${B}/documents/:id/download`, () => new HttpResponse(new Blob(['%PDF-1.4 mock'], { type: 'application/pdf' }), { headers: { 'Content-Type': 'application/pdf' } })),

  // passports
  http.get(`${B}/passports`, ({ request }) => {
    const url = new URL(request.url);
    if (siteOf(request) !== 'SITE-01') return HttpResponse.json({ items: [], total: 0 });
    let items = Object.values(passports).map(passportSummary);
    const st = url.searchParams.get('status'); if (st) items = items.filter((p) => p.status === st);
    const q = url.searchParams.get('q'); if (q) items = items.filter((p) => p.serial.toLowerCase().includes(q.toLowerCase()));
    return HttpResponse.json({ items, total: items.length });
  }),
  http.get(`${B}/passports/:serial`, ({ params }) => (passports[String(params.serial)] ? HttpResponse.json(passports[String(params.serial)]) : HttpResponse.json({ title: 'Not found', status: 404 }, { status: 404 }))),
  http.post(`${B}/passports/:serial/approve`, ({ params }) => {
    const p = passports[String(params.serial)];
    if (!p) return HttpResponse.json({ title: 'Not found', status: 404 }, { status: 404 });
    if (!p.completeness.complete) return HttpResponse.json({ title: 'Unprocessable', status: 422, missing: p.completeness.missing }, { status: 422 });
    p.status = 'Approved'; p.approvedBy = currentRole().username; p.approvedAt = new Date().toISOString();
    p.completeness.requirements = p.completeness.requirements.map((r) => (r.code === 'APPROVAL' ? { ...r, satisfied: true, evidence: `${p.approvedBy} · ${t0d(0)}` } : r));
    return HttpResponse.json(p);
  }),
  http.post(`${B}/passports/:serial/generate`, async ({ params }) => {
    const p = passports[String(params.serial)];
    if (!p) return HttpResponse.json({ title: 'Not found', status: 404 }, { status: 404 });
    if (!p.completeness.complete) return HttpResponse.json({ type: 'about:blank', title: 'Passport incomplete', status: 422, detail: 'Completeness rules not satisfied', missing: p.completeness.missing }, { status: 422 });
    if (p.status !== 'Approved' && p.status !== 'Generated') return HttpResponse.json({ title: 'Passport must be approved first', status: 409 }, { status: 409 });
    await delay(300);
    const version = p.versions.length + 1;
    p.versions = [{ version, generatedAt: new Date().toISOString(), generatedBy: currentRole().username, sha256: SHA(`${p.serial}-v${version}-${Date.now()}`), fileSize: 240_000 + version * 1111, status: 'Current' }, ...p.versions.map((v) => ({ ...v, status: 'Superseded' as const }))];
    p.status = 'Generated';
    pushAudit('PassportGenerated', 'Passport', p.serial, { version: version - 1 }, { version, sha256: p.versions[0]!.sha256.slice(0, 16) }, currentRole().username);
    return HttpResponse.json({ version, sha256: p.versions[0]!.sha256, downloadUrl: `${B}/passports/${p.serial}/versions/${version}/pdf` }, { status: 201 });
  }),
  http.get(`${B}/passports/:serial/versions/:v/pdf`, () => new HttpResponse(new Blob(['%PDF-1.4 mock passport'], { type: 'application/pdf' }), { headers: { 'Content-Type': 'application/pdf' } })),
  http.get(`${B}/passports/:serial/qr`, () => new HttpResponse(PNG_1x1, { headers: { 'Content-Type': 'image/png' } })),

  // audit / admin
  http.get(`${B}/audit`, ({ request }) => {
    const url = new URL(request.url);
    if (url.searchParams.get('format') === 'csv') return new HttpResponse('id,occurredAt,user,action,entity,entityCode\n' + audit.map((a) => [a.id, a.occurredAt, a.user, a.action, a.entity, a.entityCode].join(',')).join('\n'), { headers: { 'Content-Type': 'text/csv' } });
    let items = audit;
    const e = url.searchParams.get('entity'); if (e) items = items.filter((a) => a.entity === e);
    const c = url.searchParams.get('code'); if (c) items = items.filter((a) => a.entityCode.toLowerCase().includes(c.toLowerCase()));
    const u = url.searchParams.get('user'); if (u) items = items.filter((a) => a.user.toLowerCase().includes(u.toLowerCase()));
    return HttpResponse.json({ items, total: items.length });
  }),
  http.get(`${B}/admin/settings`, () => HttpResponse.json(adminSettings)),
];
