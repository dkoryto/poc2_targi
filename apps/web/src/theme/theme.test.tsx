import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ThemeProvider, ThemeSwitch, THEME_STORAGE_KEY, resolveTheme, readStoredPreference } from './theme';
import '@/i18n';

/** jsdom has no matchMedia; emulate the OS preference. */
function mockSystem(prefersLight: boolean) {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    configurable: true,
    value: vi.fn().mockImplementation((query: string) => ({
      matches: query.includes('prefers-color-scheme: light') ? prefersLight : false,
      media: query,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      addListener: vi.fn(),
      removeListener: vi.fn(),
      dispatchEvent: vi.fn(),
      onchange: null,
    })),
  });
}

function renderSwitch() {
  return render(
    <ThemeProvider>
      <ThemeSwitch />
    </ThemeProvider>,
  );
}

describe('theme', () => {
  beforeEach(() => {
    localStorage.clear();
    delete document.documentElement.dataset.theme;
    mockSystem(false);
  });

  it('defaults to auto and resolves from the OS preference', async () => {
    mockSystem(true);
    renderSwitch();
    await waitFor(() => expect(document.documentElement.dataset.theme).toBe('light'));
    expect(screen.getByTestId('theme-switch-auto')).toHaveAttribute('aria-pressed', 'true');
  });

  it('switches to light and dark, stamping data-theme on <html>', async () => {
    const user = userEvent.setup();
    renderSwitch();
    await waitFor(() => expect(document.documentElement.dataset.theme).toBe('dark'));

    await user.click(screen.getByTestId('theme-switch-light'));
    await waitFor(() => expect(document.documentElement.dataset.theme).toBe('light'));
    expect(screen.getByTestId('theme-switch-light')).toHaveAttribute('aria-pressed', 'true');

    await user.click(screen.getByTestId('theme-switch-dark'));
    await waitFor(() => expect(document.documentElement.dataset.theme).toBe('dark'));
  });

  it('persists the preference and restores it on the next mount', async () => {
    const user = userEvent.setup();
    const { unmount } = renderSwitch();
    await user.click(screen.getByTestId('theme-switch-light'));
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('light');
    unmount();

    renderSwitch();
    await waitFor(() => expect(document.documentElement.dataset.theme).toBe('light'));
    expect(screen.getByTestId('theme-switch-light')).toHaveAttribute('aria-pressed', 'true');
  });

  it('survives storage being unavailable', () => {
    const spy = vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('denied');
    });
    expect(readStoredPreference()).toBe('auto');
    spy.mockRestore();
  });

  it('resolveTheme honours explicit preferences over the system', () => {
    mockSystem(true);
    expect(resolveTheme('dark')).toBe('dark');
    expect(resolveTheme('light')).toBe('light');
    expect(resolveTheme('auto')).toBe('light');
  });
});
