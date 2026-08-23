import { vi } from 'vitest';

/**
 * jsdom has no layout engine, so `matchMedia` is stubbed globally to never match.
 * These helpers evaluate `(max-width: N px)` / `(min-width: N px)` against a pretend
 * viewport width so the breakpoint-driven components can be tested.
 */
export function setViewportWidth(width: number): void {
  const listeners = new Set<() => void>();
  Object.defineProperty(window, 'innerWidth', { value: width, writable: true, configurable: true });
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    configurable: true,
    value: (query: string) => {
      const max = /max-width:\s*(\d+)px/.exec(query);
      const min = /min-width:\s*(\d+)px/.exec(query);
      let matches = true;
      if (max) matches &&= width <= Number(max[1]);
      if (min) matches &&= width >= Number(min[1]);
      if (!max && !min) matches = false;
      return {
        matches,
        media: query,
        onchange: null,
        addListener: (cb: () => void) => listeners.add(cb),
        removeListener: (cb: () => void) => listeners.delete(cb),
        addEventListener: (_: string, cb: () => void) => listeners.add(cb),
        removeEventListener: (_: string, cb: () => void) => listeners.delete(cb),
        dispatchEvent: vi.fn(),
      };
    },
  });
}

/** Phone width used across the responsive tests (below the `md` breakpoint). */
export const MOBILE_WIDTH = 390;
/** Trade-show monitor width. */
export const DESKTOP_WIDTH = 1920;
