import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it } from 'vitest';
import { Route, Routes } from 'react-router';
import { SupplyListPage } from './supply/SupplyListPage';
import { PlanningPage } from './planning/PlanningPage';
import { AuditPage } from './audit/AuditPage';
import { ScenarioDetailPage } from './planning/ScenarioDetailPage';
import { renderWithProviders } from '@/test/utils';
import { DESKTOP_WIDTH, MOBILE_WIDTH, setViewportWidth } from '@/test/viewport';

afterEach(() => setViewportWidth(DESKTOP_WIDTH));

describe('feature screens on a phone', () => {
  it('renders the purchase-order list as cards, not a clipped table', async () => {
    setViewportWidth(MOBILE_WIDTH);
    renderWithProviders(<SupplyListPage />, { route: '/supply', auth: true });

    const list = await screen.findByTestId('po-table');
    // The card list replaces the table wholesale — no <table> can push the page sideways.
    expect(list.querySelector('table')).toBeNull();
    await waitFor(() => expect(within(list).getByText('PO-2026-0007')).toBeInTheDocument());
    // Columns that used to fall off the right edge are now labelled rows inside the card.
    expect(within(list).getAllByText(/Ryzyko|Risk/i).length).toBeGreaterThan(0);
    // Sorting stays reachable without a table header.
    expect(screen.getByTestId('card-sort')).toBeInTheDocument();
  });

  it('keeps the purchase-order filters behind a toggle with an active count', async () => {
    setViewportWidth(MOBILE_WIDTH);
    const user = userEvent.setup();
    renderWithProviders(<SupplyListPage />, { route: '/supply?status=Shipped&q=ACT', auth: true });

    const toggle = screen.getByTestId('filter-toggle');
    expect(screen.queryByTestId('filter-panel')).toBeNull();
    expect(toggle).toHaveTextContent('2'); // status + q
    await user.click(toggle);
    expect(await screen.findByTestId('filter-panel')).toBeInTheDocument();
  });

  it('renders audit rows as expandable cards with a stacked diff', async () => {
    setViewportWidth(MOBILE_WIDTH);
    const user = userEvent.setup();
    renderWithProviders(<AuditPage />, { route: '/audit', auth: true });

    const list = await screen.findByTestId('audit-table');
    expect(list.querySelector('table')).toBeNull();
    await user.click(screen.getByTestId('audit-row-a-1'));

    const diff = await screen.findByTestId('json-diff');
    // Stacked layout: field blocks rather than three columns.
    expect(diff.querySelector('table')).toBeNull();
    expect(diff.querySelectorAll('[data-changed="true"]').length).toBeGreaterThanOrEqual(2);
    expect(screen.getByTestId('audit-detail')).toHaveTextContent('c0ffee01');
  });

  it('moves the plan actions into a sticky bar on a phone', async () => {
    setViewportWidth(MOBILE_WIDTH);
    const user = userEvent.setup();
    renderWithProviders(
      <Routes>
        <Route path="/planning" element={<PlanningPage />} />
        <Route path="/planning/scenarios/:id" element={<ScenarioDetailPage />} />
      </Routes>,
      { route: '/planning', auth: true },
    );
    const tile = await screen.findByTestId('scenario-tile-DELAY_ACT40_10D');
    await waitFor(() => expect(tile).toBeEnabled());
    await user.click(tile);
    await waitFor(() => expect(screen.getByTestId('scenario-status')).toHaveTextContent('Completed'), { timeout: 5000 });

    const bar = await screen.findByTestId('scenario-actions');
    expect(within(bar).getByTestId('btn-approve-plan')).toBeInTheDocument();
    // Exactly one action group — the header copy must not also render.
    expect(screen.getAllByTestId('scenario-actions')).toHaveLength(1);
  });
});
