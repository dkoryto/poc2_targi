import { NavLink } from 'react-router';
import { useTranslation } from 'react-i18next';
import { LayoutDashboard, Truck, Warehouse, CalendarRange, GitBranch, FileBadge, ScrollText, Settings2 } from 'lucide-react';
import s from './layout.module.css';
import { useAuth, canAccess } from '@/features/auth/auth';

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

export function Nav({ collapsed = false }: { collapsed?: boolean }) {
  const { t } = useTranslation();
  const { user } = useAuth();
  return (
    <nav
      id="main-nav"
      className={[s.nav, collapsed && s.navRail].filter(Boolean).join(' ')}
      aria-label="main"
      data-testid="main-nav"
      data-collapsed={collapsed ? 'true' : 'false'}
    >
      {ITEMS.filter((it) => canAccess(user?.role, it.to)).map((it) => {
        const label = t(`nav.${it.key}`);
        return (
          <NavLink
            key={it.to}
            to={it.to}
            end={it.to === '/'}
            className={s.navLink}
            data-testid={`nav-${it.key}`}
            title={collapsed ? label : undefined}
          >
            <it.icon size={17} aria-hidden />
            <span className={s.navLabel}>{label}</span>
            {collapsed && <span className={s.navTip} aria-hidden>{label}</span>}
            {collapsed && <span className="sr-only">{label}</span>}
          </NavLink>
        );
      })}
    </nav>
  );
}
