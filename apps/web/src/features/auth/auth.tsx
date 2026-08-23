import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { api, getToken, onUnauthorized, setToken, ApiError } from '@/api/client';
import type { DemoStatus, LoginResponse, Role, UserContext } from '@/api/types';

interface AuthState {
  user: UserContext | null;
  demoMode: boolean;
  ready: boolean;
  login: (username: string, password: string) => Promise<void>;
  demoLogin: (role: Role, supplierCode?: string) => Promise<void>;
  logout: () => void;
}

const Ctx = createContext<AuthState | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const qc = useQueryClient();
  const [user, setUser] = useState<UserContext | null>(null);
  const [demoMode, setDemoMode] = useState(false);
  const [ready, setReady] = useState(false);

  const applyLogin = useCallback(
    (res: LoginResponse) => {
      setToken(res.accessToken);
      setUser(res.user);
      setDemoMode(res.user.demoMode);
      void qc.invalidateQueries();
    },
    [qc],
  );

  const login = useCallback(
    async (username: string, password: string) => {
      const res = await api.post<LoginResponse>('/auth/login', { username, password }, { idempotent: false });
      applyLogin(res);
    },
    [applyLogin],
  );

  const demoLogin = useCallback(
    async (role: Role, supplierCode?: string) => {
      const res = await api.get<LoginResponse>('/auth/demo-login', { role, supplierCode });
      applyLogin(res);
    },
    [applyLogin],
  );

  const logout = useCallback(() => {
    setToken(null);
    setUser(null);
    qc.clear();
  }, [qc]);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      let demo = false;
      try {
        const st = await api.get<DemoStatus>('/demo/status');
        demo = st.demoMode;
      } catch {
        demo = false;
      }
      if (cancelled) return;
      setDemoMode(demo);
      if (getToken()) {
        try {
          const me = await api.get<UserContext>('/auth/me');
          if (!cancelled) {
            setUser(me);
            setDemoMode(me.demoMode);
            setReady(true);
            return;
          }
        } catch {
          setToken(null);
        }
      }
      if (demo) {
        try {
          const res = await api.get<LoginResponse>('/auth/demo-login', { role: 'DemoPresenter' });
          if (!cancelled) applyLogin(res);
        } catch {
          /* fall through to login page */
        }
      }
      if (!cancelled) setReady(true);
    })();
    return () => {
      cancelled = true;
    };
  }, [applyLogin]);

  useEffect(() => onUnauthorized(() => logout()), [logout]);

  const value = useMemo<AuthState>(() => ({ user, demoMode, ready, login, demoLogin, logout }), [user, demoMode, ready, login, demoLogin, logout]);
  return <Ctx.Provider value={value}>{children}</Ctx.Provider>;
}

export function useAuth(): AuthState {
  const ctx = useContext(Ctx);
  if (!ctx) throw new Error('AuthProvider missing');
  return ctx;
}

export function isApiError(e: unknown, status?: number): e is ApiError {
  return e instanceof ApiError && (status === undefined || e.status === status);
}

/** Navigation / route access matrix mirroring spec §6. */
export const ROUTE_ROLES: Record<string, Role[] | 'all'> = {
  '/': ['OperationsDirector', 'ProductionPlanner', 'InboundCoordinator', 'QualityInspector', 'Auditor', 'Administrator', 'DemoPresenter'],
  '/supply': 'all',
  '/inbound': ['InboundCoordinator', 'ProductionPlanner', 'OperationsDirector', 'Administrator', 'DemoPresenter', 'Auditor'],
  '/planning': ['ProductionPlanner', 'OperationsDirector', 'Administrator', 'DemoPresenter', 'Auditor'],
  '/trace': ['QualityInspector', 'ProductionPlanner', 'OperationsDirector', 'Auditor', 'Administrator', 'DemoPresenter', 'InboundCoordinator'],
  '/passports': ['QualityInspector', 'OperationsDirector', 'Auditor', 'Administrator', 'DemoPresenter'],
  '/audit': ['Auditor', 'Administrator', 'OperationsDirector', 'DemoPresenter'],
  '/admin': ['Administrator', 'DemoPresenter'],
  '/notifications': 'all',
};

export function canAccess(role: Role | undefined, route: string): boolean {
  if (!role) return false;
  const allowed = ROUTE_ROLES[route];
  if (!allowed) return true;
  return allowed === 'all' || allowed.includes(role);
}

export function homeFor(role: Role): string {
  return role === 'SupplierUser' ? '/supply' : '/';
}
