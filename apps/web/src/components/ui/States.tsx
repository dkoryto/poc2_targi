import { Inbox, AlertOctagon, RefreshCw } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import s from './ui.module.css';
import { Button } from './Button';
import { ApiError } from '@/api/client';

export function Skeleton({ height = 16, width = '100%', style }: { height?: number | string; width?: number | string; style?: React.CSSProperties }) {
  return <div className={s.skeleton} style={{ height, width, ...style }} aria-hidden data-testid="skeleton" />;
}

export function EmptyState({ title, detail, action }: { title?: string; detail?: string; action?: React.ReactNode }) {
  const { t } = useTranslation();
  return (
    <div className={s.state} role="status" data-testid="empty-state">
      <Inbox size={28} aria-hidden />
      <div className={s.stateTitle}>{title ?? t('common.empty')}</div>
      <div>{detail ?? t('common.emptyDetail')}</div>
      {action}
    </div>
  );
}

export function describeError(error: unknown, t: (k: string) => string): { title: string; detail: string } {
  if (error instanceof ApiError) {
    if (error.status === 403) return { title: t('common.forbidden'), detail: error.problem?.detail ?? '' };
    if (error.status === 404) return { title: t('common.notFound'), detail: error.problem?.detail ?? '' };
    if (error.status === 412) return { title: t('common.conflict'), detail: '' };
    const detail = error.problem?.detail ?? error.problem?.title ?? `HTTP ${error.status}`;
    const traceId = error.problem?.traceId ? ` (trace ${error.problem.traceId})` : '';
    return { title: t('common.error'), detail: `${detail}${traceId}` };
  }
  if (error instanceof Error) return { title: t('common.error'), detail: error.message || t('common.errorDetail') };
  return { title: t('common.error'), detail: t('common.errorDetail') };
}

export function ErrorState({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  const { t } = useTranslation();
  const { title, detail } = describeError(error, t);
  return (
    <div className={s.state} role="alert" data-testid="error-state">
      <AlertOctagon size={28} color="var(--crit)" aria-hidden />
      <div className={s.stateTitle}>{title}</div>
      {detail && <div>{detail}</div>}
      {onRetry && (
        <Button size="sm" onClick={onRetry} icon={<RefreshCw size={13} />}>
          {t('common.retry')}
        </Button>
      )}
    </div>
  );
}

export function LoadingState({ rows = 4 }: { rows?: number }) {
  const { t } = useTranslation();
  return (
    <div className="stack" aria-busy="true" aria-label={t('common.loading')} data-testid="loading-state">
      {Array.from({ length: rows }).map((_, i) => (
        <Skeleton key={i} height={18} width={`${90 - i * 12}%`} />
      ))}
    </div>
  );
}
