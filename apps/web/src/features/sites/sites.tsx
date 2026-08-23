import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api } from '@/api/client';
import { keys } from '@/api/keys';
import { useOptionalAuth } from '@/features/auth/auth';
import type { Site } from '@/api/types';

const STORAGE_KEY = 'dspc.site';

function readStored(): string | null {
  try {
    return localStorage.getItem(STORAGE_KEY);
  } catch {
    return null;
  }
}
function writeStored(code: string): void {
  try {
    localStorage.setItem(STORAGE_KEY, code);
  } catch {
    /* storage unavailable */
  }
}

interface SiteState {
  sites: Site[];
  /** Plants the current user may switch to (already filtered by `availableSites`). */
  activeSite: Site | null;
  activeSiteCode: string;
  setActiveSite: (code: string) => void;
  /** False when the API exposes no `/sites`: nothing is scoped and no parameter is sent. */
  scoped: boolean;
  /**
   * True once `/sites` has settled — success *or* failure. Data hooks wait for it so they
   * never fire for the wrong plant, but a backend without `/sites` still unblocks the app:
   * the code stays empty, `?siteCode=` is omitted and the API answers for the user's own plant.
   */
  ready: boolean;
  isLoading: boolean;
}

const Ctx = createContext<SiteState | null>(null);

export function useSites() {
  const user = useOptionalAuth()?.user ?? null;
  return useQuery({
    queryKey: keys.sites,
    queryFn: () => api.get<Site[]>('/sites'),
    enabled: !!user,
    staleTime: 5 * 60_000,
    retry: false,
  });
}

/**
 * Resolution order for the active plant: a persisted choice, then the user's own
 * `siteCode`, then the plant flagged `isDefault`, then the first one. A stored code the
 * user may no longer access (role switch to a supplier) is discarded rather than sent.
 */
export function resolveActiveSite(sites: Site[], stored: string | null, userSiteCode: string | undefined, available: string[] | undefined): string {
  if (sites.length === 0) return '';
  const allowed = sites.filter((s) => !available || available.length === 0 || available.includes(s.code));
  const pool = allowed.length > 0 ? allowed : sites;
  const pick = (code: string | null | undefined) => (code ? pool.find((s) => s.code === code) : undefined);
  return (pick(stored) ?? pick(userSiteCode) ?? pool.find((s) => s.isDefault) ?? pool[0]!).code;
}

export function SiteProvider({ children }: { children: ReactNode }) {
  const auth = useOptionalAuth();
  const user = auth?.user ?? null;
  // Without an AuthProvider (isolated component rendering) there is nothing to wait for.
  const authReady = auth ? auth.ready : true;
  const query = useSites();
  const [selected, setSelected] = useState<string | null>(readStored);

  /**
   * An API without `/sites` (single-plant build) still gets a named plant in the top bar,
   * synthesised from the user's own `siteCode`; `?siteCode=` is then omitted entirely.
   */
  const all = useMemo<Site[]>(() => {
    if (query.data && query.data.length > 0) return query.data;
    if (user?.siteCode) {
      return [{ code: user.siteCode, name: user.siteCode, city: '', country: '', lat: 0, lon: 0, timeZone: 'Europe/Warsaw', isDefault: true }];
    }
    return [];
  }, [query.data, user?.siteCode]);
  const available = user?.availableSites;
  const sites = useMemo(
    () => (available && available.length > 0 ? all.filter((s) => available.includes(s.code)) : all),
    [all, available],
  );

  const scoped = (query.data?.length ?? 0) > 0;

  const activeSiteCode = useMemo(
    () => resolveActiveSite(all, selected, user?.siteCode, available),
    [all, selected, user?.siteCode, available],
  );

  // A role switch can revoke access to the stored plant; fall back without writing a bad value.
  // Only correct the selection once the real list is in — during the single-plant fallback the
  // stored choice must survive untouched, or reloading would silently drop it.
  useEffect(() => {
    if (!scoped) return;
    if (activeSiteCode && activeSiteCode !== selected) setSelected(activeSiteCode);
  }, [scoped, activeSiteCode, selected]);

  const setActiveSite = useCallback((code: string) => {
    setSelected(code);
    writeStored(code);
  }, []);

  const activeSite = useMemo(() => all.find((s) => s.code === activeSiteCode) ?? null, [all, activeSiteCode]);
  // Hooks stay idle until both auth and `/sites` have settled, so each one fires exactly
  // once with the right plant in its key instead of re-keying a request already in flight.
  const ready = authReady && !query.isLoading;

  const value = useMemo<SiteState>(
    () => ({ sites, activeSite, activeSiteCode, setActiveSite, ready, scoped, isLoading: query.isLoading }),
    [sites, activeSite, activeSiteCode, setActiveSite, ready, scoped, query.isLoading],
  );
  return <Ctx.Provider value={value}>{children}</Ctx.Provider>;
}

export function useSite(): SiteState {
  const ctx = useContext(Ctx);
  if (!ctx) throw new Error('SiteProvider missing');
  return ctx;
}

/**
 * The active plant code for query keys and `?siteCode=`. Empty until `/sites` resolves
 * (or when the API has no `/sites`), in which case the parameter is simply omitted.
 */
export function useSiteCode(): string {
  return useContext(Ctx)?.activeSiteCode ?? '';
}

/** The plant code to actually send, or '' when the API is not plant-scoped. */
export function useScopedSiteCode(): string {
  const ctx = useContext(Ctx);
  return ctx?.scoped ? ctx.activeSiteCode : '';
}

/** Query gate: keeps a hook idle until the active plant is known. */
export function useSiteReady(): boolean {
  return useContext(Ctx)?.ready ?? true;
}

/** `{ siteCode }` for request params — omitted while unknown or when the API has no `/sites`. */
export function useSiteParam(): { siteCode?: string } {
  const ctx = useContext(Ctx);
  return ctx?.scoped && ctx.activeSiteCode ? { siteCode: ctx.activeSiteCode } : {};
}

/** Human label for a plant code, for cross-plant records. */
export function useSiteLabel(): (code: string | null | undefined) => string {
  const { sites } = useSite();
  return useCallback((code) => (code ? (sites.find((s) => s.code === code)?.name ?? code) : ''), [sites]);
}
