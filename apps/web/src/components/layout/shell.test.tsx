import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
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

  it('collapses and expands the side nav, persisting the choice', async () => {
    const user = userEvent.setup();
    localStorage.removeItem('dspc.nav.collapsed');
    // jsdom defaults to 1024px, which is below the rail breakpoint; emulate the 1920×1080 stand.
    Object.defineProperty(window, 'innerWidth', { value: 1920, writable: true, configurable: true });
    const { unmount } = renderWithProviders(<Shell />, { auth: true });
    await waitFor(() => expect(screen.getByTestId('main-nav')).toBeInTheDocument());

    const nav = screen.getByTestId('main-nav');
    const toggle = screen.getByTestId('nav-toggle');
    expect(nav).toHaveAttribute('data-collapsed', 'false');
    expect(toggle).toHaveAttribute('aria-expanded', 'true');
    expect(toggle).toHaveAttribute('aria-controls', 'main-nav');
    expect(nav).toHaveAttribute('id', 'main-nav');

    await user.click(toggle);
    expect(screen.getByTestId('main-nav')).toHaveAttribute('data-collapsed', 'true');
    expect(screen.getByTestId('nav-toggle')).toHaveAttribute('aria-expanded', 'false');
    expect(localStorage.getItem('dspc.nav.collapsed')).toBe('1');
    // links stay reachable by their accessible name while collapsed
    expect(screen.getByTestId('nav-planning')).toBeInTheDocument();

    unmount();
    renderWithProviders(<Shell />, { auth: true });
    await waitFor(() => expect(screen.getByTestId('main-nav')).toHaveAttribute('data-collapsed', 'true'));
    localStorage.removeItem('dspc.nav.collapsed');
  });

  it('defaults to the icon rail on narrow screens', async () => {
    localStorage.removeItem('dspc.nav.collapsed');
    Object.defineProperty(window, 'innerWidth', { value: 1100, writable: true, configurable: true });
    renderWithProviders(<Shell />, { auth: true });
    await waitFor(() => expect(screen.getByTestId('main-nav')).toHaveAttribute('data-collapsed', 'true'));
    Object.defineProperty(window, 'innerWidth', { value: 1920, writable: true, configurable: true });
  });

  it('renders the error boundary instead of a blank page when a route throws', async () => {
    const spy = vi.spyOn(console, 'error').mockImplementation(() => {});
    function Broken(): never {
      throw new TypeError('riskWeights.map is not a function');
    }
    renderWithProviders(
      <Routes>
        <Route element={<AppShell />}>
          <Route index element={<Broken />} />
        </Route>
      </Routes>,
      { auth: true },
    );
    await waitFor(() => expect(screen.getByTestId('error-boundary')).toBeInTheDocument());
    // the shell itself survives
    expect(screen.getByTestId('main-nav')).toBeInTheDocument();
    spy.mockRestore();
  });
});
