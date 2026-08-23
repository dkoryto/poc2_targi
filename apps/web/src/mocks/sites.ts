import * as F from './fixtures';
import type { GanttData, KpiResponse, MapData, PurchaseOrderSummary, QualityStatus, RiskHeatmap, ScenarioPreset, Site } from '@/api/types';

const { t0, t0d } = F;

/** The four demo plants — see docs/architecture/multi-site.md. */
export const SITES: Site[] = [
  { code: 'SITE-01', name: 'Zakład Kielce', city: 'Kielce', country: 'PL', lat: 50.87, lon: 20.63, timeZone: 'Europe/Warsaw', profileKey: 'ASSEMBLY_INTEGRATION', featuredScenarioKey: 'DELAY_ACT40_10D', isDefault: true, highRiskDeliveries: 3 },
  { code: 'SITE-02', name: 'Zakład Piła', city: 'Piła', country: 'PL', lat: 53.15, lon: 16.74, timeZone: 'Europe/Warsaw', profileKey: 'ELECTRONICS_COMMS', featuredScenarioKey: 'DELAY_MCUX7_14D', highRiskDeliveries: 2 },
  { code: 'SITE-03', name: 'Zakład Zamość', city: 'Zamość', country: 'PL', lat: 50.72, lon: 23.25, timeZone: 'Europe/Warsaw', profileKey: 'STRUCTURES_ARMOUR', featuredScenarioKey: 'BLOCK_LOT_HTS22', highRiskDeliveries: 1 },
  { code: 'SITE-04', name: 'Zakład Leszno', city: 'Leszno', country: 'PL', lat: 51.84, lon: 16.58, timeZone: 'Europe/Warsaw', profileKey: 'INTEGRATION_TEST', featuredScenarioKey: 'CAPACITY_INT_50', highRiskDeliveries: 0 },
];

export const DEFAULT_SITE = 'SITE-01';
export function siteOf(request: Request): string {
  const code = new URL(request.url).searchParams.get('siteCode');
  return code && SITES.some((s) => s.code === code) ? code : DEFAULT_SITE;
}

const PREFIX: Record<string, string> = { 'SITE-01': '', 'SITE-02': 'PIL-', 'SITE-03': 'ZAM-', 'SITE-04': 'LES-' };
const WO_BAND: Record<string, string> = { 'SITE-01': '0', 'SITE-02': '1', 'SITE-03': '2', 'SITE-04': '3' };

/** KPI sets differ per plant so a switch is unmistakable on screen. */
const KPI_VALUES: Record<string, [number, number, number, number, number, number]> = {
  'SITE-02': [92, 81, 2, 12, 88, 40],
  'SITE-03': [68, 90, 1, 0, 95, 75],
  'SITE-04': [100, 94, 0, 0, 100, 100],
};
const KPI_ORDER = ['MATERIAL_READINESS', 'OTIF', 'HIGH_RISK_DELIVERIES', 'PREDICTED_DOWNTIME_H', 'ORDER_ON_TIME', 'PASSPORT_COMPLETENESS'] as const;

export function kpisFor(site: string, site01: KpiResponse): KpiResponse {
  if (site === DEFAULT_SITE) return site01;
  const vals = KPI_VALUES[site]!;
  return {
    asOf: site01.asOf,
    items: KPI_ORDER.map((code, i) => {
      const value = vals[i]!;
      const bad = code === 'HIGH_RISK_DELIVERIES' || code === 'PREDICTED_DOWNTIME_H';
      const status = bad ? (value > 5 ? 'critical' : value > 0 ? 'warn' : 'ok') : value >= 95 ? 'ok' : value >= 80 ? 'warn' : 'critical';
      return { code, value, unit: code === 'HIGH_RISK_DELIVERIES' ? 'count' : code === 'PREDICTED_DOWNTIME_H' ? 'h' : '%', trend: 0, status, definitionKey: `kpi.def.${code}` } as KpiResponse['items'][number];
    }),
  };
}

