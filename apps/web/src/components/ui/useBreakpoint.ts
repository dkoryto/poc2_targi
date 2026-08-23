import { useEffect, useState } from 'react';

/**
 * Breakpoints from docs/architecture/responsive.md. Kept in sync with the literal
 * values in the CSS modules (custom properties cannot be used inside @media).
 */
export const BP = { sm: 480, md: 768, lg: 1200, xl: 1600 } as const;

/** Subscribe to a media query. SSR/jsdom-safe: returns `false` when matchMedia is missing. */
export function useMediaQuery(query: string): boolean {
  const [matches, setMatches] = useState(() => {
    if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') return false;
    return window.matchMedia(query).matches;
  });

  useEffect(() => {
    if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') return;
    const mql = window.matchMedia(query);
    const onChange = () => setMatches(mql.matches);
    onChange();
    // Safari < 14 only has addListener; both are guarded because jsdom stubs vary.
    if (typeof mql.addEventListener === 'function') {
      mql.addEventListener('change', onChange);
      return () => mql.removeEventListener('change', onChange);
    }
    if (typeof mql.addListener === 'function') {
      mql.addListener(onChange);
      return () => mql.removeListener(onChange);
    }
    return;
  }, [query]);

  return matches;
}

/** True below the `md` breakpoint — the single-column / drawer / card-list layout. */
export function useIsMobile(): boolean {
  return useMediaQuery(`(max-width: ${BP.md - 1}px)`);
}

/** True below the `lg` breakpoint — tablet and phone; the icon rail is the default here. */
export function useIsCompact(): boolean {
  return useMediaQuery(`(max-width: ${BP.lg - 1}px)`);
}
