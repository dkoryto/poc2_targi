import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, beforeEach } from 'vitest';
import { Routes, Route } from 'react-router';
import { AppShell } from '@/components/layout/AppShell';
import { DashboardPage } from '@/features/dashboard/DashboardPage';
import { PlanningPage } from '@/features/planning/PlanningPage';
import { renderWithProviders } from '@/test/utils';
import { resolveActiveSite } from './sites';
import { SITES } from '@/mocks/sites';

function Shell() {
  return (
    <Routes>
      <Route element={<AppShell />}>
        <Route index element={<DashboardPage />} />
        <Route path="/planning" element={<PlanningPage />} />
        <Route path="*" element={<div>other</div>} />
      </Route>
    </Routes>
  );
}

beforeEach(() => {
  try {
    localStorage.clear();
  } catch {
    /* jsdom without localStorage */
  }
});

describe('resolveActiveSite', () => {
  it('prefers a stored plant, then the user default, then the flagged default', () => {
    expect(resolveActiveSite(SITES, 'SITE-03', 'SITE-02', undefined)).toBe('SITE-03');
    expect(resolveActiveSite(SITES, null, 'SITE-02', undefined)).toBe('SITE-02');
    expect(resolveActiveSite(SITES, null, undefined, undefined)).toBe('SITE-01');
  });

  it('never returns a plant the user may not access', () => {
    expect(resolveActiveSite(SITES, 'SITE-03', 'SITE-01', ['SITE-01', 'SITE-04'])).toBe('SITE-01');
    expect(resolveActiveSite(SITES, 'SITE-04', 'SITE-01', ['SITE-01', 'SITE-04'])).toBe('SITE-04');
  });
});

describe('plant switching', () => {
  it('switches the whole control room to the selected plant', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Shell />, { auth: true });

    // Kielce is the default and keeps the golden-path numbers.
    await waitFor(() => expect(screen.getByTestId('kpi-HIGH_RISK_DELIVERIES')).toHaveTextContent('3'));
    expect(screen.getByTestId('site-switch')).toHaveTextContent('Kielce');

    await user.click(screen.getByTestId('site-switch'));
    await user.click(screen.getByTestId('site-option-SITE-02'));

    await waitFor(() => expect(screen.getByTestId('kpi-HIGH_RISK_DELIVERIES')).toHaveTextContent('2'));
    expect(screen.getByTestId('kpi-PREDICTED_DOWNTIME_H')).toHaveTextContent('12');
    expect(screen.getByTestId('site-switch')).toHaveTextContent('Piła');
  });

  it('shows no other plant’s records after a switch', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Shell />, { auth: true });
    await waitFor(() => expect(screen.getByTestId('panel-plan')).toBeInTheDocument());
    await waitFor(() => expect(screen.getByTestId('gantt')).toBeInTheDocument());
    expect(screen.getByTestId('panel-plan')).toHaveTextContent('WC-CUT');

    await user.click(screen.getByTestId('site-switch'));
    await user.click(screen.getByTestId('site-option-SITE-03'));

    // Zamość uses its own ZAM- work centres; Kielce's must be gone entirely.
    await waitFor(() => expect(screen.getByTestId('panel-plan')).toHaveTextContent('ZAM-WC-CUT'));
    const plan = screen.getByTestId('panel-plan');
    expect(within(plan).queryByText('WO-2026-014')).not.toBeInTheDocument();
  });

  it('persists the choice for the next session', async () => {
    const user = userEvent.setup();
    const first = renderWithProviders(<Shell />, { auth: true });
    await waitFor(() => expect(screen.getByTestId('site-switch')).toHaveTextContent('Kielce'));
    await user.click(screen.getByTestId('site-switch'));
    await user.click(screen.getByTestId('site-option-SITE-04'));
    await waitFor(() => expect(screen.getByTestId('site-switch')).toHaveTextContent('Leszno'));
    first.unmount();

    renderWithProviders(<Shell />, { auth: true });
    await waitFor(() => expect(screen.getByTestId('site-switch')).toHaveTextContent('Leszno'));
  });

  it('offers only the plants a supplier actually supplies', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Shell />, { auth: true });
    await waitFor(() => expect(screen.getByTestId('current-role')).toHaveTextContent('Prezenter demo'));
    await user.click(screen.getByTestId('user-menu'));
    await user.click(screen.getByTestId('switch-role-SupplierUser-SUP-02'));
    await waitFor(() => expect(screen.getByTestId('current-role')).toHaveTextContent('Dostawca'));

    await user.click(screen.getByTestId('site-switch'));
    expect(screen.getByTestId('site-option-SITE-01')).toBeInTheDocument();
    expect(screen.getByTestId('site-option-SITE-04')).toBeInTheDocument();
    expect(screen.queryByTestId('site-option-SITE-02')).not.toBeInTheDocument();
    expect(screen.queryByTestId('site-option-SITE-03')).not.toBeInTheDocument();
  });

  it('badges each plant’s own headline scenario', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Shell />, { auth: true, route: '/planning' });
    await waitFor(() => expect(screen.getByTestId('scenario-featured-DELAY_ACT40_10D')).toBeInTheDocument());

    await user.click(screen.getByTestId('site-switch'));
    await user.click(screen.getByTestId('site-option-SITE-04'));

    await waitFor(() => expect(screen.getByTestId('scenario-featured-CAPACITY_INT_50')).toBeInTheDocument());
    expect(screen.queryByTestId('scenario-featured-DELAY_ACT40_10D')).not.toBeInTheDocument();
  });
});
