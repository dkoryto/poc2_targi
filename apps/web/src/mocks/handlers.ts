import { http, HttpResponse, delay } from 'msw';
import * as F from './fixtures';
import { wave2Handlers, resetWave2, bindCurrentUser } from './wave2';
import type { EtaChangeRequest, LogisticsEvent, Role, UserContext } from '@/api/types';

const B = '/api/v1';
let currentUser: UserContext = F.presenter;
let po = structuredClone(F.po0007);
let events = structuredClone(F.logisticsEvents);
let notifs = structuredClone(F.notifications);
let kpis = structuredClone(F.kpis);

export function resetMockState() {
  currentUser = F.presenter;
  resetWave2();
  po = structuredClone(F.po0007);
  events = structuredClone(F.logisticsEvents);
  notifs = structuredClone(F.notifications);
  kpis = structuredClone(F.kpis);
}

function userFor(role: Role, supplierCode?: string | null): UserContext {
  if (role === 'SupplierUser') return { ...F.supplierUser, supplierId: supplierCode ?? 'SUP-02' };
  return { ...F.presenter, id: `u-${role}`, username: role.toLowerCase(), displayName: role, role };
}

bindCurrentUser(() => ({ username: currentUser.username, role: currentUser.role }));

export const handlers = [
  ...wave2Handlers,
  http.get('/health/live', () => HttpResponse.text('Healthy')),
  http.get(`${B}/demo/status`, () => HttpResponse.json({ demoMode: true, seedVersion: 'mock-1', seededAt: F.t0(0), lastResetMs: 1200 })),
  http.get(`${B}/demo/script`, () => HttpResponse.json(F.demoScript)),
  http.post(`${B}/demo/reset`, async () => { await delay(200); resetMockState(); return HttpResponse.json({ durationMs: 1234, seedVersion: 'mock-1', counts: { purchaseOrders: 18 } }); }),
  http.get(`${B}/auth/demo-accounts`, () => HttpResponse.json([{ username: 'supplier.hydromech', role: 'SupplierUser', supplierCode: 'SUP-02', description: 'Hydromech Actuators GmbH' }, { username: 'supplier.nordstal', role: 'SupplierUser', supplierCode: 'SUP-01', description: 'Nordstal Sp. z o.o.' }])),
  http.get(`${B}/auth/demo-login`, ({ request }) => {
    const url = new URL(request.url);
    currentUser = userFor((url.searchParams.get('role') as Role) ?? 'DemoPresenter', url.searchParams.get('supplierCode'));
    return HttpResponse.json({ accessToken: 'mock-token', expiresAt: F.t0(1), user: currentUser });
  }),
  http.post(`${B}/auth/login`, async ({ request }) => {
    const body = (await request.json()) as { username: string; password: string };
    if (body.password !== 'demo') return HttpResponse.json({ title: 'Unauthorized', status: 401 }, { status: 401 });
    currentUser = body.username.startsWith('supplier') ? F.supplierUser : F.presenter;
    return HttpResponse.json({ accessToken: 'mock-token', expiresAt: F.t0(1), user: currentUser });
  }),
  http.get(`${B}/auth/me`, () => HttpResponse.json(currentUser)),

  http.get(`${B}/dashboard/kpis`, () => HttpResponse.json(kpis)),
  http.get(`${B}/dashboard/map`, () => HttpResponse.json(F.mapData)),
  http.get(`${B}/dashboard/risk-heatmap`, () => HttpResponse.json(F.heatmap)),
  http.get(`${B}/dashboard/quality-status`, () => HttpResponse.json(F.qualityStatus)),
  http.get(`${B}/dashboard/plan`, () => HttpResponse.json(F.plan)),

  http.get(`${B}/suppliers`, () => HttpResponse.json({ items: currentUser.role === 'SupplierUser' ? F.suppliers.filter((s) => s.code === currentUser.supplierId) : F.suppliers, total: F.suppliers.length })),
  http.get(`${B}/purchase-orders`, ({ request }) => {
    const url = new URL(request.url);
    let items = F.poList;
    if (currentUser.role === 'SupplierUser') items = items.filter((p) => p.supplierCode === currentUser.supplierId);
    const risk = url.searchParams.get('riskCategory');
    if (risk) items = items.filter((p) => p.riskCategory === risk);
    const q = url.searchParams.get('q');
    if (q) items = items.filter((p) => p.code.toLowerCase().includes(q.toLowerCase()));
    return HttpResponse.json({ items, total: items.length });
  }),
  http.get(`${B}/purchase-orders/:code`, ({ params }) => {
    if (params.code !== 'PO-2026-0007') return HttpResponse.json({ title: 'Not found', status: 404, detail: `PO ${String(params.code)}` }, { status: 404 });
    if (currentUser.role === 'SupplierUser' && currentUser.supplierId !== 'SUP-02') return HttpResponse.json({ title: 'Not found', status: 404 }, { status: 404 });
    return HttpResponse.json(po);
  }),
  http.get(`${B}/purchase-orders/:code/lines/:lineId/impact`, () => HttpResponse.json({ risk: po.lines[0]!.risk, endangeredOrders: po.lines[0]!.risk.endangeredOrders, predictedDowntimeHours: po.lines[0]!.risk.category === 'Critical' ? 36 : 0 })),
  http.post(`${B}/purchase-orders/:code/lines/:lineId/eta`, async ({ request }) => {
    const body = (await request.json()) as EtaChangeRequest;
    const line = po.lines[0]!;
    const lateDays = Math.round((new Date(body.eta).getTime() - new Date(line.requiredDate).getTime()) / 86_400_000);
    const risk = lateDays > 0 ? F.act40RiskAfter : F.act40Risk;
    line.eta = body.eta;
    line.risk = risk;
    line.rowVersion = String(Number(line.rowVersion) + 1);
    po.history.unshift({ id: `h-${Date.now()}`, occurredAt: new Date().toISOString(), user: currentUser.username, action: 'EtaChanged', field: 'eta', before: F.act40Line.eta, after: body.eta, comment: body.comment ?? body.reason });
    if (lateDays > 0) {
      kpis = { ...kpis, items: kpis.items.map((k) => (k.code === 'HIGH_RISK_DELIVERIES' ? { ...k, value: 4, trend: 2, status: 'critical' } : k.code === 'PREDICTED_DOWNTIME_H' ? { ...k, value: 36, trend: 36, status: 'critical' } : k)) };
      notifs.unshift({ id: `n-${Date.now()}`, createdAt: new Date().toISOString(), title: 'Ryzyko krytyczne', message: 'PO-2026-0007/1 ACT-40: ETA +10 dni, zagrożone WO-2026-014.', severity: 'critical', read: false, route: '/supply/orders/PO-2026-0007' });
    }
    return HttpResponse.json({ line, risk, endangeredOrders: risk.endangeredOrders });
  }),
  http.patch(`${B}/purchase-orders/:code/lines/:lineId`, async ({ request }) => {
    const ifMatch = request.headers.get('If-Match');
    const line = po.lines[0]!;
    if (ifMatch && ifMatch !== line.rowVersion) return HttpResponse.json({ title: 'Precondition Failed', status: 412 }, { status: 412 });
    const body = (await request.json()) as Partial<typeof line>;
    Object.assign(line, body);
    line.rowVersion = String(Number(line.rowVersion) + 1);
    return HttpResponse.json(line, { headers: { ETag: line.rowVersion } });
  }),
  http.post(`${B}/documents`, async ({ request }) => {
    const fd = await request.formData();
    const file = fd.get('file') as File | null;
    const doc = { id: `doc-${Date.now()}`, type: String(fd.get('type')), fileName: file?.name ?? 'file.pdf', sizeBytes: file?.size ?? 0, sha256: 'mock0000000000000000000000000000', status: 'Pending', uploadedAt: new Date().toISOString(), uploadedBy: currentUser.username, documentNumber: String(fd.get('documentNumber')) };
    po.lines[0]!.documents.push(doc as never);
    return HttpResponse.json(doc, { status: 201 });
  }),
  http.get(`${B}/shipments`, () => HttpResponse.json({ items: currentUser.role === 'SupplierUser' ? F.shipments.filter((s) => s.supplierCode === currentUser.supplierId) : F.shipments, total: F.shipments.length })),
  http.get(`${B}/shipments/:code`, ({ params }) => {
    const sh = F.shipments.find((s) => s.code === params.code);
    return sh ? HttpResponse.json(sh) : HttpResponse.json({ title: 'Not found', status: 404 }, { status: 404 });
  }),
  http.post(`${B}/shipments`, async ({ request }) => {
    const body = (await request.json()) as { poCode: string; lineIds: string[]; carrier: string; vehicle: string; plannedDeparture: string; eta: string };
    return HttpResponse.json({ ...F.shipments[0], code: 'SHP-2026-0099', ...body, status: 'Planned', progress: 0, events: [], lines: [] }, { status: 201 });
  }),
  http.post(`${B}/shipments/:code/events`, async ({ request }) => {
    const body = (await request.json()) as { type: string; occurredAt: string; note?: string };
    return HttpResponse.json({ id: `e-${Date.now()}`, ...body, user: currentUser.username }, { status: 201 });
  }),
  http.get(`${B}/logistics-events`, () => HttpResponse.json({ items: events, total: events.length })),
  http.post(`${B}/logistics-events`, async ({ request }) => {
    const body = (await request.json()) as Omit<LogisticsEvent, 'id' | 'raisedAt' | 'active'>;
    const ev: LogisticsEvent = { id: `le-${Date.now()}`, raisedAt: new Date().toISOString(), active: true, ...body };
    events = [ev, ...events];
    return HttpResponse.json(ev, { status: 201 });
  }),
  http.get(`${B}/notifications`, ({ request }) => {
    const unread = new URL(request.url).searchParams.get('unreadOnly') === 'true';
    const items = unread ? notifs.filter((n) => !n.read) : notifs;
    return HttpResponse.json({ items, total: items.length });
  }),
  http.post(`${B}/notifications/:id/read`, ({ params }) => { notifs = notifs.map((n) => (n.id === params.id ? { ...n, read: true } : n)); return new HttpResponse(null, { status: 204 }); }),
  http.get(`${B}/admin/status`, () => HttpResponse.json({ services: [{ name: 'postgres', status: 'up', latencyMs: 3 }, { name: 'minio', status: 'up', latencyMs: 5 }, { name: 'planning-engine', status: 'up', latencyMs: 12 }, { name: 'local-ai', status: 'disabled' }], recentErrors: [] })),
];
