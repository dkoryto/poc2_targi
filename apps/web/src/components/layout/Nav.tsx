import { useEffect, useRef } from 'react';
import { NavLink } from 'react-router';
import { useTranslation } from 'react-i18next';
import { LayoutDashboard, Truck, Warehouse, CalendarRange, GitBranch, FileBadge, ScrollText, Settings2, X } from 'lucide-react';
import s from './layout.module.css';
import { useAuth, canAccess } from '@/features/auth/auth';
import { IconButton } from '@/components/ui';

const ITEMS = [
  { to: '/', key: 'controlRoom', icon: LayoutDashboard },
  { to: '/supply', key: 'supply', icon: Truck },
  { to: '/inbound', key: 'inbound', icon: Warehouse },
  { to: '/planning', key: 'planning', icon: CalendarRange },
  { to: '/trace', key: 'trace', icon: GitBranch },
  { to: '/passports', key: 'passports', icon: FileBadge },
  { to: '/audit', key: 'audit', icon: ScrollText },
  { to: '/admin', key: 'admin', icon: Settings2 },
] as const;

const FOCUSABLE = 'a[href], button:not([disabled])';

export function Nav({
  collapsed = false,
  drawer = false,
  open = false,
  onClose,
}: {
  collapsed?: boolean;
  /** Mobile: render as an off-canvas drawer over the content instead of a grid column. */
  drawer?: boolean;
  open?: boolean;
  onClose?: () => void;
}) {
  const { t } = useTranslation();
  const { user } = useAuth();
  const ref = useRef<HTMLElement>(null);

  // Drawer only: trap focus and close on Escape.
  useEffect(() => {
    if (!drawer || !open) return;
    const prev = document.activeElement as HTMLElement | null;
    const el = ref.current;
    el?.querySelector<HTMLElement>(FOCUSABLE)?.focus();
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        onClose?.();
        return;
      }
      if (e.key !== 'Tab' || !el) return;
      const nodes = Array.from(el.querySelectorAll<HTMLElement>(FOCUSABLE));
      if (nodes.length === 0) return;
      const first = nodes[0]!;
      const last = nodes[nodes.length - 1]!;
      if (e.shiftKey && document.activeElement === first) {
        e.preventDefault();
        last.focus();
      } else if (!e.shiftKey && document.activeElement === last) {
        e.preventDefault();
        first.focus();
      }
    };
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('keydown', onKey);
      prev?.focus?.();
    };
  }, [drawer, open, onClose]);

  const rail = collapsed && !drawer;
  const links = ITEMS.filter((it) => canAccess(user?.role, it.to)).map((it) => {
    const label = t(`nav.${it.key}`);
    return (
      <NavLink
        key={it.to}
        to={it.to}
        end={it.to === '/'}
        className={s.navLink}
        data-testid={`nav-${it.key}`}
        title={rail ? label : undefined}
        onClick={drawer ? onClose : undefined}
      >
        <it.icon size={17} aria-hidden />
        <span className={s.navLabel}>{label}</span>
        {rail && (
          <span className={s.navTip} aria-hidden>
            {label}
          </span>
        )}
        {rail && <span className="sr-only">{label}</span>}
      </NavLink>
    );
  });

  if (drawer) {
    if (!open) return null;
    return (
      <>
        <div className={s.navBackdrop} onMouseDown={onClose} aria-hidden />
        <nav
          ref={ref}
          id="main-nav"
          className={[s.nav, s.navDrawer].join(' ')}
          aria-label="main"
          data-testid="nav-drawer"
          data-collapsed="false"
        >
          <div className={s.navDrawerHeader}>
            <span>{t('topbar.menu')}</span>
            <IconButton label={t('common.close')} onClick={onClose}>
              <X size={18} />
            </IconButton>
          </div>
          {links}
        </nav>
      </>
    );
  }

  return (
    <nav
      id="main-nav"
      className={[s.nav, rail && s.navRail].filter(Boolean).join(' ')}
      aria-label="main"
      data-testid="main-nav"
      data-collapsed={rail ? 'true' : 'false'}
    >
      {links}
    </nav>
  );
}
