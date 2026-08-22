import { writeFileSync } from 'node:fs';
const T0 = new Date(Date.UTC(2026, 8, 7)); // 2026-09-07 Monday
const day = n => { const d = new Date(T0); d.setUTCDate(d.getUTCDate() + n); return d.toISOString().slice(0, 10); };
const at = (n, hhmm) => `${day(n)}T${hhmm}:00`;

const workCenters = [
  { code: 'WC-CUT', lineCode: 'LINE-1', hoursPerDay: 16, capacityFactor: 1.0, calendar: [] },
  { code: 'WC-WELD', lineCode: 'LINE-1', hoursPerDay: 16, capacityFactor: 1.0, calendar: [] },
  { code: 'WC-ELEC', lineCode: 'LINE-2', hoursPerDay: 16, capacityFactor: 1.0, calendar: [] },
  { code: 'WC-INT', lineCode: 'LINE-2', hoursPerDay: 16, capacityFactor: 1.0, calendar: [] },
  { code: 'WC-TEST', lineCode: 'LINE-1', hoursPerDay: 16, capacityFactor: 1.0, calendar: [] },
];

// BOM per unit, split by the first consuming operation of each routing
const BOM = {
  'P-OBS-01': { 10: { 'FRM-3': 1, 'BAT-9': 2, 'CON-5': 6, 'HRN-8': 2, 'ANT-2': 1, 'PCB-11': 2, 'SNS-4': 3, 'FAS-1': 40 },
                30: { 'ACT-40': 2, 'MCU-X7': 1, 'OPT-12': 1, 'GBX-7': 2 } },
  'P-COM-02': { 10: { 'MCU-X7': 1, 'PCB-11': 3, 'ENC-4': 1, 'HRN-8': 1, 'FAS-1': 20, 'DSP-2': 1 },
                20: { 'ANT-2': 2, 'PSU-6': 1, 'CON-5': 8 } },
  'P-MOB-03': { 10: { 'HTS-22': 200, 'SEAL-3': 10, 'FAS-1': 120, 'HRN-8': 3, 'PSU-6': 1, 'CBL-3': 30, 'SNS-4': 4, 'CON-5': 12, 'BAT-9': 1 },
                20: { 'ARM-2': 4 },
                30: { 'ACT-40': 2, 'MCU-X7': 1, 'WHL-1': 4, 'GBX-7': 1 } },
};
const ROUTING = {
  'P-OBS-01': [[10, 'WC-CUT'], [20, 'WC-ELEC'], [30, 'WC-INT'], [40, 'WC-TEST']],
  'P-COM-02': [[10, 'WC-ELEC'], [20, 'WC-INT'], [30, 'WC-TEST']],
  'P-MOB-03': [[10, 'WC-CUT'], [20, 'WC-WELD'], [30, 'WC-INT'], [40, 'WC-TEST']],
};

function order(code, productCode, quantity, priority, release, due, lineCode, frozen, ops) {
  const routing = ROUTING[productCode];
  return {
    code, productCode, priority, quantity, dueDate: day(due), releaseDate: day(release), frozen, lineCode,
    operations: ops.map(([hours, s, e, opFrozen], i) => {
      const [seq, wc] = routing[i];
      const bom = BOM[productCode][seq] || {};
      return {
        code: `${code}/${seq}`, sequence: seq, workCenterCode: wc, durationHours: hours, frozen: !!opFrozen,
        baselineStart: at(...s), baselineEnd: at(...e),
        materialRequirements: Object.entries(bom).map(([partCode, q]) => ({ partCode, quantity: q * quantity })),
      };
    }),
  };
}

