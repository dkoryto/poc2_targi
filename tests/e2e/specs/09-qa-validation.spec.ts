/**
 * Regression cover for defects found during the full QA sweep.
 * These assert the CORRECT behaviour, so they fail until each defect is fixed.
 */
import { test, expect } from '@playwright/test';
import { apiAs, resetDemo } from '../helpers/api';

test.describe('malformed uploads are rejected, not crashed on', () => {
  test.beforeAll(async () => { await resetDemo(); });

  // Content sniffing and the size cap already work correctly and answer 400 with a precise
  // message. What does not is binding of the poLineId field itself.
  for (const [label, poLineId] of [
    ['missing', undefined],
    ['empty', ''],
    ['not a GUID', 'not-a-guid'],
  ] as const) {
    test(`an upload whose poLineId is ${label} is refused with a validation error, not 500`, async () => {
      const supplier = await apiAs('SupplierUser', 'SUP-02');
      const multipart: Record<string, unknown> = {
        file: { name: 'a.pdf', mimeType: 'application/pdf', buffer: Buffer.from('%PDF-') },
        type: 'MATERIAL_CERT',
        documentNumber: 'QA-BIND-1',
        issuedOn: '2026-08-01',
      };
      if (poLineId !== undefined) multipart.poLineId = poLineId;
      const res = await supplier.post('/api/v1/documents', { multipart: multipart as never });
      // A supplier sending an incomplete form must be told which field is wrong. A 500 also
      // lands in the administrator's "recent errors" panel during a demonstration.
      expect(res.status(), await res.text()).toBeLessThan(500);
      expect(res.status()).toBeGreaterThanOrEqual(400);
      await supplier.dispose();
    });
  }

  test('a well-formed upload referencing an unknown line answers 404', async () => {
    const supplier = await apiAs('SupplierUser', 'SUP-02');
    const res = await supplier.post('/api/v1/documents', {
      multipart: {
        file: { name: 'a.pdf', mimeType: 'application/pdf', buffer: Buffer.from('%PDF-') },
        type: 'MATERIAL_CERT',
        poLineId: '00000000-0000-0000-0000-000000000001',
        documentNumber: 'QA-BIND-2',
        issuedOn: '2026-08-01',
      },
    });
    expect(res.status()).toBe(404);
    await supplier.dispose();
  });
});

test.describe('the lot-block dialog explains why it refused', () => {
  test.beforeAll(async () => { await resetDemo(); });
  test.afterAll(async () => { await resetDemo(); });

  test('submitting with empty fields shows a message instead of failing silently', async ({ page }) => {
    await page.goto('/trace/lots/HTS-22-2608');
    await page.getByTestId('btn-block-lot').first().click();
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();

    // Both fields are marked required in the label ("POWÓD BLOKADY *", "TYTUŁ NCR *").
    await page.getByRole('button', { name: /Zablokuj partię/ }).last().click();
    await page.waitForTimeout(1500);

    // Whatever the mechanism (client-side guard, disabled submit, or rendering the API's
    // field errors), the presenter must see why nothing happened.
    await expect(dialog).toBeVisible();
    await expect(dialog).toContainText(/wymagan|uzupełnij|nie może być puste|podaj/i);
  });

  test('blocking with both fields filled invalidates the affected passports', async ({ page }) => {
    await page.goto('/trace/lots/HTS-22-2608');
    await page.getByTestId('btn-block-lot').first().click();
    await page.getByLabel(/POWÓD BLOKADY/i).fill('Niezgodność wymiarowa wykryta przy odbiorze');
    await page.getByLabel(/TYTUŁ NCR/i).fill('NCR-QA-001');
    await page.getByRole('button', { name: /Zablokuj partię/ }).last().click();
    await page.waitForTimeout(2500);

    const q = await apiAs('QualityInspector');
    const passport = await (await q.get('/api/v1/passports/PMV-2026-0007')).json();
    expect(passport.status).toBe('Invalidated');
    await q.dispose();
  });
});
