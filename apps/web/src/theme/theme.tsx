import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Monitor, Moon, Sun } from 'lucide-react';
import { SegmentedControl } from '@/components/ui';

/** User preference. `auto` follows the OS `prefers-color-scheme`. */
export type ThemePreference = 'auto' | 'light' | 'dark';
/** What is actually painted. */
export type ResolvedTheme = 'light' | 'dark';

export const THEME_STORAGE_KEY = 'dspc.theme';
/** Fired on `document` whenever the resolved theme changes (non-CSS consumers: MapLibre/WebGL). */
export const THEME_EVENT = 'dspc:themechange';

export function readStoredPreference(): ThemePreference {
  try {
    const v = localStorage.getItem(THEME_STORAGE_KEY);
    if (v === 'light' || v === 'dark' || v === 'auto') return v;
  } catch {
    /* private mode / storage disabled */
  }
  return 'auto';
}

function storePreference(p: ThemePreference): void {
  try {
    localStorage.setItem(THEME_STORAGE_KEY, p);
  } catch {
    /* ignore */
  }
}

function systemTheme(): ResolvedTheme {
  try {
    return window.matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark';
  } catch {
    return 'dark';
  }
}

export function resolveTheme(pref: ThemePreference): ResolvedTheme {
  return pref === 'auto' ? systemTheme() : pref;
}

/**
 * Reads a CSS custom property from :root. WebGL/canvas cannot resolve `var()`,
 * so the map reads concrete values here and re-reads on THEME_EVENT.
 */
export function readThemeColor(token: string, fallback = '#000000'): string {
  if (typeof document === 'undefined') return fallback;
  const name = token.startsWith('--') ? token : `--${token}`;
  const v = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
  return v || fallback;
}

interface ThemeCtx {
  preference: ThemePreference;
  theme: ResolvedTheme;
  setPreference: (p: ThemePreference) => void;
}

const Ctx = createContext<ThemeCtx>({ preference: 'auto', theme: 'dark', setPreference: () => {} });

export function useTheme(): ThemeCtx {
  return useContext(Ctx);
}

function applyTheme(theme: ResolvedTheme): void {
  const root = document.documentElement;
  if (root.dataset.theme === theme) return;
  root.dataset.theme = theme;
  document.dispatchEvent(new CustomEvent(THEME_EVENT, { detail: { theme } }));
}

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [preference, setPreferenceState] = useState<ThemePreference>(() => readStoredPreference());
  const [theme, setTheme] = useState<ResolvedTheme>(() => resolveTheme(readStoredPreference()));

  // Apply to <html> and notify non-CSS consumers.
  useEffect(() => {
    applyTheme(theme);
  }, [theme]);

  // Follow the OS while the preference is `auto`.
  useEffect(() => {
    if (preference !== 'auto') return;
    let mq: MediaQueryList;
    try {
      mq = window.matchMedia('(prefers-color-scheme: light)');
    } catch {
      return;
    }
    const onChange = () => setTheme(systemTheme());
    onChange();
    mq.addEventListener('change', onChange);
    return () => mq.removeEventListener('change', onChange);
  }, [preference]);

  const setPreference = useCallback((p: ThemePreference) => {
    setPreferenceState(p);
    storePreference(p);
    setTheme(resolveTheme(p));
  }, []);

  const value = useMemo(() => ({ preference, theme, setPreference }), [preference, theme, setPreference]);
  return <Ctx.Provider value={value}>{children}</Ctx.Provider>;
}

/** Auto / Light / Dark switch for the top bar. */
export function ThemeSwitch() {
  const { t } = useTranslation();
  const { preference, setPreference } = useTheme();
  return (
    <SegmentedControl
      label={t('topbar.theme')}
      value={preference}
      onChange={(v) => setPreference(v as ThemePreference)}
      data-testid="theme-switch"
      options={[
        { value: 'auto', label: <Monitor size={13} aria-hidden />, title: t('topbar.themeAuto') },
        { value: 'light', label: <Sun size={13} aria-hidden />, title: t('topbar.themeLight') },
        { value: 'dark', label: <Moon size={13} aria-hidden />, title: t('topbar.themeDark') },
      ]}
    />
  );
}