export function mapFor(site: string, site01: MapData): MapData {
  const s = SITES.find((x) => x.code === site)!;
  if (site === DEFAULT_SITE) return { ...site01, site: { code: s.code, name: s.name, lat: s.lat, lon: s.lon } };
  // Fewer, different inbound flows per plant, all routed to that plant.
  const take = site === 'SITE-02' ? ['SUP-03', 'SUP-06'] : site === 'SITE-03' ? ['SUP-01', 'SUP-05'] : ['SUP-07'];
  const suppliers = site01.suppliers.filter((x) => take.includes(x.code));
  return {
    site: { code: s.code, name: s.name, lat: s.lat, lon: s.lon },
    suppliers,
    shipments: suppliers.map((sup, i) => ({
      code: `SHP-2026-${WO_BAND[site]}10${i}`,
      poCode: `PO-2026-${WO_BAND[site]}00${i + 1}`,
      supplierCode: sup.code,
      partCode: site === 'SITE-02' ? 'MCU-X7' : site === 'SITE-03' ? 'HTS-22' : 'GBX-7',
      quantity: 10 + i,
      eta: t0d(6 + i * 3),
      requiredDate: t0d(8 + i * 3),
      status: 'InTransit',
      riskScore: sup.riskScore,
      riskCategory: sup.riskCategory,
      progress: 0.4,
      lat: sup.lat,
      lon: sup.lon,
      route: [[sup.lon, sup.lat], [s.lon, s.lat]] as [number, number][],
    })),
  };
}

export function heatmapFor(site: string, site01: RiskHeatmap): RiskHeatmap {
  if (site === DEFAULT_SITE) return site01;
  const keep = site === 'SITE-02' ? ['Elektronika'] : site === 'SITE-03' ? ['Materiały'] : ['Mechanika'];
  return { ...site01, cells: site01.cells.filter((c) => keep.includes(c.col)) };
}

export function qualityFor(site: string, site01: QualityStatus): QualityStatus {
  if (site === DEFAULT_SITE) return site01;
  const scale: Record<string, QualityStatus> = {
    'SITE-02': { passports: { draft: 3, pendingReview: 1, approved: 0, generated: 1, invalidated: 0 }, documents: { pending: 2, verifying: 1, accepted: 9, rejected: 1, requiresCompletion: 0 }, openNonConformances: 0, lotsBlocked: 0, readyForAcceptance: 1 },
    'SITE-03': { passports: { draft: 2, pendingReview: 0, approved: 0, generated: 1, invalidated: 0 }, documents: { pending: 1, verifying: 0, accepted: 8, rejected: 0, requiresCompletion: 1 }, openNonConformances: 1, lotsBlocked: 1, readyForAcceptance: 1 },
    'SITE-04': { passports: { draft: 1, pendingReview: 0, approved: 1, generated: 1, invalidated: 0 }, documents: { pending: 0, verifying: 0, accepted: 11, rejected: 0, requiresCompletion: 0 }, openNonConformances: 0, lotsBlocked: 0, readyForAcceptance: 2 },
  };
  return scale[site]!;
}

/** A small, feasible plan per plant using that plant's own work-center and order codes. */
export function planFor(site: string, site01: GanttData): GanttData {
  if (site === DEFAULT_SITE) return site01;
  const p = PREFIX[site]!;
  const band = WO_BAND[site]!;
  const wcs = [
    { code: `${p}WC-CUT`, name: 'Cięcie i obróbka', lineCode: `${p}LINE-1` },
    { code: `${p}WC-ELEC`, name: 'Montaż elektroniki', lineCode: `${p}LINE-2` },
    { code: `${p}WC-INT`, name: 'Gniazdo integracji', lineCode: `${p}LINE-2` },
  ];
  const orders = [1, 2, 3].map((n) => ({
    code: `WO-2026-${band}0${n}`,
    productCode: site === 'SITE-02' ? 'P-COM-02' : site === 'SITE-03' ? 'P-MOB-03' : 'P-OBS-01',
    productName: site === 'SITE-02' ? 'Moduł bezpiecznej łączności' : site === 'SITE-03' ? 'Pojazd chronionej mobilności' : 'Bezzałogowa platforma obserwacyjna',
    priority: 5 - n,
    dueDate: t0d(14 + n * 7),
    status: n === 1 ? 'InProgress' : 'Planned',
    materialComplete: n !== 2,
    riskFlag: (n === 2 ? 'warn' : 'none') as 'warn' | 'none',
  }));
  const operations = orders.flatMap((o, i) =>
    wcs.map((wc, j) => ({
      orderCode: o.code,
      code: `${o.code}/${(j + 1) * 10}`,
      sequence: (j + 1) * 10,
      workCenterCode: wc.code,
      start: t0(i * 4 + j * 2, 6),
      end: t0(i * 4 + j * 2 + 1, 14),
      frozen: i === 0 && j === 0,
      status: i === 0 && j === 0 ? 'InProgress' : 'Planned',
      materialWait: false,
    })),
  );
  return {
    horizonStart: site01.horizonStart,
    horizonEnd: site01.horizonEnd,
    workCenters: wcs,
    orders,
    operations,
    dependencies: orders.flatMap((o) => [{ from: `${o.code}/10`, to: `${o.code}/20` }, { from: `${o.code}/20`, to: `${o.code}/30` }]),
    conflicts: [],
  };
}

