import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { AuditPage } from './AuditPage';
import { diffKeys } from './JsonDiff';
import { renderWithProviders } from '@/test/utils';

describe('Audit', () => {
  it('diffKeys flags changed keys only', () => {
    const rows = diffKeys({ status: 'Approved', version: 0, nested: { a: 1 } }, { status: 'Generated', version: 1, nested: { a: 1 } });
    expect(rows.find((r) => r.key === 'status')?.changed).toBe(true);
    expect(rows.find((r) => r.key === 'nested.a')?.changed).toBe(false);
    expect(rows.find((r) => r.key === 'version')).toMatchObject({ before: '0', after: '1', changed: true });
  });
  it('expands a row to show before/after diff with changed keys highlighted', async () => {
    const user = userEvent.setup();
    renderWithProviders(<AuditPage />, { route: '/audit', auth: true });
    await waitFor(() => expect(screen.getByTestId('audit-table')).toBeInTheDocument());
    await user.click(screen.getByTestId('audit-row-a-1'));
    const diff = await screen.findByTestId('json-diff');
    expect(diff).toHaveTextContent('status');
    expect(diff.querySelectorAll('tr[data-changed="true"]').length).toBeGreaterThanOrEqual(2);
    expect(screen.getByTestId('audit-detail')).toHaveTextContent('c0ffee01');
  });
});
