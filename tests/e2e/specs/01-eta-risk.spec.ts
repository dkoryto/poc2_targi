import { test, expect } from '@playwright/test';
import { apiAs, resetDemo, kpi } from '../helpers/api';

test.describe('supplier ETA update drives risk and the dashboard', () => {
  test.beforeAll(async () => { await resetDemo(); });
  test.afterAll(async () => { await resetDemo(); });

  test('ETA +10 days on ACT-40 raises risk to Critical and flags WO-2026-014', async () => {
    const before = await kpi('HIGH_RISK_DELIVERIES');
    expect(before).toBe(3);

    const supplier = await apiAs('SupplierUser', 'SUP-02');
    const po = await (await supplier.get('/api/v1/purchase-orders/PO-2026-0007')).json();
    const line = po.lines.find((l: { partCode: string }) => l.partCode === 'ACT-40');
    expect(line.risk.category).toBe('Medium');

    const newEta = new Date(new Date(line.eta).getTime() + 10 * 864e5).toISOString().slice(0, 10);
    const res = await supplier.post(`/api/v1/purchase-orders/PO-2026-0007/lines/${line.id}/eta`, {
      data: { eta: newEta, reason: 'PRODUCTION_DELAY', comment: 'e2e' },
    });
    expect(res.ok()).toBeTruthy();
    const body = await res.json();
    expect(body.risk.category).toBe('Critical');
    expect(body.risk.score).toBeGreaterThanOrEqual(70);
    expect(body.risk.factors.length).toBeGreaterThanOrEqual(3);
    expect(JSON.stringify(body.endangeredOrders)).toContain('WO-2026-014');
    await supplier.dispose();

    expect(await kpi('HIGH_RISK_DELIVERIES')).toBe(before + 1);
    expect(await kpi('PREDICTED_DOWNTIME_H')).toBe(36);
  });

  test('supplier cannot read another supplier data', async () => {
    const sup2 = await apiAs('SupplierUser', 'SUP-02');
    const foreign = await sup2.get('/api/v1/purchase-orders/PO-2026-0013'); // SUP-01
    expect([403, 404]).toContain(foreign.status());
    await sup2.dispose();
  });

  test('the change is audited', async () => {
    const auditor = await apiAs('Auditor');
    // Audit rows are written through the outbox dispatcher (~500 ms poll), so poll rather
    // than asserting once — a bare read races the dispatcher and fails intermittently.
    let audit: { items: { correlationId: string; before: unknown; after: unknown }[] } = { items: [] };
    await expect
      .poll(async () => {
        audit = await (await auditor.get('/api/v1/audit?entity=PurchaseOrderLine')).json();
        return audit.items.length;
      }, { timeout: 15_000, intervals: [250, 500, 1000] })
      .toBeGreaterThan(0);
    const entry = audit.items[0]!;
    expect(entry.correlationId).toBeTruthy();
    expect(entry.before).toBeTruthy();
    expect(entry.after).toBeTruthy();
    await auditor.dispose();
  });
});
