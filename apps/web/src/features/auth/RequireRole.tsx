import { Navigate, Outlet, useLocation } from 'react-router';
import { useTranslation } from 'react-i18next';
import { useAuth, canAccess, homeFor } from './auth';
import { EmptyState } from '@/components/ui';

export function RequireAuth() {
  const { user, ready } = useAuth();
  const loc = useLocation();
  const { t } = useTranslation();
  if (!ready) return <div className="page muted">{t('common.loading')}</div>;
  if (!user) return <Navigate to="/login" replace state={{ from: loc.pathname }} />;
  const base = '/' + (loc.pathname.split('/')[1] ?? '');
  if (!canAccess(user.role, base)) {
    if (loc.pathname === '/') return <Navigate to={homeFor(user.role)} replace />;
    return (
      <div className="page">
        <EmptyState title={t('common.forbidden')} detail="" />
      </div>
    );
  }
  return <Outlet />;
}
