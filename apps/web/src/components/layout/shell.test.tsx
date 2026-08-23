import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { Routes, Route } from 'react-router';
import { AppShell } from './AppShell';
import { renderWithProviders } from '@/test/utils';

function Shell() {
  return (
    <Routes>
      <Route element={<AppShell />}>
        <Route index element={<div>home</div>} />
        <Route path="/supply" element={<div>supply</div>} />
        <Route path="/supply/orders/:code" element={<div>po</div>} />
        <Route path="*" element={<div>other</div>} />
      </Route>
    </Routes>
  );
}

describe('AppShell', () => {
  it('auto-logs in as DemoPresenter and shows full nav; switching to supplier hides control-room nav', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Shell />, { auth: true });
    await waitFor(() => expect(screen.getByTestId('current-role')).toHaveTextContent('Prezenter demo'));
    expect(screen.getByTestId('nav-controlRoom')).toBeInTheDocument();
    expect(screen.getByTestId('nav-admin')).toBeInTheDocument();
    await user.click(screen.getByTestId('user-menu'));
    await user.click(screen.getByTestId('switch-role-SupplierUser-SUP-02'));
    await waitFor(() => expect(screen.getByTestId('current-role')).toHaveTextContent('Dostawca'));
    expect(screen.queryByTestId('nav-controlRoom')).not.toBeInTheDocument();
    expect(screen.queryByTestId('nav-admin')).not.toBeInTheDocument();
    expect(screen.getByTestId('nav-supply')).toBeInTheDocument();
  });
  it('presenter panel lists steps and next navigates without performing actions', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Shell />, { auth: true });
    await waitFor(() => expect(screen.getByTestId('run-demo')).toBeInTheDocument());
    await user.click(screen.getByTestId('run-demo'));
    await waitFor(() => expect(screen.getByTestId('presenter-step-1')).toHaveAttribute('aria-current', 'step'));
    await user.click(screen.getByTestId('presenter-next'));
    expect(screen.getByTestId('presenter-step-2')).toHaveAttribute('aria-current', 'step');
    await waitFor(() => expect(screen.getByText('po')).toBeInTheDocument());
    expect(screen.getByTestId('presenter-prev')).toBeEnabled();
  });
  it('switches language PL → EN', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Shell />, { auth: true });
    await waitFor(() => expect(screen.getByTestId('current-role')).toBeInTheDocument());
    await user.click(screen.getByRole('button', { name: 'EN' }));
    await waitFor(() => expect(screen.getByTestId('current-role')).toHaveTextContent('Demo presenter'));
  });
});
