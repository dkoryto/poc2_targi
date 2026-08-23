import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { Route, Routes } from 'react-router';
import { PlanningPage } from './PlanningPage';
import { ScenarioDetailPage } from './ScenarioDetailPage';
import { renderWithProviders } from '@/test/utils';
import { server } from '@/mocks/server';
import { http, HttpResponse } from 'msw';
import { setToken } from '@/api/client';

function App() {
  return (
    <Routes>
      <Route path="/planning" element={<PlanningPage />} />
      <Route path="/planning/scenarios/:id" element={<ScenarioDetailPage />} />
    </Routes>
  );
}

describe('Planning / What-If', () => {
  it('runs the ACT-40 preset and renders explanations, KPI delta and compare Gantt', async () => {
    const user = userEvent.setup();
    renderWithProviders(<App />, { route: '/planning', auth: true });
    await waitFor(() => expect(screen.getByTestId('baseline-meta')).toHaveTextContent('v1'));
    const tile = await screen.findByTestId('scenario-tile-DELAY_ACT40_10D');
    // bare titleKey from the API must still resolve to the localized preset name
    expect(tile).toHaveTextContent('Opóźnij siłowniki ACT-40 o 10 dni');
    await waitFor(() => expect(tile).toBeEnabled());
    await user.click(tile);
    await waitFor(() => expect(screen.getByTestId('scenario-detail')).toBeInTheDocument());
    await waitFor(() => expect(screen.getByTestId('scenario-status')).toHaveTextContent('Completed'), { timeout: 5000 });
    expect(screen.getByTestId('explanation-ORDER_PULLED_FORWARD')).toHaveTextContent('WO-2026-019');
    expect(screen.getByTestId('explanation-DOWNTIME_REDUCED')).toHaveTextContent('36');
    expect(screen.getByTestId('explanation-DOWNTIME_REDUCED')).toHaveTextContent('8');
    expect(screen.getByTestId('explanation-ORDER_DELAYED_MATERIAL_SHORTAGE')).toHaveTextContent('ACT-40');
    const delta = screen.getByTestId('kpi-delta-downtime');
    expect(delta).toHaveTextContent('36');
    expect(delta).toHaveTextContent('8');
    expect(delta).toHaveTextContent('−28');
    // compare mode renders ghost bars and the shifted WO-2026-019 INT op
    expect(screen.getAllByTestId('gantt-ghost').length).toBeGreaterThan(0);
    expect(within(screen.getByTestId('gantt')).getByTestId('gantt-bar-WO-2026-019/20')).toHaveAttribute('data-changed', 'true');
    expect(screen.getByTestId('solver-badge')).toHaveTextContent('dspc-heuristic/1.0');
  });

  it('approve is gated by role: Auditor cannot approve, DemoPresenter can and baseline version bumps', async () => {
    const user = userEvent.setup();
    setToken(null);
    server.use(http.get('/api/v1/auth/demo-login', () => HttpResponse.json({ accessToken: 'tok', expiresAt: '2099-01-01', user: { id: 'u-a', username: 'auditor', displayName: 'Auditor', role: 'Auditor', siteId: 'SITE-01', locale: 'pl', demoMode: true } })));
    const { unmount } = renderWithProviders(<App />, { route: '/planning', auth: true });
    const tile = await screen.findByTestId('scenario-tile-DELAY_ACT40_10D');
    await waitFor(() => expect(screen.getByText(/tylko na przeglądanie/)).toBeInTheDocument());
    expect(tile).toBeDisabled();
    unmount();
    server.resetHandlers();
    setToken(null);

    renderWithProviders(<App />, { route: '/planning', auth: true });
    const tile2 = await screen.findByTestId('scenario-tile-DELAY_ACT40_10D');
    await waitFor(() => expect(tile2).toBeEnabled());
    await user.click(tile2);
    await waitFor(() => expect(screen.getByTestId('btn-approve-plan')).toBeEnabled(), { timeout: 5000 });
    await user.click(screen.getByTestId('btn-approve-plan'));
    await user.click(screen.getByTestId('confirm-button'));
    await waitFor(() => expect(screen.getByTestId('scenario-status')).toHaveTextContent('Approved'));
    expect(screen.getByText(/plan bazowy v2/)).toBeInTheDocument();
  });
});
