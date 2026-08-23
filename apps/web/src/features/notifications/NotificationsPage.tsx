import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { Info, AlertTriangle, OctagonAlert } from 'lucide-react';
import s from '@/components/layout/layout.module.css';
import { useMarkRead, useNotifications } from './api';
import { Button, EmptyState, ErrorState, LoadingState } from '@/components/ui';
import { fmtDateTime } from '@/lib/format';

export function NotificationsPage() {
  const { t } = useTranslation();
  const [unreadOnly, setUnreadOnly] = useState(false);
  const q = useNotifications(true, unreadOnly);
  const mark = useMarkRead();
  const icon = { info: <Info size={16} color="var(--info)" />, warn: <AlertTriangle size={16} color="var(--warn)" />, critical: <OctagonAlert size={16} color="var(--crit)" /> };
  return (
    <div className="page">
      <div className="page-header">
        <div>
          <h1>{t('notifications.title')}</h1>
          <p>{t('notifications.subtitle')}</p>
        </div>
        <label className="row" style={{ fontSize: 'var(--fs-sm)' }}>
          <input type="checkbox" checked={unreadOnly} onChange={(e) => setUnreadOnly(e.target.checked)} /> {t('notifications.unreadOnly')}
        </label>
      </div>
      {q.isLoading && <LoadingState />}
      {q.isError && <ErrorState error={q.error} onRetry={() => q.refetch()} />}
      {q.data && q.data.items.length === 0 && <EmptyState title={t('notifications.empty')} detail="" />}
      <div className="stack">
        {q.data?.items.map((n) => (
          <div key={n.id} className={[s.notifItem, !n.read && s.notifUnread].filter(Boolean).join(' ')} style={{ borderLeftColor: n.severity === 'critical' ? 'var(--crit)' : n.severity === 'warn' ? 'var(--warn)' : 'var(--info)' }}>
            {icon[n.severity] ?? icon.info}
            <div style={{ flex: 1 }}>
              <div style={{ fontWeight: 600 }}>{n.title}</div>
              <div className="muted">{n.message}</div>
              <div className="muted" style={{ fontSize: 'var(--fs-xs)', marginTop: 4 }}>
                {fmtDateTime(n.createdAt)}
                {n.route && (
                  <>
                    {' · '}
                    <Link to={n.route}>{t('common.goTo')}</Link>
                  </>
                )}
              </div>
            </div>
            {!n.read && (
              <Button size="sm" variant="ghost" onClick={() => mark.mutate(n.id)}>
                {t('notifications.markRead')}
              </Button>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}