export function purchaseOrdersFor(site: string, site01: PurchaseOrderSummary[]): PurchaseOrderSummary[] {
  if (site === DEFAULT_SITE) return site01;
  const band = WO_BAND[site]!;
  const rows: Record<string, Array<[string, string, string, number, string]>> = {
    'SITE-02': [['SUP-03', 'Vistula Electronics S.A.', 'InProduction', 62, 'High'], ['SUP-06', 'Rhône Connectique SAS', 'Shipped', 51, 'High'], ['SUP-08', 'Iberia Power Systems S.L.', 'Confirmed', 18, 'Low']],
    'SITE-03': [['SUP-01', 'Nordstal Sp. z o.o.', 'Delivered', 54, 'High'], ['SUP-05', 'Carpathia Composites s.r.o.', 'Confirmed', 21, 'Low']],
    'SITE-04': [['SUP-07', 'Silesia Precision Sp. z o.o.', 'Shipped', 12, 'Low'], ['SUP-02', 'Hydromech Actuators GmbH', 'Confirmed', 20, 'Low']],
  };
  return (rows[site] ?? []).map(([supplierCode, supplierName, status, riskScore, riskCategory], i) => ({
    code: `PO-2026-${band}00${i + 1}`,
    supplierCode,
    supplierName,
    status,
    orderedAt: t0d(-20 - i),
    requiredDate: t0d(10 + i * 4),
    eta: t0d(9 + i * 4),
    lineCount: 1 + (i % 2),
    riskScore,
    riskCategory: riskCategory as PurchaseOrderSummary['riskCategory'],
    progressPercent: [40, 100, 0][i] ?? 30,
    siteCode: site,
  }));
}

/** Each plant offers its own presets; exactly one carries `featured`. */
export function presetsFor(site: string, site01: ScenarioPreset[]): ScenarioPreset[] {
  const featuredKey = SITES.find((s) => s.code === site)?.featuredScenarioKey;
  if (site === DEFAULT_SITE) return site01.map((p) => ({ ...p, siteCode: site, featured: p.key === featuredKey }));
  const p = PREFIX[site]!;
  const band = WO_BAND[site]!;
  const all: ScenarioPreset[] = [
    { key: 'DELAY_MCUX7_14D', titleKey: 'MCU_X7_DELAY', changes: [{ type: 'DELAY_INBOUND', poLineId: `line-${band}001-1`, days: 14, poCode: `PO-2026-${band}001`, partCode: 'MCU-X7' }] },
    { key: 'BLOCK_LOT_HTS22', titleKey: 'HTS22_BLOCK', changes: [{ type: 'BLOCK_LOT', lotNumber: `HTS-22-${band}608` }] },
    { key: 'CAPACITY_INT_50', titleKey: 'WC_INT_CAPACITY', changes: [{ type: 'CAPACITY', workCenterCode: `${p}WC-INT`, factor: 0.5 }] },
    { key: 'PRIORITY_WO014', titleKey: 'WO014_PRIORITY', changes: [{ type: 'PRIORITY', orderCode: `WO-2026-${band}01`, priority: 5 }] },
  ];
  return all.map((x) => ({ ...x, siteCode: site, featured: x.key === featuredKey }));
}
