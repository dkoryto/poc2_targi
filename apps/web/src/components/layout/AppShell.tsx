import { createContext, useCallback, useContext, useEffect, useState } from 'react';
import { Outlet, useLocation, useNavigate } from 'react-router';
import s from './layout.module.css';
import { TopBar } from './TopBar';
import { Nav } from './Nav';
import { PresenterPanel } from './PresenterPanel';
import { DisclaimerBanner, ConfirmDialog, ErrorBoundary, useToast } from '@/components/ui';
import { useLive, type LiveStatus } from '@/realtime/useLive';

const NAV_STORAGE_KEY = 'dspc.nav.collapsed';
/** Below this width the icon rail is the default (the 2×2 dashboard needs the room). */
const RAIL_BREAKPOINT = 1200;

function readNavCollapsed(): boolean {
  try {
    const v = localStorage.getItem(NAV_STORAGE_KEY);
    if (v === '1') return true;
    if (v === '0') return false;
  } catch {
    /* ignore */
  }
  return typeof window !== 'undefined' && window.innerWidth < RAIL_BREAKPOINT;
}

const LiveCtx = createContext<LiveStatus>('disconnected');
/** SignalR connection state, available anywhere under AppShell. */
export function useLiveStatus(): LiveStatus {
  return useContext(LiveCtx);
}
import { useAuth } from '@/features/auth/auth';
import { useResetDemo } from '@/features/demo/api';
import { useTranslation } from 'react-i18next';

export function AppShell() {
  const { user } = useAuth();
  const live = useLive(!!user);
  const [presenter, setPresenter] = useState(false);
  const [resetOpen, setResetOpen] = useState(false);
  const reset = useResetDemo();
  const toast = useToast();
  const { t } = useTranslation();
  const navigate = useNavigate();
  const location = useLocation();
  const [navCollapsed, setNavCollapsed] = useState(readNavCollapsed);

  const toggleNav = useCallback(() => {
    setNavCollapsed((c) => {
      const next = !c;
      try {
        localStorage.setItem(NAV_STORAGE_KEY, next ? '1' : '0');
      } catch {
        /* ignore */
      }
      return next;
    });
  }, []);

  // The main area changes width; anything measuring itself (map, Gantt) listens for resize.
  useEffect(() => {
    const id = window.setTimeout(() => window.dispatchEvent(new Event('resize')), 220);
    return () => window.clearTimeout(id);
  }, [navCollapsed]);

  return (
    <LiveCtx.Provider value={live}>
    <div className={[s.shell, navCollapsed && s.shellRail].filter(Boolean).join(' ')}>
      <TopBar live={live} onOpenPresenter={() => setPresenter(true)} navCollapsed={navCollapsed} onToggleNav={toggleNav} />
      <Nav collapsed={navCollapsed} />
      <main className={s.main} id="main">
        <ErrorBoundary resetKey={location.pathname}>
          <Outlet />
        </ErrorBoundary>
      </main>
      <div className={s.footer}>
        <DisclaimerBanner />
      </div>
      <PresenterPanel open={presenter} onClose={() => setPresenter(false)} onReset={() => setResetOpen(true)} />
      <ConfirmDialog
        open={resetOpen}
        onClose={() => setResetOpen(false)}
        title={t('demo.resetTitle')}
        impact={t('demo.resetImpact')}
        danger
        confirmLabel={t('topbar.resetDemo')}
        loading={reset.isPending}
        onConfirm={() =>
          reset.mutate(undefined, {
            onSuccess: (r) => {
              setResetOpen(false);
              toast.ok(t('demo.resetDone', { ms: r.durationMs }));
              navigate('/');
            },
            onError: () => toast.critical(t('common.error')),
          })
        }
      />
    </div>
    </LiveCtx.Provider>
  );
}
