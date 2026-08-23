// Generates plants.json — the SITE-02/03/04 demo plants (SITE-01 lives in the other files and must stay untouched).
// Run: node packages/demo-data/generate-plants.mjs > packages/demo-data/plants.json
// All dates are "T0+n" offsets; T0 is the Monday of the demo week, so n mod 7 in {0..4} is a working day.
import { writeFileSync } from 'node:fs';

const doc = (type, number, status, issuedOn, extra = {}) => ({
  type, status, number, fileName: `${number}.pdf`, issuedOn,
  ...(status === 'Accepted' ? { verifiedBy: 'quality', verifiedAt: `${issuedOn} 09:00` } : {}),
  ...extra,
});

// ---------------------------------------------------------------- SITE-02 Piła — electronics, MCU-X7 delay story
const pila = {
  site: { code: 'SITE-02', name: 'Zakład Piła', country: 'PL', city: 'Piła', lat: 53.15, lon: 16.74,
          timeZone: 'Europe/Warsaw', profileKey: 'site.profile.electronics', featuredScenarioKey: 'DELAY_MCUX7_14D', sequence: 2 },
  lines: [{ code: 'PIL-LINE-1', name: 'Linia elektroniki' }, { code: 'PIL-LINE-2', name: 'Linia integracji' }],
  workCenters: [
    { code: 'PIL-WC-ELEC', namePl: 'Montaż elektroniki', nameEn: 'Electronics assembly', line: 'PIL-LINE-1', hoursPerDay: 16, sequence: 1 },
    { code: 'PIL-WC-INT',  namePl: 'Gniazdo integracji', nameEn: 'Integration cell',      line: 'PIL-LINE-2', hoursPerDay: 16, sequence: 2 },
    { code: 'PIL-WC-TEST', namePl: 'Testy końcowe',      nameEn: 'Final test',            line: 'PIL-LINE-2', hoursPerDay: 16, sequence: 3 },
  ],
  purchaseOrders: [
    { code: 'PO-2026-1001', supplier: 'SUP-03', status: 'Closed', orderedOn: 'T0-60', lines: [
      { lineNo: 1, part: 'MCU-X7', qty: 12, requiredDate: 'T0-30', eta: 'T0-31', status: 'Delivered', progress: 100, deliveredOn: 'T0-31', lotNumber: 'MCU-X7-1101',
        documents: [doc('MATERIAL_CERT', 'MC-VIS-2026-1101', 'Accepted', 'T0-34')] },
      { lineNo: 2, part: 'PCB-11', qty: 120, requiredDate: 'T0-30', eta: 'T0-31', status: 'Delivered', progress: 100, deliveredOn: 'T0-31', lotNumber: 'PCB-11-1102',
        documents: [doc('MATERIAL_CERT', 'MC-VIS-2026-1102', 'Accepted', 'T0-34')] },
      { lineNo: 3, part: 'ANT-2', qty: 80, requiredDate: 'T0-30', eta: 'T0-29', status: 'Delivered', progress: 100, deliveredOn: 'T0-29', lotNumber: 'ANT-2-1103',
        documents: [doc('MATERIAL_CERT', 'MC-VIS-2026-1103', 'Accepted', 'T0-33')] } ] },
    { code: 'PO-2026-1002', supplier: 'SUP-03', status: 'Closed', orderedOn: 'T0-55', lines: [
      { lineNo: 1, part: 'ENC-4', qty: 40, requiredDate: 'T0-25', eta: 'T0-26', status: 'Delivered', progress: 100, deliveredOn: 'T0-26', lotNumber: 'ENC-4-1104',
        documents: [doc('MATERIAL_CERT', 'MC-VIS-2026-1104', 'Accepted', 'T0-29')] } ] },
    // the scenario target: on time today, Critical once pushed by 14 days
    { code: 'PO-2026-1003', supplier: 'SUP-03', status: 'Open', orderedOn: 'T0-25', notes: 'Dostawa kluczowa dla WO-2026-102.', lines: [
      { lineNo: 1, part: 'MCU-X7', qty: 10, requiredDate: 'T0+13', eta: 'T0+12', status: 'Shipped', progress: 100, shipment: 'SHP-2026-1031',
        documents: [doc('MATERIAL_CERT', 'MC-VIS-2026-1105', 'Pending', 'T0+8')] } ] },
    { code: 'PO-2026-1004', supplier: 'SUP-06', status: 'Open', orderedOn: 'T0-20', lines: [
      { lineNo: 1, part: 'CON-5', qty: 300, requiredDate: 'T0+9', eta: 'T0+14', status: 'InProduction', progress: 55, supplierConfirmed: false,
        documents: [doc('DECLARATION_OF_CONFORMITY', 'DOC-RHO-2026-1106', 'Missing', 'T0+2')] },
      { lineNo: 2, part: 'HRN-8', qty: 30, requiredDate: 'T0+9', eta: 'T0+10', status: 'InProduction', progress: 70, documents: [] } ] },
    { code: 'PO-2026-1005', supplier: 'SUP-03', status: 'Open', orderedOn: 'T0-10', lines: [
      { lineNo: 1, part: 'MCU-X7', qty: 6, requiredDate: 'T0+25', eta: 'T0+24', status: 'Confirmed', progress: 20, documents: [] } ] },
    { code: 'PO-2026-1006', supplier: 'SUP-04', status: 'Open', orderedOn: 'T0-18', lines: [
      { lineNo: 1, part: 'DSP-2', qty: 20, requiredDate: 'T0+6', eta: 'T0+13', status: 'InProduction', progress: 40,
        documents: [doc('INSPECTION_REPORT', 'IR-BAL-2026-1107', 'Rejected', 'T0+1', { verifiedBy: 'quality', verifiedAt: 'T0+3 11:00', comment: 'Brak wyników testu klimatycznego.' })] } ] },
    { code: 'PO-2026-1007', supplier: 'SUP-08', status: 'Open', orderedOn: 'T0-12', lines: [
      { lineNo: 1, part: 'PSU-6', qty: 40, requiredDate: 'T0+20', eta: 'T0+19', status: 'Confirmed', progress: 15, documents: [] } ] },
    { code: 'PO-2026-1008', supplier: 'SUP-06', status: 'Closed', orderedOn: 'T0-50', lines: [
      { lineNo: 1, part: 'FAS-1', qty: 800, requiredDate: 'T0-28', eta: 'T0-29', status: 'Delivered', progress: 100, deliveredOn: 'T0-29', lotNumber: 'FAS-1-1105', documents: [doc('MATERIAL_CERT', 'MC-RHO-2026-1108', 'Accepted', 'T0-32')] },
      { lineNo: 2, part: 'CON-5', qty: 300, requiredDate: 'T0-28', eta: 'T0-27', status: 'Delivered', progress: 100, deliveredOn: 'T0-27', lotNumber: 'CON-5-1106', documents: [doc('MATERIAL_CERT', 'MC-RHO-2026-1109', 'Accepted', 'T0-31')] },
      { lineNo: 3, part: 'HRN-8', qty: 40, requiredDate: 'T0-28', eta: 'T0-27', status: 'Delivered', progress: 100, deliveredOn: 'T0-27', lotNumber: 'HRN-8-1107', documents: [doc('MATERIAL_CERT', 'MC-RHO-2026-1110', 'Accepted', 'T0-31')] },
      { lineNo: 4, part: 'DSP-2', qty: 40, requiredDate: 'T0-28', eta: 'T0-24', status: 'Delivered', progress: 100, deliveredOn: 'T0-24', lotNumber: 'DSP-2-1108', documents: [doc('MATERIAL_CERT', 'MC-BAL-2026-1111', 'Accepted', 'T0-30')] },
      { lineNo: 5, part: 'PSU-6', qty: 40, requiredDate: 'T0-28', eta: 'T0-27', status: 'Delivered', progress: 100, deliveredOn: 'T0-27', lotNumber: 'PSU-6-1109', documents: [doc('MATERIAL_CERT', 'MC-IBE-2026-1112', 'Accepted', 'T0-31')] } ] },
    { code: 'PO-2026-1009', supplier: 'SUP-03', status: 'Open', orderedOn: 'T0-5', lines: [
      { lineNo: 1, part: 'MCU-X7', qty: 12, requiredDate: 'T0+32', eta: 'T0+31', status: 'Confirmed', progress: 10, documents: [] } ] },
  ],
  shipments: [
    { code: 'SHP-2026-1031', po: 'PO-2026-1003', status: 'InTransit', carrier: 'Vistula Logistics', vehicle: 'PL-VIS-1031', plannedDeparture: 'T0+9 08:00', actualDeparture: 'T0+9 09:20', eta: 'T0+12', progress: 0.35,
      events: [{ type: 'Departed', at: 'T0+9 09:20', note: 'Wyjazd z zakładu dostawcy', location: 'Kraków' }] },
  ],
  lots: [
    { lotNumber: 'MCU-X7-1101', part: 'MCU-X7', supplier: 'SUP-03', poLine: 'PO-2026-1001/1', quantity: 12, remaining: 6, receivedOn: 'T0-31', producedOn: 'T0-40',
      documents: [doc('MATERIAL_CERT', 'MC-VIS-2026-1101L', 'Accepted', 'T0-34')], inspections: [{ code: 'QI-2026-1101', result: 'Passed', by: 'quality', at: 'T0-30 09:00', notes: 'Test funkcjonalny zgodny z wymaganiami.' }] },
    { lotNumber: 'PCB-11-1102', part: 'PCB-11', supplier: 'SUP-03', poLine: 'PO-2026-1001/2', quantity: 120, remaining: 120, receivedOn: 'T0-31',
      documents: [doc('MATERIAL_CERT', 'MC-VIS-2026-1102L', 'Accepted', 'T0-34')], inspections: [{ code: 'QI-2026-1102', result: 'Passed', by: 'quality', at: 'T0-30 10:00' }] },
    { lotNumber: 'ANT-2-1103', part: 'ANT-2', supplier: 'SUP-03', poLine: 'PO-2026-1001/3', quantity: 80, remaining: 80, receivedOn: 'T0-29',
      documents: [doc('MATERIAL_CERT', 'MC-VIS-2026-1103L', 'Accepted', 'T0-33')], inspections: [{ code: 'QI-2026-1103', result: 'Passed', by: 'quality', at: 'T0-28 09:00' }] },
    { lotNumber: 'ENC-4-1104', part: 'ENC-4', supplier: 'SUP-03', poLine: 'PO-2026-1002/1', quantity: 40, remaining: 40, receivedOn: 'T0-26',
      documents: [doc('MATERIAL_CERT', 'MC-VIS-2026-1104L', 'Accepted', 'T0-29')], inspections: [{ code: 'QI-2026-1104', result: 'Passed', by: 'quality', at: 'T0-25 09:00' }] },
    { lotNumber: 'FAS-1-1105', part: 'FAS-1', supplier: 'SUP-06', poLine: 'PO-2026-1008/1', quantity: 800, remaining: 800, receivedOn: 'T0-29', documents: [], inspections: [] },
    { lotNumber: 'CON-5-1106', part: 'CON-5', supplier: 'SUP-06', poLine: 'PO-2026-1008/2', quantity: 300, remaining: 300, receivedOn: 'T0-27',
      documents: [doc('MATERIAL_CERT', 'MC-RHO-2026-1109L', 'Accepted', 'T0-31')], inspections: [] },
    { lotNumber: 'HRN-8-1107', part: 'HRN-8', supplier: 'SUP-06', poLine: 'PO-2026-1008/3', quantity: 40, remaining: 40, receivedOn: 'T0-27', documents: [], inspections: [] },
    { lotNumber: 'DSP-2-1108', part: 'DSP-2', supplier: 'SUP-04', poLine: 'PO-2026-1008/4', quantity: 40, remaining: 40, receivedOn: 'T0-24',
      documents: [doc('MATERIAL_CERT', 'MC-BAL-2026-1111L', 'RequiresCompletion', 'T0-30', { comment: 'Brak numeru rewizji na certyfikacie.' })], inspections: [] },
    { lotNumber: 'PSU-6-1109', part: 'PSU-6', supplier: 'SUP-08', poLine: 'PO-2026-1008/5', quantity: 40, remaining: 40, receivedOn: 'T0-27',
      documents: [doc('MATERIAL_CERT', 'MC-IBE-2026-1112L', 'Accepted', 'T0-31')], inspections: [{ code: 'QI-2026-1109', result: 'Passed', by: 'quality', at: 'T0-26 09:00' }] },
    { lotNumber: 'MCU-X7-1110', part: 'MCU-X7', supplier: 'SUP-03', poLine: 'PO-2026-1001/1', quantity: 8, remaining: 0, receivedOn: 'T0-31', producedOn: 'T0-42',
      documents: [doc('MATERIAL_CERT', 'MC-VIS-2026-1110L', 'Accepted', 'T0-34')], inspections: [{ code: 'QI-2026-1110', result: 'Passed', by: 'quality', at: 'T0-30 11:00' }] },
  ],
  orders: [
    { code: 'WO-2026-106', product: 'P-COM-02', line: 'PIL-LINE-1', quantity: 4, priority: 3, releaseDate: 'T0-35', dueDate: 'T0-22', status: 'Completed', customerReference: 'KONTRAKT-DEMO-2025/21',
      operations: [
        { seq: 10, wc: 'PIL-WC-ELEC', hours: 16, start: 'T0-28 06:00', end: 'T0-28 22:00', status: 'Completed', materials: [{ part: 'MCU-X7', qty: 4 }, { part: 'ENC-4', qty: 4 }, { part: 'PCB-11', qty: 12 }] },
        { seq: 20, wc: 'PIL-WC-INT',  hours: 8,  start: 'T0-27 06:00', end: 'T0-27 14:00', status: 'Completed', materials: [{ part: 'ANT-2', qty: 8 }, { part: 'PSU-6', qty: 4 }] },
        { seq: 30, wc: 'PIL-WC-TEST', hours: 8,  start: 'T0-26 06:00', end: 'T0-26 14:00', status: 'Completed', materials: [] }],
      serials: [{ serial: 'SCM-2026-0201-P', status: 'Completed', completedAt: 'T0-26 14:00' }, { serial: 'SCM-2026-0202-P', status: 'Completed', completedAt: 'T0-26 14:00' }],
      consumptions: [
        { lot: 'MCU-X7-1110', qty: 1, serial: 'SCM-2026-0201-P', opSeq: 10, at: 'T0-28 07:00' },
        { lot: 'ENC-4-1104',  qty: 1, serial: 'SCM-2026-0201-P', opSeq: 10, at: 'T0-28 07:10' },
        { lot: 'ANT-2-1103',  qty: 2, serial: 'SCM-2026-0201-P', opSeq: 20, at: 'T0-27 07:00' },
        { lot: 'MCU-X7-1110', qty: 1, serial: 'SCM-2026-0202-P', opSeq: 10, at: 'T0-28 08:00' },
        { lot: 'ENC-4-1104',  qty: 1, serial: 'SCM-2026-0202-P', opSeq: 10, at: 'T0-28 08:10' },
        { lot: 'ANT-2-1103',  qty: 2, serial: 'SCM-2026-0202-P', opSeq: 20, at: 'T0-27 08:00' }] },
    { code: 'WO-2026-101', product: 'P-COM-02', line: 'PIL-LINE-1', quantity: 6, priority: 4, releaseDate: 'T0-4', dueDate: 'T0+11', status: 'InProgress', customerReference: 'KONTRAKT-DEMO-2026/21',
      operations: [
        { seq: 10, wc: 'PIL-WC-ELEC', hours: 24, start: 'T0 06:00',    end: 'T0+1 14:00',  status: 'InProgress', frozen: true, materials: [{ part: 'MCU-X7', qty: 6 }, { part: 'ENC-4', qty: 6 }, { part: 'PCB-11', qty: 18 }] },
        { seq: 20, wc: 'PIL-WC-INT',  hours: 16, start: 'T0+2 06:00',  end: 'T0+2 22:00',  status: 'Planned', materials: [{ part: 'ANT-2', qty: 12 }, { part: 'PSU-6', qty: 6 }] },
        { seq: 30, wc: 'PIL-WC-TEST', hours: 8,  start: 'T0+3 06:00',  end: 'T0+3 14:00',  status: 'Planned', materials: [] }],
      reservations: [{ part: 'MCU-X7', qty: 6, lot: 'MCU-X7-1101' }, { part: 'ENC-4', qty: 6, lot: 'ENC-4-1104' }, { part: 'ANT-2', qty: 12, lot: 'ANT-2-1103' }],
      serials: [] },
    // the order the featured scenario hurts: it lives entirely off PO-2026-1003
    { code: 'WO-2026-102', product: 'P-COM-02', line: 'PIL-LINE-1', quantity: 8, priority: 5, releaseDate: 'T0+7', dueDate: 'T0+25', status: 'Released', customerReference: 'KONTRAKT-DEMO-2026/22-PRIORYTET',
      operations: [
        { seq: 10, wc: 'PIL-WC-ELEC', hours: 32, start: 'T0+14 06:00', end: 'T0+15 22:00', status: 'Planned', materials: [{ part: 'MCU-X7', qty: 8 }, { part: 'ENC-4', qty: 8 }, { part: 'PCB-11', qty: 24 }] },
        { seq: 20, wc: 'PIL-WC-INT',  hours: 16, start: 'T0+16 06:00', end: 'T0+16 22:00', status: 'Planned', materials: [{ part: 'ANT-2', qty: 16 }, { part: 'PSU-6', qty: 8 }] },
        { seq: 30, wc: 'PIL-WC-TEST', hours: 8,  start: 'T0+17 06:00', end: 'T0+17 14:00', status: 'Planned', materials: [] }],
      reservations: [], serials: [] },
    { code: 'WO-2026-103', product: 'P-COM-02', line: 'PIL-LINE-1', quantity: 4, priority: 3, releaseDate: 'T0+14', dueDate: 'T0+39', status: 'Planned',
      operations: [
        { seq: 10, wc: 'PIL-WC-ELEC', hours: 16, start: 'T0+28 06:00', end: 'T0+28 22:00', status: 'Planned', materials: [{ part: 'MCU-X7', qty: 4 }, { part: 'ENC-4', qty: 4 }, { part: 'PCB-11', qty: 12 }] },
        { seq: 20, wc: 'PIL-WC-INT',  hours: 8,  start: 'T0+29 06:00', end: 'T0+29 14:00', status: 'Planned', materials: [{ part: 'ANT-2', qty: 8 }, { part: 'PSU-6', qty: 4 }] },
        { seq: 30, wc: 'PIL-WC-TEST', hours: 8,  start: 'T0+30 06:00', end: 'T0+30 14:00', status: 'Planned', materials: [] }],
      reservations: [], serials: [] },
    { code: 'WO-2026-104', product: 'P-COM-02', line: 'PIL-LINE-1', quantity: 10, priority: 3, releaseDate: 'T0+21', dueDate: 'T0+53', status: 'Planned',
      operations: [
        { seq: 10, wc: 'PIL-WC-ELEC', hours: 40, start: 'T0+35 06:00', end: 'T0+37 14:00', status: 'Planned', materials: [{ part: 'MCU-X7', qty: 10 }, { part: 'ENC-4', qty: 10 }, { part: 'PCB-11', qty: 30 }] },
        { seq: 20, wc: 'PIL-WC-INT',  hours: 24, start: 'T0+38 06:00', end: 'T0+39 14:00', status: 'Planned', materials: [{ part: 'ANT-2', qty: 20 }, { part: 'PSU-6', qty: 10 }] },
        { seq: 30, wc: 'PIL-WC-TEST', hours: 8,  start: 'T0+42 06:00', end: 'T0+42 14:00', status: 'Planned', materials: [] }],
      reservations: [], serials: [] },
    { code: 'WO-2026-105', product: 'P-COM-02', line: 'PIL-LINE-1', quantity: 5, priority: 2, releaseDate: 'T0+28', dueDate: 'T0+67', status: 'Planned',
      operations: [
        { seq: 10, wc: 'PIL-WC-ELEC', hours: 20, start: 'T0+43 06:00', end: 'T0+44 10:00', status: 'Planned', materials: [{ part: 'MCU-X7', qty: 5 }, { part: 'ENC-4', qty: 5 }, { part: 'PCB-11', qty: 15 }] },
        { seq: 20, wc: 'PIL-WC-INT',  hours: 12, start: 'T0+45 06:00', end: 'T0+45 18:00', status: 'Planned', materials: [{ part: 'ANT-2', qty: 10 }, { part: 'PSU-6', qty: 5 }] },
        { seq: 30, wc: 'PIL-WC-TEST', hours: 8,  start: 'T0+46 06:00', end: 'T0+46 14:00', status: 'Planned', materials: [] }],
      reservations: [], serials: [] },
  ],
  serialInspections: [{ code: 'QI-2026-1201', serial: 'SCM-2026-0201-P', result: 'Passed', by: 'quality', at: 'T0-26 15:00', notes: 'Test końcowy modułu łączności zaliczony.' }],
  passports: [
    { serial: 'SCM-2026-0201-P', status: 'Generated', approvedBy: 'quality', approvedAt: 'T0-26 16:00' },
    { serial: 'SCM-2026-0202-P', status: 'Draft' },
  ],
};
writeFileSync(new URL('./plants.part1.json', import.meta.url), JSON.stringify(pila, null, 1));
console.error('SITE-02 written');
import { writeFileSync, readFileSync } from 'node:fs';
const doc = (type, number, status, issuedOn, extra = {}) => ({
  type, status, number, fileName: `${number}.pdf`, issuedOn,
  ...(status === 'Accepted' ? { verifiedBy: 'quality', verifiedAt: `${issuedOn} 09:00` } : {}), ...extra,
});