const orders = [
  order('WO-2026-012', 'P-COM-02', 10, 3, -2, 9, 'LINE-2', true, [
    [24, [0, '06:00'], [1, '14:00'], true], [16, [2, '06:00'], [2, '22:00'], true], [8, [3, '06:00'], [3, '14:00'], true]]),
  order('WO-2026-013', 'P-MOB-03', 2, 4, 0, 18, 'LINE-1', false, [
    [32, [0, '06:00'], [1, '22:00'], true], [48, [2, '06:00'], [4, '22:00']], [32, [7, '06:00'], [8, '22:00']], [16, [9, '06:00'], [9, '22:00']]]),
  order('WO-2026-014', 'P-OBS-01', 4, 5, 0, 18, 'LINE-2', false, [
    [16, [2, '06:00'], [2, '22:00']], [32, [4, '06:00'], [7, '22:00']], [36, [9, '06:00'], [11, '10:00']], [12, [14, '06:00'], [14, '18:00']]]),
  order('WO-2026-015', 'P-MOB-03', 1, 3, 0, 32, 'LINE-1', false, [
    [16, [3, '06:00'], [3, '22:00']], [24, [8, '06:00'], [9, '14:00']], [16, [21, '06:00'], [21, '22:00']], [8, [22, '06:00'], [22, '14:00']]]),
  order('WO-2026-016', 'P-OBS-01', 2, 2, 7, 39, 'LINE-2', false, [
    [8, [15, '06:00'], [15, '14:00']], [16, [16, '06:00'], [16, '22:00']], [24, [23, '06:00'], [24, '14:00']], [8, [25, '06:00'], [25, '14:00']]]),
  order('WO-2026-017', 'P-COM-02', 8, 3, 14, 46, 'LINE-2', false, [
    [20, [29, '06:00'], [30, '10:00']], [12, [31, '06:00'], [31, '18:00']], [8, [32, '06:00'], [32, '14:00']]]),
  order('WO-2026-018', 'P-MOB-03', 2, 4, 21, 60, 'LINE-1', false, [
    [32, [30, '06:00'], [31, '22:00']], [48, [32, '06:00'], [36, '22:00']], [32, [37, '06:00'], [38, '22:00']], [16, [39, '06:00'], [39, '22:00']]]),
  order('WO-2026-019', 'P-COM-02', 6, 2, 0, 53, 'LINE-2', false, [
    [24, [37, '06:00'], [38, '14:00']], [28, [39, '06:00'], [42, '18:00']], [8, [43, '06:00'], [43, '14:00']]]),
];

const materials = (act40Eta) => [
  { partCode: 'ACT-40', onHand: 4, reserved: 0, inbound: [
    { quantity: 12, eta: day(act40Eta), reference: 'PO-2026-0007/1', riskScore: 44 },
    { quantity: 10, eta: day(20), reference: 'PO-2026-0012/1', riskScore: 22 } ] },
  { partCode: 'MCU-X7', onHand: 26, reserved: 0, inbound: [ { quantity: 12, eta: day(25), reference: 'PO-2026-0009/1', riskScore: 58 } ] },
  { partCode: 'HTS-22', onHand: 900, reserved: 0, inbound: [ { quantity: 600, eta: day(24), reference: 'PO-2026-0013/1', riskScore: 18 } ] },
  { partCode: 'OPT-12', onHand: 6, reserved: 0, inbound: [ { quantity: 4, eta: day(16), reference: 'PO-2026-0010/1', riskScore: 55 } ] },
  { partCode: 'CON-5', onHand: 400, reserved: 0, inbound: [ { quantity: 400, eta: day(10), reference: 'PO-2026-0011/2', riskScore: 52 } ] },
  { partCode: 'SNS-4', onHand: 60, reserved: 0, inbound: [] },
  { partCode: 'BAT-9', onHand: 30, reserved: 0, inbound: [] },
  { partCode: 'PSU-6', onHand: 40, reserved: 0, inbound: [] },
  { partCode: 'FRM-3', onHand: 10, reserved: 0, inbound: [] },
  { partCode: 'ARM-2', onHand: 24, reserved: 0, inbound: [] },
  { partCode: 'HRN-8', onHand: 80, reserved: 0, inbound: [] },
  { partCode: 'CBL-3', onHand: 200, reserved: 0, inbound: [] },
  { partCode: 'GBX-7', onHand: 25, reserved: 0, inbound: [] },
  { partCode: 'WHL-1', onHand: 24, reserved: 0, inbound: [] },
  { partCode: 'SEAL-3', onHand: 60, reserved: 0, inbound: [] },
  { partCode: 'FAS-1', onHand: 2000, reserved: 0, inbound: [] },
  { partCode: 'ANT-2', onHand: 70, reserved: 0, inbound: [] },
  { partCode: 'ENC-4', onHand: 30, reserved: 0, inbound: [] },
  { partCode: 'PCB-11', onHand: 120, reserved: 0, inbound: [] },
  { partCode: 'DSP-2', onHand: 30, reserved: 0, inbound: [] },
];

const request = (scenarioId, act40Eta) => ({
  scenarioId, baselineId: 'BASELINE-2026-W37-v1', horizonStart: day(0), horizonEnd: day(84), timeLimitMs: 2500,
  workCenters, orders, materials: materials(act40Eta),
  weights: { latenessPerDayPerPriority: 10, shortagePerUnit: 5, downtimePerHour: 20, deliveryBreachPerOrder: 100, changePerMovedOperation: 2, changeoverPerSwitch: 8 },
});

const out = process.argv.slice(2);
for (const dir of out) {
  writeFileSync(`${dir}/baseline.json`, JSON.stringify(request('SCN-BASELINE', 8), null, 2) + '\n');
  writeFileSync(`${dir}/act40-delay.json`, JSON.stringify(request('SCN-ACT40-DELAY-10D', 18), null, 2) + '\n');
}
console.log('written', out.join(', '));
