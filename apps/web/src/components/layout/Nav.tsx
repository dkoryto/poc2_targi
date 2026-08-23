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

export function Nav() {
  const { t } = useTranslation();
  const { user } = useAuth();
  return (
    <nav className={s.nav} aria-label="main" data-testid="main-nav">
      {ITEMS.filter((it) => canAccess(user?.role, it.to)).map((it) => (
        <NavLink key={it.to} to={it.to} end={it.to === '/'} className={s.navLink} data-testid={`nav-${it.key}`}>
          <it.icon size={17} aria-hidden />
          {t(`nav.${it.key}`)}
        </NavLink>
      ))}
    </nav>
  );
}