// ------------------------------------------- SITE-03 Zamość — hulls & armour, lot-block story on HTS-22-3110
const zamosc = {
  site: { code: 'SITE-03', name: 'Zakład Zamość', country: 'PL', city: 'Zamość', lat: 50.72, lon: 23.25,
          timeZone: 'Europe/Warsaw', profileKey: 'site.profile.structures', featuredScenarioKey: 'BLOCK_LOT_HTS22', sequence: 3 },
  lines: [{ code: 'ZAM-LINE-1', name: 'Linia kadłubów' }, { code: 'ZAM-LINE-2', name: 'Linia montażu' }],
  workCenters: [
    { code: 'ZAM-WC-CUT',  namePl: 'Cięcie i obróbka', nameEn: 'Cutting & machining', line: 'ZAM-LINE-1', hoursPerDay: 16, sequence: 1 },
    { code: 'ZAM-WC-WELD', namePl: 'Spawanie',         nameEn: 'Welding',             line: 'ZAM-LINE-1', hoursPerDay: 16, sequence: 2 },
    { code: 'ZAM-WC-INT',  namePl: 'Gniazdo integracji', nameEn: 'Integration cell',  line: 'ZAM-LINE-2', hoursPerDay: 16, sequence: 3 },
  ],
  purchaseOrders: [
    { code: 'PO-2026-2001', supplier: 'SUP-01', status: 'Closed', orderedOn: 'T0-70', lines: [
      { lineNo: 1, part: 'HTS-22', qty: 900, requiredDate: 'T0-40', eta: 'T0-41', status: 'Delivered', progress: 100, deliveredOn: 'T0-41', lotNumber: 'HTS-22-3110', heatNumber: 'H-3110',
        documents: [doc('MATERIAL_CERT', 'MC-NOR-2026-3110', 'Accepted', 'T0-45')] } ] },
    { code: 'PO-2026-2002', supplier: 'SUP-01', status: 'Closed', orderedOn: 'T0-55', lines: [
      { lineNo: 1, part: 'HTS-22', qty: 800, requiredDate: 'T0-25', eta: 'T0-24', status: 'Delivered', progress: 100, deliveredOn: 'T0-24', lotNumber: 'HTS-22-3111', heatNumber: 'H-3111',
        documents: [doc('MATERIAL_CERT', 'MC-NOR-2026-3111', 'Accepted', 'T0-28')] } ] },
    { code: 'PO-2026-2003', supplier: 'SUP-05', status: 'Closed', orderedOn: 'T0-60', lines: [
      { lineNo: 1, part: 'ARM-2', qty: 30, requiredDate: 'T0-30', eta: 'T0-31', status: 'Delivered', progress: 100, deliveredOn: 'T0-31', lotNumber: 'ARM-2-3113',
        documents: [doc('MATERIAL_CERT', 'MC-CAR-2026-3113', 'Accepted', 'T0-35')] } ] },
    { code: 'PO-2026-2004', supplier: 'SUP-07', status: 'Closed', orderedOn: 'T0-58', lines: [
      { lineNo: 1, part: 'WHL-1', qty: 30, requiredDate: 'T0-28', eta: 'T0-29', status: 'Delivered', progress: 100, deliveredOn: 'T0-29', lotNumber: 'WHL-1-3116', documents: [doc('MATERIAL_CERT', 'MC-SIL-2026-3116', 'Accepted', 'T0-33')] },
      { lineNo: 2, part: 'GBX-7', qty: 10, requiredDate: 'T0-28', eta: 'T0-29', status: 'Delivered', progress: 100, deliveredOn: 'T0-29', lotNumber: 'GBX-7-3117', documents: [doc('MATERIAL_CERT', 'MC-SIL-2026-3117', 'Accepted', 'T0-33')] },
      { lineNo: 3, part: 'FAS-1', qty: 900, requiredDate: 'T0-28', eta: 'T0-27', status: 'Delivered', progress: 100, deliveredOn: 'T0-27', lotNumber: 'FAS-1-3112', documents: [] } ] },
    { code: 'PO-2026-2005', supplier: 'SUP-02', status: 'Closed', orderedOn: 'T0-50', lines: [
      { lineNo: 1, part: 'ACT-40', qty: 16, requiredDate: 'T0-22', eta: 'T0-20', status: 'Delivered', progress: 100, deliveredOn: 'T0-20', lotNumber: 'ACT-40-3114',
        documents: [doc('MATERIAL_CERT', 'MC-HYD-2026-3114', 'Accepted', 'T0-26')] } ] },
    { code: 'PO-2026-2006', supplier: 'SUP-03', status: 'Closed', orderedOn: 'T0-48', lines: [
      { lineNo: 1, part: 'MCU-X7', qty: 10, requiredDate: 'T0-20', eta: 'T0-21', status: 'Delivered', progress: 100, deliveredOn: 'T0-21', lotNumber: 'MCU-X7-3115',
        documents: [doc('MATERIAL_CERT', 'MC-VIS-2026-3115', 'Accepted', 'T0-25')] } ] },
    { code: 'PO-2026-2007', supplier: 'SUP-07', status: 'Open', orderedOn: 'T0-15', notes: 'Alternatywne źródło stali HTS-22.', lines: [
      { lineNo: 1, part: 'HTS-22', qty: 600, requiredDate: 'T0+20', eta: 'T0+26', status: 'InProduction', progress: 35, supplierConfirmed: false,
        documents: [doc('MATERIAL_CERT', 'MC-SIL-2026-3130', 'Missing', 'T0+10')] } ] },
    { code: 'PO-2026-2008', supplier: 'SUP-05', status: 'Open', orderedOn: 'T0-8', lines: [
      { lineNo: 1, part: 'ARM-2', qty: 20, requiredDate: 'T0+30', eta: 'T0+33', status: 'Confirmed', progress: 10,
        documents: [doc('MATERIAL_CERT', 'MC-CAR-2026-3131', 'Pending', 'T0+22')] } ] },
  ],
  shipments: [
    { code: 'SHP-2026-2041', po: 'PO-2026-2007', status: 'Advised', carrier: 'Silesia Transport', vehicle: 'PL-SIL-2041', plannedDeparture: 'T0+22 07:00', eta: 'T0+26', progress: 0,
      events: [{ type: 'Advised', at: 'T0+18 10:00', note: 'Awizacja dostawy stali', location: 'Gliwice' }] },
  ],
  lots: [
    // the plant's own steel lot: consumed by a finished serial and reserved by an open order
    { lotNumber: 'HTS-22-3110', heatNumber: 'H-3110', part: 'HTS-22', supplier: 'SUP-01', poLine: 'PO-2026-2001/1', quantity: 900, remaining: 500, unit: 'kg', receivedOn: 'T0-41', producedOn: 'T0-55',
      documents: [doc('MATERIAL_CERT', 'MC-NOR-2026-3110L', 'Accepted', 'T0-45')], inspections: [{ code: 'QI-2026-3110', result: 'Passed', by: 'quality', at: 'T0-40 09:00', notes: 'Skład chemiczny i wytrzymałość zgodne z certyfikatem.' }] },
    { lotNumber: 'HTS-22-3111', heatNumber: 'H-3111', part: 'HTS-22', supplier: 'SUP-01', poLine: 'PO-2026-2002/1', quantity: 800, remaining: 800, unit: 'kg', receivedOn: 'T0-24', producedOn: 'T0-38',
      documents: [doc('MATERIAL_CERT', 'MC-NOR-2026-3111L', 'Accepted', 'T0-28')], inspections: [{ code: 'QI-2026-3111', result: 'Passed', by: 'quality', at: 'T0-23 09:00' }] },
    { lotNumber: 'FAS-1-3112', part: 'FAS-1', supplier: 'SUP-07', poLine: 'PO-2026-2004/3', quantity: 900, remaining: 900, receivedOn: 'T0-27', documents: [], inspections: [] },
    { lotNumber: 'ARM-2-3113', part: 'ARM-2', supplier: 'SUP-05', poLine: 'PO-2026-2003/1', quantity: 30, remaining: 30, receivedOn: 'T0-31',
      documents: [doc('MATERIAL_CERT', 'MC-CAR-2026-3113L', 'Accepted', 'T0-35')], inspections: [{ code: 'QI-2026-3113', result: 'Passed', by: 'quality', at: 'T0-30 09:00' }] },
    { lotNumber: 'ACT-40-3114', part: 'ACT-40', supplier: 'SUP-02', poLine: 'PO-2026-2005/1', quantity: 16, remaining: 16, receivedOn: 'T0-20',
      documents: [doc('MATERIAL_CERT', 'MC-HYD-2026-3114L', 'Accepted', 'T0-26')], inspections: [{ code: 'QI-2026-3114', result: 'Passed', by: 'quality', at: 'T0-19 09:00' }] },
    { lotNumber: 'MCU-X7-3115', part: 'MCU-X7', supplier: 'SUP-03', poLine: 'PO-2026-2006/1', quantity: 10, remaining: 10, receivedOn: 'T0-21',
      documents: [doc('MATERIAL_CERT', 'MC-VIS-2026-3115L', 'Accepted', 'T0-25')], inspections: [{ code: 'QI-2026-3115', result: 'Passed', by: 'quality', at: 'T0-20 09:00' }] },
    { lotNumber: 'WHL-1-3116', part: 'WHL-1', supplier: 'SUP-07', poLine: 'PO-2026-2004/1', quantity: 30, remaining: 30, receivedOn: 'T0-29',
      documents: [doc('MATERIAL_CERT', 'MC-SIL-2026-3116L', 'Accepted', 'T0-33')], inspections: [] },
    { lotNumber: 'GBX-7-3117', part: 'GBX-7', supplier: 'SUP-07', poLine: 'PO-2026-2004/2', quantity: 10, remaining: 10, receivedOn: 'T0-29',
      documents: [doc('MATERIAL_CERT', 'MC-SIL-2026-3117L', 'Accepted', 'T0-33')], inspections: [{ code: 'QI-2026-3117', result: 'Passed', by: 'quality', at: 'T0-28 09:00' }] },
    { lotNumber: 'SEAL-3-3118', part: 'SEAL-3', supplier: 'SUP-07', poLine: 'PO-2026-2004/1', quantity: 120, remaining: 120, receivedOn: 'T0-29',
      documents: [doc('DECLARATION_OF_CONFORMITY', 'DOC-SIL-2026-3118', 'RequiresCompletion', 'T0-33', { comment: 'Brak podpisu osoby upoważnionej.' })], inspections: [] },
  ],
  orders: [
    { code: 'WO-2026-201', product: 'P-MOB-03', line: 'ZAM-LINE-1', quantity: 2, priority: 4, releaseDate: 'T0-35', dueDate: 'T0-18', status: 'Completed', customerReference: 'KONTRAKT-DEMO-2025/31',
      operations: [
        { seq: 10, wc: 'ZAM-WC-CUT',  hours: 32, start: 'T0-28 06:00', end: 'T0-27 22:00', status: 'Completed', materials: [{ part: 'HTS-22', qty: 400 }, { part: 'FAS-1', qty: 240 }] },
        { seq: 20, wc: 'ZAM-WC-WELD', hours: 48, start: 'T0-26 06:00', end: 'T0-24 22:00', status: 'Completed', materials: [{ part: 'ARM-2', qty: 8 }] },
        { seq: 30, wc: 'ZAM-WC-INT',  hours: 32, start: 'T0-21 06:00', end: 'T0-20 22:00', status: 'Completed', materials: [{ part: 'ACT-40', qty: 4 }, { part: 'MCU-X7', qty: 2 }, { part: 'WHL-1', qty: 8 }, { part: 'GBX-7', qty: 2 }] }],
      serials: [{ serial: 'PMV-2026-0201-Z', status: 'Completed', completedAt: 'T0-20 22:00' }, { serial: 'PMV-2026-0202-Z', status: 'Completed', completedAt: 'T0-20 22:00' }],
      consumptions: [
        { lot: 'HTS-22-3110', qty: 200, serial: 'PMV-2026-0201-Z', opSeq: 10, at: 'T0-28 08:00' },
        { lot: 'HTS-22-3110', qty: 200, serial: 'PMV-2026-0202-Z', opSeq: 10, at: 'T0-28 09:00' },
        { lot: 'ARM-2-3113',  qty: 4,   serial: 'PMV-2026-0201-Z', opSeq: 20, at: 'T0-26 07:00' },
        { lot: 'ARM-2-3113',  qty: 4,   serial: 'PMV-2026-0202-Z', opSeq: 20, at: 'T0-26 08:00' },
        { lot: 'ACT-40-3114', qty: 2,   serial: 'PMV-2026-0201-Z', opSeq: 30, at: 'T0-21 07:00' },
        { lot: 'ACT-40-3114', qty: 2,   serial: 'PMV-2026-0202-Z', opSeq: 30, at: 'T0-21 08:00' },
        { lot: 'MCU-X7-3115', qty: 1,   serial: 'PMV-2026-0201-Z', opSeq: 30, at: 'T0-21 07:30' },
        { lot: 'MCU-X7-3115', qty: 1,   serial: 'PMV-2026-0202-Z', opSeq: 30, at: 'T0-21 08:30' },
        { lot: 'GBX-7-3117',  qty: 1,   serial: 'PMV-2026-0201-Z', opSeq: 30, at: 'T0-21 09:00' },
        { lot: 'GBX-7-3117',  qty: 1,   serial: 'PMV-2026-0202-Z', opSeq: 30, at: 'T0-21 10:00' }] },
    { code: 'WO-2026-202', product: 'P-MOB-03', line: 'ZAM-LINE-1', quantity: 2, priority: 4, releaseDate: 'T0-2', dueDate: 'T0+18', status: 'Released', customerReference: 'KONTRAKT-DEMO-2026/31',
      operations: [
        { seq: 10, wc: 'ZAM-WC-CUT',  hours: 32, start: 'T0 06:00',   end: 'T0+1 22:00', status: 'InProgress', frozen: true, materials: [{ part: 'HTS-22', qty: 400 }, { part: 'FAS-1', qty: 240 }] },
        { seq: 20, wc: 'ZAM-WC-WELD', hours: 48, start: 'T0+2 06:00', end: 'T0+4 22:00', status: 'Planned', materials: [{ part: 'ARM-2', qty: 8 }] },
        { seq: 30, wc: 'ZAM-WC-INT',  hours: 32, start: 'T0+7 06:00', end: 'T0+8 22:00', status: 'Planned', materials: [{ part: 'ACT-40', qty: 4 }, { part: 'MCU-X7', qty: 2 }, { part: 'WHL-1', qty: 8 }, { part: 'GBX-7', qty: 2 }] }],
      reservations: [{ part: 'HTS-22', qty: 400, lot: 'HTS-22-3110' }, { part: 'ARM-2', qty: 8, lot: 'ARM-2-3113' }, { part: 'ACT-40', qty: 4, lot: 'ACT-40-3114' }],
      serials: [{ serial: 'PMV-2026-0203-Z', status: 'InProduction' }, { serial: 'PMV-2026-0204-Z', status: 'InProduction' }] },
    { code: 'WO-2026-203', product: 'P-MOB-03', line: 'ZAM-LINE-1', quantity: 1, priority: 3, releaseDate: 'T0+3', dueDate: 'T0+32', status: 'Released',
      operations: [
        { seq: 10, wc: 'ZAM-WC-CUT',  hours: 16, start: 'T0+2 06:00', end: 'T0+2 22:00', status: 'Planned', materials: [{ part: 'HTS-22', qty: 200 }, { part: 'FAS-1', qty: 120 }] },
        { seq: 20, wc: 'ZAM-WC-WELD', hours: 24, start: 'T0+7 06:00', end: 'T0+8 14:00', status: 'Planned', materials: [{ part: 'ARM-2', qty: 4 }] },
        { seq: 30, wc: 'ZAM-WC-INT',  hours: 16, start: 'T0+9 06:00', end: 'T0+9 22:00', status: 'Planned', materials: [{ part: 'ACT-40', qty: 2 }, { part: 'MCU-X7', qty: 1 }, { part: 'WHL-1', qty: 4 }, { part: 'GBX-7', qty: 1 }] }],
      reservations: [{ part: 'HTS-22', qty: 200, lot: 'HTS-22-3111' }], serials: [] },
    { code: 'WO-2026-204', product: 'P-MOB-03', line: 'ZAM-LINE-1', quantity: 2, priority: 3, releaseDate: 'T0+7', dueDate: 'T0+46', status: 'Planned',
      operations: [
        { seq: 10, wc: 'ZAM-WC-CUT',  hours: 32, start: 'T0+14 06:00', end: 'T0+15 22:00', status: 'Planned', materials: [{ part: 'HTS-22', qty: 400 }, { part: 'FAS-1', qty: 240 }] },
        { seq: 20, wc: 'ZAM-WC-WELD', hours: 48, start: 'T0+16 06:00', end: 'T0+18 22:00', status: 'Planned', materials: [{ part: 'ARM-2', qty: 8 }] },
        { seq: 30, wc: 'ZAM-WC-INT',  hours: 32, start: 'T0+21 06:00', end: 'T0+22 22:00', status: 'Planned', materials: [{ part: 'ACT-40', qty: 4 }, { part: 'MCU-X7', qty: 2 }, { part: 'WHL-1', qty: 8 }, { part: 'GBX-7', qty: 2 }] }],
      reservations: [], serials: [] },
    { code: 'WO-2026-205', product: 'P-MOB-03', line: 'ZAM-LINE-1', quantity: 1, priority: 2, releaseDate: 'T0+14', dueDate: 'T0+60', status: 'Planned',
      operations: [
        { seq: 10, wc: 'ZAM-WC-CUT',  hours: 16, start: 'T0+21 06:00', end: 'T0+21 22:00', status: 'Planned', materials: [{ part: 'HTS-22', qty: 200 }, { part: 'FAS-1', qty: 120 }] },
        { seq: 20, wc: 'ZAM-WC-WELD', hours: 24, start: 'T0+23 06:00', end: 'T0+24 14:00', status: 'Planned', materials: [{ part: 'ARM-2', qty: 4 }] },
        { seq: 30, wc: 'ZAM-WC-INT',  hours: 16, start: 'T0+25 06:00', end: 'T0+25 22:00', status: 'Planned', materials: [{ part: 'ACT-40', qty: 2 }, { part: 'MCU-X7', qty: 1 }, { part: 'WHL-1', qty: 4 }, { part: 'GBX-7', qty: 1 }] }],
      reservations: [], serials: [] },
  ],
  serialInspections: [{ code: 'QI-2026-3201', serial: 'PMV-2026-0201-Z', result: 'Passed', by: 'quality', at: 'T0-20 23:00', notes: 'Odbiór końcowy kadłuba zaliczony.' }],
  passports: [
    { serial: 'PMV-2026-0201-Z', status: 'Generated', approvedBy: 'quality', approvedAt: 'T0-19 08:00' },
    { serial: 'PMV-2026-0202-Z', status: 'Draft' },
  ],
};
writeFileSync('/tmp/plants.part2.json', JSON.stringify(zamosc, null, 1));
console.error('SITE-03 written');
import { writeFileSync } from 'node:fs';
const doc = (type, number, status, issuedOn, extra = {}) => ({
  type, status, number, fileName: `${number}.pdf`, issuedOn,
  ...(status === 'Accepted' ? { verifiedBy: 'quality', verifiedAt: `${issuedOn} 09:00` } : {}), ...extra,
});

