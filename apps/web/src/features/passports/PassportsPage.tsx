import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate, useSearchParams } from 'react-router';
import s from './passports.module.css';
import { usePassports } from './api';
import type { PassportStatus, PassportSummary } from '@/api/types';
import { PASSPORT_STATUSES } from '@/api/types';
import { Button, Card, DataTable, FormField, Input, StatusChip, type Column, type Tone } from '@/components/ui';
import { fmtDateTime } from '@/lib/format';

const PASSPORT_TONE: Record<PassportStatus, Tone> = { Draft: 'neutral', PendingReview: 'info', Approved: 'ok', Generated: 'ok', Invalidated: 'critical' };
export function PassportStatusChip({ status, small }: { status: PassportStatus; small?: boolean }) {
  const { t } = useTranslation();
  return <StatusChip tone={PASSPORT_TONE[status] ?? 'neutral'} label={t(`status.passport.${status}`, { defaultValue: status })} small={small} />;
}

export function PassportsPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [params, setParams] = useSearchParams();
  const status = params.get('status') ?? '';
  const [q, setQ] = useState(params.get('q') ?? '');
  const list = usePassports({ status: status || undefined, q: q || undefined });
  const setStatus = (v: string) => { const p = new URLSearchParams(params); if (v) p.set('status', v); else p.delete('status'); setParams(p); };

  const cols: Column<PassportSummary>[] = [
    { key: 'serial', header: t('passports.serial'), render: (r) => <span className="mono">{r.serial}</span>, sortValue: (r) => r.serial },
    { key: 'product', header: t('passports.product'), render: (r) => <span>{r.productName ?? r.productCode}<span className="muted"> · {r.productCode}</span></span>, sortValue: (r) => r.productCode },
    { key: 'order', header: t('gantt.order'), render: (r) => <span className="mono">{r.orderCode}</span>, sortValue: (r) => r.orderCode },
    { key: 'status', header: t('supply.status'), render: (r) => <PassportStatusChip status={r.status} small />, sortValue: (r) => r.status },
    { key: 'complete', header: t('passports.completeness'), render: (r) => <StatusChip tone={r.complete ? 'ok' : 'warn'} label={r.complete ? t('passports.complete') : t('passports.missingCount', { count: r.missingCount ?? 0 })} small />, sortValue: (r) => (r.complete ? 1 : 0) },
    { key: 'version', header: t('common.version'), align: 'right', render: (r) => (r.latestVersion ? `v${r.latestVersion}` : '—'), sortValue: (r) => r.latestVersion ?? 0 },
    { key: 'updated', header: t('passports.updatedAt'), render: (r) => fmtDateTime(r.updatedAt), sortValue: (r) => r.updatedAt ?? '' },
  ];
  return (
    <div className="page" data-testid="passports-page">
      <div className="page-header"><div><h1>{t('passports.title')}</h1><p>{t('passports.subtitle')}</p></div></div>
      <Card>
        <div className={s.filters}>
          <FormField label={t('common.search')}>{(id) => <Input id={id} value={q} onChange={(e) => { setQ(e.target.value); const p = new URLSearchParams(params); if (e.target.value) p.set('q', e.target.value); else p.delete('q'); setParams(p); }} placeholder={t('passports.searchPlaceholder')} />}</FormField>
          <FormField label={t('supply.status')}>
            {() => (
              <div className={s.statusFilter} role="group" aria-label={t('supply.status')}>
                <Button size="sm" variant={status === '' ? 'primary' : 'default'} onClick={() => setStatus('')}>{t('common.all')}</Button>
                {PASSPORT_STATUSES.map((st) => <Button key={st} size="sm" variant={status === st ? 'primary' : 'default'} onClick={() => setStatus(st)} data-testid={`passport-filter-${st}`}>{t(`status.passport.${st}`)}</Button>)}
              </div>
            )}
          </FormField>
          <Button variant="ghost" onClick={() => { setQ(''); setParams({}); }}>{t('common.clearFilters')}</Button>
        </div>
      </Card>
      <Card flush>
        <DataTable columns={cols} rows={list.data?.items} loading={list.isLoading} error={list.error} onRetry={() => list.refetch()} rowKey={(r) => r.serial} onRowClick={(r) => navigate(`/passports/${encodeURIComponent(r.serial)}`)} initialSort={{ key: 'serial', dir: 'asc' }} data-testid="passports-table" />
      </Card>
    </div>
  );
}
