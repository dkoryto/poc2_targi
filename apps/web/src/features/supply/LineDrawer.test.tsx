import { screen, waitFor, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { LineDrawer, etaSchema } from './LineDrawer';
import { act40Line, t0d } from '@/mocks/fixtures';
import { renderWithProviders } from '@/test/utils';
import { AuthProvider } from '@/features/auth/auth';

describe('ETA change form', () => {
  it('schema requires eta and reason', () => {
    expect(etaSchema.safeParse({ eta: '', reason: 'LOGISTICS' }).success).toBe(false);
    expect(etaSchema.safeParse({ eta: '2026-09-25', reason: 'NOPE' }).success).toBe(false);
    expect(etaSchema.safeParse({ eta: '2026-09-25', reason: 'LOGISTICS' }).success).toBe(true);
  });
  it('submits +10 days and shows before/after risk with endangered orders', async () => {
    const user = userEvent.setup();
    renderWithProviders(
      <AuthProvider>
        <LineDrawer poCode="PO-2026-0007" line={act40Line} onClose={() => {}} />
      </AuthProvider>,
    );
    await waitFor(() => expect(screen.getByTestId('submit-eta')).toBeEnabled());
    const eta = screen.getByLabelText(/Nowe ETA/) as HTMLInputElement;
    fireEvent.change(eta, { target: { value: t0d(18) } });
    await user.selectOptions(screen.getByLabelText(/Powód/), 'PRODUCTION_DELAY');
    await user.click(screen.getByTestId('submit-eta'));
    await waitFor(() => expect(screen.getByTestId('risk-after-score')).toHaveTextContent('79'));
    expect(screen.getByTestId('endangered-orders')).toHaveTextContent('WO-2026-014');
    expect(screen.getAllByText(/Krytyczne/).length).toBeGreaterThan(0);
  });
});