// --------------------------- SITE-04 Leszno — integration & final test, capacity story on LES-WC-INT.
// Due dates are deliberately tight: halving the integration cell must push several orders past them.
const leszno = {
  site: { code: 'SITE-04', name: 'Zakład Leszno', country: 'PL', city: 'Leszno', lat: 51.84, lon: 16.58,
          timeZone: 'Europe/Warsaw', profileKey: 'site.profile.integration', featuredScenarioKey: 'CAPACITY_INT_50', sequence: 4 },
  lines: [{ code: 'LES-LINE-1', name: 'Linia elektroniki' }, { code: 'LES-LINE-2', name: 'Linia integracji i testów' }],
  workCenters: [
    { code: 'LES-WC-ELEC', namePl: 'Montaż elektroniki',   nameEn: 'Electronics assembly', line: 'LES-LINE-1', hoursPerDay: 16, sequence: 1 },
    { code: 'LES-WC-INT',  namePl: 'Gniazdo integracji',   nameEn: 'Integration cell',     line: 'LES-LINE-2', hoursPerDay: 16, sequence: 2 },
    { code: 'LES-WC-TEST', namePl: 'Testy końcowe',        nameEn: 'Final test',           line: 'LES-LINE-2', hoursPerDay: 16, sequence: 3 },
  ],
  purchaseOrders: [
    { code: 'PO-2026-3001', supplier: 'SUP-03', status: 'Closed', orderedOn: 'T0-60', lines: [
      { lineNo: 1, part: 'MCU-X7', qty: 50, requiredDate: 'T0-30', eta: 'T0-31', status: 'Delivered', progress: 100, deliveredOn: 'T0-31', lotNumber: 'MCU-X7-4101', documents: [doc('MATERIAL_CERT', 'MC-VIS-2026-4101', 'Accepted', 'T0-35')] },
      { lineNo: 2, part: 'ENC-4',  qty: 50, requiredDate: 'T0-30', eta: 'T0-31', status: 'Delivered', progress: 100, deliveredOn: 'T0-31', lotNumber: 'ENC-4-4102',  documents: [doc('MATERIAL_CERT', 'MC-VIS-2026-4102', 'Accepted', 'T0-35')] },
      { lineNo: 3, part: 'PCB-11', qty: 150, requiredDate: 'T0-30', eta: 'T0-31', status: 'Delivered', progress: 100, deliveredOn: 'T0-31', lotNumber: 'PCB-11-4103', documents: [doc('MATERIAL_CERT', 'MC-VIS-2026-4103', 'Accepted', 'T0-35')] } ] },
    { code: 'PO-2026-3002', supplier: 'SUP-03', status: 'Closed', orderedOn: 'T0-55', lines: [
      { lineNo: 1, part: 'ANT-2', qty: 90, requiredDate: 'T0-26', eta: 'T0-25', status: 'Delivered', progress: 100, deliveredOn: 'T0-25', lotNumber: 'ANT-2-4104', documents: [doc('MATERIAL_CERT', 'MC-VIS-2026-4104', 'Accepted', 'T0-29')] } ] },
    { code: 'PO-2026-3003', supplier: 'SUP-08', status: 'Closed', orderedOn: 'T0-52', lines: [
      { lineNo: 1, part: 'PSU-6', qty: 50, requiredDate: 'T0-24', eta: 'T0-26', status: 'Delivered', progress: 100, deliveredOn: 'T0-26', lotNumber: 'PSU-6-4105', documents: [doc('MATERIAL_CERT', 'MC-IBE-2026-4105', 'Accepted', 'T0-30')] } ] },
    { code: 'PO-2026-3004', supplier: 'SUP-06', status: 'Closed', orderedOn: 'T0-50', lines: [
      { lineNo: 1, part: 'CON-5', qty: 350, requiredDate: 'T0-22', eta: 'T0-24', status: 'Delivered', progress: 100, deliveredOn: 'T0-24', lotNumber: 'CON-5-4106', documents: [doc('MATERIAL_CERT', 'MC-RHO-2026-4106', 'Accepted', 'T0-28')] },
      { lineNo: 2, part: 'FAS-1', qty: 900, requiredDate: 'T0-22', eta: 'T0-24', status: 'Delivered', progress: 100, deliveredOn: 'T0-24', lotNumber: 'FAS-1-4107', documents: [] },
      { lineNo: 3, part: 'HRN-8', qty: 50, requiredDate: 'T0-22', eta: 'T0-24', status: 'Delivered', progress: 100, deliveredOn: 'T0-24', lotNumber: 'HRN-8-4109', documents: [doc('MATERIAL_CERT', 'MC-RHO-2026-4109', 'Accepted', 'T0-28')] } ] },
    { code: 'PO-2026-3005', supplier: 'SUP-04', status: 'Closed', orderedOn: 'T0-45', lines: [
      { lineNo: 1, part: 'DSP-2', qty: 50, requiredDate: 'T0-20', eta: 'T0-21', status: 'Delivered', progress: 100, deliveredOn: 'T0-21', lotNumber: 'DSP-2-4108', documents: [doc('MATERIAL_CERT', 'MC-BAL-2026-4108', 'Accepted', 'T0-25')] } ] },
    { code: 'PO-2026-3006', supplier: 'SUP-03', status: 'Open', orderedOn: 'T0-16', lines: [
      { lineNo: 1, part: 'MCU-X7', qty: 20, requiredDate: 'T0+15', eta: 'T0+21', status: 'InProduction', progress: 45, supplierConfirmed: false,
        documents: [doc('INSPECTION_REPORT', 'IR-VIS-2026-4120', 'Rejected', 'T0+4', { verifiedBy: 'quality', verifiedAt: 'T0+6 10:00', comment: 'Raport bez wyników testu wibracyjnego.' })] } ] },
    { code: 'PO-2026-3007', supplier: 'SUP-06', status: 'Open', orderedOn: 'T0-6', lines: [
      { lineNo: 1, part: 'CON-5', qty: 200, requiredDate: 'T0+28', eta: 'T0+29', status: 'Confirmed', progress: 10, documents: [] } ] },
  ],
  shipments: [
    { code: 'SHP-2026-3051', po: 'PO-2026-3006', status: 'Advised', carrier: 'Vistula Logistics', vehicle: 'PL-VIS-3051', plannedDeparture: 'T0+18 07:00', eta: 'T0+21', progress: 0,
      events: [{ type: 'Advised', at: 'T0+12 09:00', note: 'Awizacja dostawy modułów sterujących', location: 'Kraków' }] },
  ],
  lots: [
    { lotNumber: 'MCU-X7-4101', part: 'MCU-X7', supplier: 'SUP-03', poLine: 'PO-2026-3001/1', quantity: 50, remaining: 50, receivedOn: 'T0-31',
      documents: [doc('MATERIAL_CERT', 'MC-VIS-2026-4101L', 'Accepted', 'T0-35')], inspections: [{ code: 'QI-2026-4101', result: 'Passed', by: 'quality', at: 'T0-30 09:00' }] },
    { lotNumber: 'ENC-4-4102', part: 'ENC-4', supplier: 'SUP-03', poLine: 'PO-2026-3001/2', quantity: 50, remaining: 50, receivedOn: 'T0-31',
      documents: [doc('MATERIAL_CERT', 'MC-VIS-2026-4102L', 'Accepted', 'T0-35')], inspections: [{ code: 'QI-2026-4102', result: 'Passed', by: 'quality', at: 'T0-30 10:00' }] },
    { lotNumber: 'PCB-11-4103', part: 'PCB-11', supplier: 'SUP-03', poLine: 'PO-2026-3001/3', quantity: 150, remaining: 150, receivedOn: 'T0-31',
      documents: [doc('MATERIAL_CERT', 'MC-VIS-2026-4103L', 'Accepted', 'T0-35')], inspections: [] },
    { lotNumber: 'ANT-2-4104', part: 'ANT-2', supplier: 'SUP-03', poLine: 'PO-2026-3002/1', quantity: 90, remaining: 90, receivedOn: 'T0-25',
      documents: [doc('MATERIAL_CERT', 'MC-VIS-2026-4104L', 'Accepted', 'T0-29')], inspections: [{ code: 'QI-2026-4104', result: 'Passed', by: 'quality', at: 'T0-24 09:00' }] },
    { lotNumber: 'PSU-6-4105', part: 'PSU-6', supplier: 'SUP-08', poLine: 'PO-2026-3003/1', quantity: 50, remaining: 50, receivedOn: 'T0-26',
      documents: [doc('MATERIAL_CERT', 'MC-IBE-2026-4105L', 'Accepted', 'T0-30')], inspections: [] },
    { lotNumber: 'CON-5-4106', part: 'CON-5', supplier: 'SUP-06', poLine: 'PO-2026-3004/1', quantity: 350, remaining: 350, receivedOn: 'T0-24',
      documents: [doc('MATERIAL_CERT', 'MC-RHO-2026-4106L', 'Accepted', 'T0-28')], inspections: [] },
    { lotNumber: 'FAS-1-4107', part: 'FAS-1', supplier: 'SUP-06', poLine: 'PO-2026-3004/2', quantity: 900, remaining: 900, receivedOn: 'T0-24', documents: [], inspections: [] },
    { lotNumber: 'DSP-2-4108', part: 'DSP-2', supplier: 'SUP-04', poLine: 'PO-2026-3005/1', quantity: 50, remaining: 50, receivedOn: 'T0-21',
      documents: [doc('MATERIAL_CERT', 'MC-BAL-2026-4108L', 'Accepted', 'T0-25')], inspections: [] },
    { lotNumber: 'HRN-8-4109', part: 'HRN-8', supplier: 'SUP-06', poLine: 'PO-2026-3004/3', quantity: 50, remaining: 50, receivedOn: 'T0-24',
      documents: [doc('MATERIAL_CERT', 'MC-RHO-2026-4109L', 'Accepted', 'T0-28')], inspections: [] },
  ],
  orders: [
    { code: 'WO-2026-306', product: 'P-COM-02', line: 'LES-LINE-1', quantity: 4, priority: 3, releaseDate: 'T0-35', dueDate: 'T0-22', status: 'Completed', customerReference: 'KONTRAKT-DEMO-2025/41',
      operations: [
        { seq: 10, wc: 'LES-WC-ELEC', hours: 16, start: 'T0-28 06:00', end: 'T0-28 22:00', status: 'Completed', materials: [{ part: 'MCU-X7', qty: 4 }, { part: 'ENC-4', qty: 4 }, { part: 'PCB-11', qty: 12 }] },
        { seq: 20, wc: 'LES-WC-INT',  hours: 8,  start: 'T0-27 06:00', end: 'T0-27 14:00', status: 'Completed', materials: [{ part: 'ANT-2', qty: 8 }, { part: 'PSU-6', qty: 4 }] },
        { seq: 30, wc: 'LES-WC-TEST', hours: 8,  start: 'T0-26 06:00', end: 'T0-26 14:00', status: 'Completed', materials: [] }],
      serials: [{ serial: 'SCM-2026-0301-L', status: 'Completed', completedAt: 'T0-26 14:00' }, { serial: 'SCM-2026-0302-L', status: 'Completed', completedAt: 'T0-26 14:00' }],
      consumptions: [
        { lot: 'MCU-X7-4101', qty: 1, serial: 'SCM-2026-0301-L', opSeq: 10, at: 'T0-28 07:00' },
        { lot: 'ENC-4-4102',  qty: 1, serial: 'SCM-2026-0301-L', opSeq: 10, at: 'T0-28 07:10' },
        { lot: 'ANT-2-4104',  qty: 2, serial: 'SCM-2026-0301-L', opSeq: 20, at: 'T0-27 07:00' },
        { lot: 'MCU-X7-4101', qty: 1, serial: 'SCM-2026-0302-L', opSeq: 10, at: 'T0-28 08:00' },
        { lot: 'ENC-4-4102',  qty: 1, serial: 'SCM-2026-0302-L', opSeq: 10, at: 'T0-28 08:10' },
        { lot: 'ANT-2-4104',  qty: 2, serial: 'SCM-2026-0302-L', opSeq: 20, at: 'T0-27 08:00' }] },
    { code: 'WO-2026-301', product: 'P-COM-02', line: 'LES-LINE-1', quantity: 6, priority: 4, releaseDate: 'T0-2', dueDate: 'T0+7', status: 'InProgress', customerReference: 'KONTRAKT-DEMO-2026/41',
      operations: [
        { seq: 10, wc: 'LES-WC-ELEC', hours: 24, start: 'T0 06:00',   end: 'T0+1 14:00', status: 'InProgress', frozen: true, materials: [{ part: 'MCU-X7', qty: 6 }, { part: 'ENC-4', qty: 6 }, { part: 'PCB-11', qty: 18 }] },
        { seq: 20, wc: 'LES-WC-INT',  hours: 24, start: 'T0+2 06:00', end: 'T0+3 14:00', status: 'Planned', materials: [{ part: 'ANT-2', qty: 12 }, { part: 'PSU-6', qty: 6 }] },
        { seq: 30, wc: 'LES-WC-TEST', hours: 8,  start: 'T0+4 06:00', end: 'T0+4 14:00', status: 'Planned', materials: [] }],
      reservations: [{ part: 'MCU-X7', qty: 6, lot: 'MCU-X7-4101' }, { part: 'ANT-2', qty: 12, lot: 'ANT-2-4104' }], serials: [] },
    { code: 'WO-2026-302', product: 'P-COM-02', line: 'LES-LINE-1', quantity: 8, priority: 5, releaseDate: 'T0', dueDate: 'T0+9', status: 'Released', customerReference: 'KONTRAKT-DEMO-2026/42-PRIORYTET',
      operations: [
        { seq: 10, wc: 'LES-WC-ELEC', hours: 32, start: 'T0+2 06:00', end: 'T0+3 22:00', status: 'Planned', materials: [{ part: 'MCU-X7', qty: 8 }, { part: 'ENC-4', qty: 8 }, { part: 'PCB-11', qty: 24 }] },
        { seq: 20, wc: 'LES-WC-INT',  hours: 32, start: 'T0+4 06:00', end: 'T0+7 22:00', status: 'Planned', materials: [{ part: 'ANT-2', qty: 16 }, { part: 'PSU-6', qty: 8 }] },
        { seq: 30, wc: 'LES-WC-TEST', hours: 8,  start: 'T0+8 06:00', end: 'T0+8 14:00', status: 'Planned', materials: [] }],
      reservations: [{ part: 'MCU-X7', qty: 8, lot: 'MCU-X7-4101' }], serials: [] },
    { code: 'WO-2026-303', product: 'P-COM-02', line: 'LES-LINE-1', quantity: 10, priority: 4, releaseDate: 'T0+7', dueDate: 'T0+16', status: 'Released',
      operations: [
        { seq: 10, wc: 'LES-WC-ELEC', hours: 40, start: 'T0+7 06:00',  end: 'T0+9 14:00',  status: 'Planned', materials: [{ part: 'MCU-X7', qty: 10 }, { part: 'ENC-4', qty: 10 }, { part: 'PCB-11', qty: 30 }] },
        { seq: 20, wc: 'LES-WC-INT',  hours: 40, start: 'T0+10 06:00', end: 'T0+14 14:00', status: 'Planned', materials: [{ part: 'ANT-2', qty: 20 }, { part: 'PSU-6', qty: 10 }] },
        { seq: 30, wc: 'LES-WC-TEST', hours: 8,  start: 'T0+15 06:00', end: 'T0+15 14:00', status: 'Planned', materials: [] }],
      reservations: [{ part: 'MCU-X7', qty: 10, lot: 'MCU-X7-4101' }], serials: [] },
    { code: 'WO-2026-304', product: 'P-COM-02', line: 'LES-LINE-1', quantity: 6, priority: 3, releaseDate: 'T0+14', dueDate: 'T0+19', status: 'Planned',
      operations: [
        { seq: 10, wc: 'LES-WC-ELEC', hours: 24, start: 'T0+14 06:00', end: 'T0+15 14:00', status: 'Planned', materials: [{ part: 'MCU-X7', qty: 6 }, { part: 'ENC-4', qty: 6 }, { part: 'PCB-11', qty: 18 }] },
        { seq: 20, wc: 'LES-WC-INT',  hours: 24, start: 'T0+16 06:00', end: 'T0+17 14:00', status: 'Planned', materials: [{ part: 'ANT-2', qty: 12 }, { part: 'PSU-6', qty: 6 }] },
        { seq: 30, wc: 'LES-WC-TEST', hours: 8,  start: 'T0+18 06:00', end: 'T0+18 14:00', status: 'Planned', materials: [] }],
      reservations: [{ part: 'MCU-X7', qty: 6, lot: 'MCU-X7-4101' }], serials: [] },
    { code: 'WO-2026-305', product: 'P-COM-02', line: 'LES-LINE-1', quantity: 8, priority: 3, releaseDate: 'T0+18', dueDate: 'T0+26', status: 'Planned',
      operations: [
        { seq: 10, wc: 'LES-WC-ELEC', hours: 32, start: 'T0+21 06:00', end: 'T0+22 22:00', status: 'Planned', materials: [{ part: 'MCU-X7', qty: 8 }, { part: 'ENC-4', qty: 8 }, { part: 'PCB-11', qty: 24 }] },
        { seq: 20, wc: 'LES-WC-INT',  hours: 32, start: 'T0+23 06:00', end: 'T0+24 22:00', status: 'Planned', materials: [{ part: 'ANT-2', qty: 16 }, { part: 'PSU-6', qty: 8 }] },
        { seq: 30, wc: 'LES-WC-TEST', hours: 8,  start: 'T0+25 06:00', end: 'T0+25 14:00', status: 'Planned', materials: [] }],
      reservations: [], serials: [] },
  ],
  serialInspections: [{ code: 'QI-2026-4201', serial: 'SCM-2026-0301-L', result: 'Passed', by: 'quality', at: 'T0-26 15:00', notes: 'Test końcowy zaliczony.' }],
  passports: [
    { serial: 'SCM-2026-0301-L', status: 'Generated', approvedBy: 'quality', approvedAt: 'T0-26 16:00' },
    { serial: 'SCM-2026-0302-L', status: 'Draft' },
  ],
};
writeFileSync('/tmp/plants.part3.json', JSON.stringify(leszno, null, 1));
console.error('SITE-04 written');
