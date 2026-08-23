import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it } from 'vitest';
import { Routes, Route } from 'react-router';
import { AppShell } from './AppShell';
import { renderWithProviders } from '@/test/utils';
import { DESKTOP_WIDTH, MOBILE_WIDTH, setViewportWidth } from '@/test/viewport';

function Shell() {
  return (
    <Routes>
      <Route element={<AppShell />}>
        <Route index element={<div>home</div>} />
        <Route path="/supply" element={<div>supply</div>} />
        <Route path="*" element={<div>other</div>} />
      </Route>
    </Routes>
  );
}

afterEach(() => setViewportWidth(DESKTOP_WIDTH));

describe('AppShell on a phone', () => {
  it('hides the nav until the hamburger is pressed, then closes it after picking a route', async () => {
    setViewportWidth(MOBILE_WIDTH);
    const user = userEvent.setup();
    renderWithProviders(<Shell />, { auth: true });
    await waitFor(() => expect(screen.getByTestId('nav-toggle')).toBeInTheDocument());

    // No nav column on a phone — it is an overlay drawer.
    expect(screen.queryByTestId('main-nav')).not.toBeInTheDocument();
    expect(screen.queryByTestId('nav-drawer')).not.toBeInTheDocument();

    await user.click(screen.getByTestId('nav-toggle'));
    const drawer = await screen.findByTestId('nav-drawer');
    expect(drawer).toBeInTheDocument();
    expect(screen.getByTestId('nav-toggle')).toHaveAttribute('aria-expanded', 'true');

    await user.click(screen.getByTestId('nav-supply'));
    await waitFor(() => expect(screen.queryByTestId('nav-drawer')).not.toBeInTheDocument());
    expect(screen.getByText('supply')).toBeInTheDocument();
  });

  it('traps focus inside the drawer and closes it on Escape', async () => {
    setViewportWidth(MOBILE_WIDTH);
    const user = userEvent.setup();
    renderWithProviders(<Shell />, { auth: true });
    await waitFor(() => expect(screen.getByTestId('nav-toggle')).toBeInTheDocument());
    await user.click(screen.getByTestId('nav-toggle'));

    const drawer = await screen.findByTestId('nav-drawer');
    const focusables = Array.from(drawer.querySelectorAll<HTMLElement>('a[href], button'));
    expect(focusables.length).toBeGreaterThan(1);
    await waitFor(() => expect(drawer.contains(document.activeElement)).toBe(true));

    focusables[focusables.length - 1]!.focus();
    await user.tab();
    expect(document.activeElement).toBe(focusables[0]);

    await user.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByTestId('nav-drawer')).not.toBeInTheDocument());
  });

  it('moves the controls that do not fit into the ⋯ menu instead of dropping them', async () => {
    setViewportWidth(MOBILE_WIDTH);
    const user = userEvent.setup();
    renderWithProviders(<Shell />, { auth: true });
    await waitFor(() => expect(screen.getByTestId('overflow-menu')).toBeInTheDocument());

    // Not on the bar itself…
    expect(screen.queryByTestId('lang-switch')).not.toBeInTheDocument();
    expect(screen.queryByTestId('run-demo')).not.toBeInTheDocument();

    // …but every one of them is reachable behind the menu (contract rule 2).
    await user.click(screen.getByTestId('overflow-menu'));
    expect(screen.getByTestId('lang-switch')).toBeInTheDocument();
    expect(screen.getByTestId('theme-switch')).toBeInTheDocument();
    expect(screen.getByTestId('run-demo')).toBeInTheDocument();
    expect(screen.getByTestId('reset-demo')).toBeInTheDocument();
    expect(screen.getByTestId('notifications-button')).toBeInTheDocument();
    // the user/role menu stays on the bar
    expect(screen.getByTestId('user-menu')).toBeInTheDocument();
  });

  it('keeps the full nav column and inline controls on the trade-show monitor', async () => {
    setViewportWidth(DESKTOP_WIDTH);
    renderWithProviders(<Shell />, { auth: true });
    await waitFor(() => expect(screen.getByTestId('main-nav')).toBeInTheDocument());
    expect(screen.queryByTestId('nav-drawer')).not.toBeInTheDocument();
    expect(screen.getByTestId('lang-switch')).toBeInTheDocument();
    expect(screen.queryByTestId('overflow-menu')).not.toBeInTheDocument();
  });
});
