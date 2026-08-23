import { test, expect } from '@playwright/test';
import { apiAs, resetDemo } from '../helpers/api';

test.describe('traceability, lot blocking and quality passports', () => {
  test.beforeAll(async () => { await resetDemo(); });
  test.afterAll(async () => { await resetDemo(); });

  test('trace-back and trace-forward are consistent', async () => {
    const q = await apiAs('QualityInspector');
    const serial = await (await q.get('/api/v1/trace/serials/PMV-2026-0007')).json();
    expect(JSON.stringify(serial.genealogy)).toContain('HTS-22-2608');
    const forward = await (await q.get('/api/v1/trace/lots/HTS-22-2608/forward')).json();
    expect(forward.serials.map((s: { code?: string; serial?: string }) => s.code ?? s.serial)).toContain('PMV-2026-0007');
    await q.dispose();
  });

  test('complete passport generates a versioned PDF with SHA-256 and QR', async () => {
    const q = await apiAs('QualityInspector');
    const passport = await (await q.get('/api/v1/passports/PMV-2026-0007')).json();
    expect(passport.completeness.complete).toBeTruthy();
    const gen = await q.post('/api/v1/passports/PMV-2026-0007/generate');
    expect(gen.ok()).toBeTruthy();
    const version = await gen.json();
    expect(version.sha256).toMatch(/^[0-9a-f]{64}$/i);
    const pdf = await q.get(`/api/v1/passports/PMV-2026-0007/versions/${version.version}/pdf`);
    expect(pdf.ok()).toBeTruthy();
    expect((await pdf.body()).subarray(0, 5).toString()).toBe('%PDF-');
    const qr = await q.get('/api/v1/passports/PMV-2026-0007/qr');
    expect(qr.ok()).toBeTruthy();
    await q.dispose();
  });

  test('incomplete passport is refused with the list of missing items', async () => {
    const q = await apiAs('QualityInspector');
    const res = await q.post('/api/v1/passports/SCM-2026-0103/generate');
    expect(res.status()).toBe(422);
    const problem = await res.json();
    expect(JSON.stringify(problem)).toMatch(/missing/i);
    await q.dispose();
  });

  test('blocking HTS-22-2608 invalidates affected passports and flags orders', async () => {
    const q = await apiAs('QualityInspector');
    const block = await q.post('/api/v1/lots/HTS-22-2608/block', {
      data: { reason: 'e2e quality issue', ncrTitle: 'NCR e2e' },
    });
    expect(block.ok()).toBeTruthy();
    const affected = await block.json();
    expect(JSON.stringify(affected)).toContain('PMV-2026-0007');
    const passport = await (await q.get('/api/v1/passports/PMV-2026-0007')).json();
    expect(passport.status).toBe('Invalidated');
    await q.dispose();
  });
});
