import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate } from 'react-router';
import { Bell, Play, RotateCcw, ShieldCheck, ChevronDown, LogOut, UserCog, Wifi, WifiOff, PanelLeftClose, PanelLeftOpen } from 'lucide-react';
import s from './layout.module.css';
import { useAuth } from '@/features/auth/auth';
import { useDemoAccounts, useHealth, useResetDemo, useDemoStatus } from '@/features/demo/api';
import { useNotifications } from '@/features/notifications/api';
import { Badge, Button, ConfirmDialog, IconButton, SegmentedControl, useToast } from '@/components/ui';
import { setLocale, currentLocale } from '@/i18n';
import { ThemeSwitch } from '@/theme/theme';
import { fmtClock } from '@/lib/format';
import type { LiveStatus } from '@/realtime/useLive';
import { ALL_ROLES, type Role } from '@/api/types';

function Clock() {
  const [now, setNow] = useState(() => new Date());
  useEffect(() => {
    const id = window.setInterval(() => setNow(new Date()), 1000);
    return () => window.clearInterval(id);
  }, []);
  return (
    <time className={s.clock} dateTime={now.toISOString()} data-testid="clock">
      {fmtClock(now)}
    </time>
  );
}

export function TopBar({ live, onOpenPresenter, navCollapsed, onToggleNav }: { live: LiveStatus; onOpenPresenter: () => void; navCollapsed: boolean; onToggleNav: () => void }) {
  const { t } = useTranslation();
  const { user, demoMode, demoLogin, logout } = useAuth();
  const navigate = useNavigate();
  const toast = useToast();
  const health = useHealth();
  const demoStatus = useDemoStatus();
  const accounts = useDemoAccounts(demoMode);
  const notifications = useNotifications(!!user);
  const reset = useResetDemo();
  const [menuOpen, setMenuOpen] = useState(false);
  const [resetOpen, setResetOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!menuOpen) return;
    const onDoc = (e: MouseEvent) => {
      if (!menuRef.current?.contains(e.target as Node)) setMenuOpen(false);
    };
    document.addEventListener('mousedown', onDoc);
    return () => document.removeEventListener('mousedown', onDoc);
  }, [menuOpen]);

  const online = health.data === true;
  const unread = notifications.data?.items.filter((n) => !n.read).length ?? 0;
  const locale = currentLocale();
  const canReset = user?.role === 'DemoPresenter' || user?.role === 'Administrator';

  const switchRole = async (role: Role, supplierCode?: string) => {
    setMenuOpen(false);
    try {
      await demoLogin(role, supplierCode);
      navigate(role === 'SupplierUser' ? '/supply' : '/');
    } catch {
      toast.critical(t('common.error'));
    }
  };

  const supplierAccounts = accounts.data?.filter((a) => a.role === 'SupplierUser') ?? [];

  return (
    <header className={s.topbar}>
      <button
        type="button"
        className={s.navToggle}
        onClick={onToggleNav}
        aria-expanded={!navCollapsed}
        aria-controls="main-nav"
        aria-label={navCollapsed ? t('topbar.expandNav') : t('topbar.collapseNav')}
        title={navCollapsed ? t('topbar.expandNav') : t('topbar.collapseNav')}
        data-testid="nav-toggle"
      >
        {navCollapsed ? <PanelLeftOpen size={18} aria-hidden /> : <PanelLeftClose size={18} aria-hidden />}
      </button>
      <Link to="/" className={s.brand} aria-label={t('app.name')}>
        <ShieldCheck size={22} color="var(--ok)" aria-hidden />
        <span>
          {t('app.name')}
          <small>{t('app.env')}{demoStatus.data?.seedVersion ? ` · seed ${demoStatus.data.seedVersion}` : ''}</small>
        </span>
      </Link>
      <Clock />
      <span className={s.status} data-testid="online-status" title={live === 'connected' ? t('app.liveConnected') : t('app.liveDisconnected')}>
        <span className={[s.dot, online ? (live === 'connected' ? s.dotOk : s.dotWarn) : s.dotCrit].join(' ')} aria-hidden />
        {online ? (live === 'connected' ? <Wifi size={12} aria-hidden /> : <WifiOff size={12} aria-hidden />) : <WifiOff size={12} aria-hidden />}
        {online ? (live === 'connected' ? t('app.online') : t('app.degraded')) : t('app.offline')}
      </span>
      <div className={s.spacer} />
      <div className={s.topGroup}>
        <label className="sr-only" htmlFor="site-select">
          {t('app.site')}
        </label>
        <select id="site-select" className={s.siteSelect} defaultValue="SITE-01">
          <option value="SITE-01">SITE-01 · Zakład Centralny</option>
        </select>
        <ThemeSwitch />
        <SegmentedControl
          label={t('topbar.language')}
          data-testid="lang-switch"
          value={locale}
          onChange={(v) => setLocale(v)}
          options={[
            { value: 'pl', label: 'PL' },
            { value: 'en', label: 'EN' },
          ]}
        />
        {demoMode && (
          <Button size="sm" icon={<Play size={13} />} onClick={onOpenPresenter} data-testid="run-demo">
            {t('topbar.runDemo')}
          </Button>
        )}
        {demoMode && canReset && (
          <Button size="sm" variant="danger" icon={<RotateCcw size={13} />} onClick={() => setResetOpen(true)} data-testid="reset-demo">
            {t('topbar.resetDemo')}
          </Button>
        )}
        <div style={{ position: 'relative' }}>
          <IconButton label={t('topbar.notifications')} onClick={() => navigate('/notifications')} data-testid="notifications-button">
            <Bell size={17} />
          </IconButton>
          {unread > 0 && (
            <span style={{ position: 'absolute', top: -2, right: -2 }} aria-label={t('topbar.unread', { count: unread })}>
              <Badge count={unread} />
            </span>
          )}
        </div>
        <div className={s.menuWrap} ref={menuRef}>
          <button type="button" className={s.userBtn} onClick={() => setMenuOpen((o) => !o)} aria-haspopup="menu" aria-expanded={menuOpen} data-testid="user-menu">
            <UserCog size={16} aria-hidden />
            <span>
              <strong>{user?.displayName ?? '—'}</strong>
              <span data-testid="current-role">{user ? t(`roles.${user.role}`) : ''}</span>
            </span>
            <ChevronDown size={14} aria-hidden />
          </button>
          {menuOpen && (
            <div className={s.menu} role="menu">
              {demoMode && (
                <>
                  <div className={s.menuTitle}>{t('topbar.switchRole')}</div>
                  {ALL_ROLES.filter((r) => r !== 'SupplierUser').map((r) => (
                    <button key={r} type="button" role="menuitem" className={s.menuItem} aria-current={user?.role === r} onClick={() => switchRole(r)} data-testid={`switch-role-${r}`}>
                      {t(`roles.${r}`)}
                    </button>
                  ))}
                  {(supplierAccounts.length ? supplierAccounts : [{ username: 'supplier.hydromech', role: 'SupplierUser' as Role, supplierCode: 'SUP-02', description: '' }]).map((a) => (
                    <button key={a.username} type="button" role="menuitem" className={s.menuItem} aria-current={user?.role === 'SupplierUser' && user.supplierName === a.description} onClick={() => switchRole('SupplierUser', a.supplierCode ?? undefined)} data-testid={`switch-role-SupplierUser-${a.supplierCode}`}>
                      {t('roles.SupplierUser')} · {a.supplierCode}
                    </button>
                  ))}
                </>
              )}
              <button type="button" role="menuitem" className={s.menuItem} onClick={() => { setMenuOpen(false); logout(); navigate('/login'); }}>
                <LogOut size={14} aria-hidden /> {t('topbar.logout')}
              </button>
            </div>
          )}
        </div>
      </div>
      <ConfirmDialog
        open={resetOpen}
        onClose={() => setResetOpen(false)}
        title={t('demo.resetTitle')}
        impact={t('demo.resetImpact')}
        danger
        confirmLabel={t('topbar.resetDemo')}
        loading={reset.isPending}
        onConfirm={() => {
          reset.mutate(undefined, {
            onSuccess: (r) => {
              setResetOpen(false);
              toast.ok(t('demo.resetDone', { ms: r.durationMs }));
            },
            onError: () => toast.critical(t('common.error')),
          });
        }}
      >
        {reset.isPending && <p className="muted">{t('demo.resetting')}</p>}
      </ConfirmDialog>
    </header>
  );
}
