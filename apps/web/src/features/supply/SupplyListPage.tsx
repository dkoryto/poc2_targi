import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate, useSearchParams } from 'react-router';
import s from './supply.module.css';
import { usePurchaseOrders, type PoFilters } from './api';
import { useAuth } from '@/features/auth/auth';
import { Button, DataTable, FormField, Input, PoStatusChip, ProgressBar, RiskBadge, Select, type Column } from '@/components/ui';
import { PO_LINE_STATUSES, type PurchaseOrderSummary } from '@/api/types';
import { daysBetween, fmtDate } from '@/lib/format';

export function EtaCell({ eta, required }: { eta: string; required: string }) {
  const { t } = useTranslation();
  const d = daysBetween(eta, required);
  const color = d > 0 ? 'var(--crit)' : d < 0 ? 'var(--ok)' : 'var(--fg-2)';
  return (
    <span className={s.etaCell}>
      <span>{fmtDate(eta)}</span>
      <span className={s.etaDelta} style={{ color }}>
        {d > 0 ? t('supply.late', { days: d }) : d < 0 ? t('supply.early', { days: -d }) : t('supply.onTime')}
      </span>
    </span>
  );
}

export function SupplyListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { user } = useAuth();
  const [params, setParams] = useSearchParams();
  const filters: PoFilters = useMemo(
    () => ({
      status: params.get('status') ?? undefined,
      riskCategory: params.get('riskCategory') ?? undefined,
      siteCode: params.get('siteCode') ?? undefined,
      dueFrom: params.get('dueFrom') ?? undefined,
      dueTo: params.get('dueTo') ?? undefined,
      q: params.get('q') ?? undefined,
    }),
    [params],
  );
  const q = usePurchaseOrders(filters);
  const set = (k: string, v: string) => {
    const next = new URLSearchParams(params);
    if (v) next.set(k, v);
    else next.delete(k);
    setParams(next, { replace: true });
  };

  const columns: Column<PurchaseOrderSummary>[] = [
    { key: 'code', header: t('supply.order'), render: (r) => <strong>{r.code}</strong>, sortValue: (r) => r.code },
    ...(user?.role !== 'SupplierUser' ? [{ key: 'supplier', header: t('supply.supplier'), render: (r: PurchaseOrderSummary) => `${r.supplierCode} · ${r.supplierName}`, sortValue: (r: PurchaseOrderSummary) => r.supplierName } as Column<PurchaseOrderSummary>] : []),
    { key: 'status', header: t('supply.status'), render: (r) => <PoStatusChip status={r.status} small />, sortValue: (r) => r.status },
    { key: 'lines', header: t('supply.lines'), render: (r) => r.lineCount, sortValue: (r) => r.lineCount, align: 'right' },
    { key: 'required', header: t('supply.required'), render: (r) => fmtDate(r.requiredDate), sortValue: (r) => r.requiredDate },
    { key: 'eta', header: t('supply.eta'), render: (r) => <EtaCell eta={r.eta} required={r.requiredDate} />, sortValue: (r) => r.eta },
    { key: 'progress', header: t('supply.progress'), render: (r) => <ProgressBar value={r.progressPercent} label={t('supply.progress')} />, sortValue: (r) => r.progressPercent, width: 140 },
    { key: 'risk', header: t('supply.risk'), render: (r) => <RiskBadge category={r.riskCategory} score={r.riskScore} small />, sortValue: (r) => r.riskScore },
    { key: 'site', header: t('supply.site'), render: (r) => r.siteCode, sortValue: (r) => r.siteCode },
  ];

  return (
    <div className="page" data-testid="supply-list">
      <div className="page-header">
        <div>
          <h1>{t('supply.title')}</h1>
          <p>{t('supply.subtitle')}{user?.role === 'SupplierUser' ? ` · ${user.supplierName ?? ''} — ${t('supply.ownDataOnly')}` : ''}</p>
        </div>
      </div>
      <div className={s.filters}>
        <FormField label={t('common.search')}>{(id) => <Input id={id} placeholder={t('supply.searchPlaceholder')} value={params.get('q') ?? ''} onChange={(e) => set('q', e.target.value)} />}</FormField>
        <FormField label={t('supply.filterStatus')}>
          {(id) => (
            <Select id={id} value={params.get('status') ?? ''} onChange={(e) => set('status', e.target.value)}>
              <option value="">{t('common.all')}</option>
              {PO_LINE_STATUSES.map((st) => <option key={st} value={st}>{t(`status.po.${st}`)}</option>)}
            </Select>
          )}
        </FormField>
        <FormField label={t('supply.filterRisk')}>
          {(id) => (
            <Select id={id} value={params.get('riskCategory') ?? ''} onChange={(e) => set('riskCategory', e.target.value)}>
              <option value="">{t('common.all')}</option>
              {(['Low', 'Medium', 'High', 'Critical'] as const).map((c) => <option key={c} value={c}>{t(`risk.${c}`)}</option>)}
            </Select>
          )}
        </FormField>
        <FormField label={`${t('supply.filterDue')} ${t('common.from').toLowerCase()}`}>{(id) => <Input id={id} type="date" value={params.get('dueFrom') ?? ''} onChange={(e) => set('dueFrom', e.target.value)} />}</FormField>
        <FormField label={`${t('supply.filterDue')} ${t('common.to').toLowerCase()}`}>{(id) => <Input id={id} type="date" value={params.get('dueTo') ?? ''} onChange={(e) => set('dueTo', e.target.value)} />}</FormField>
        <Button variant="ghost" onClick={() => setParams(new URLSearchParams(), { replace: true })}>{t('common.clearFilters')}</Button>
      </div>
      <DataTable columns={columns} rows={q.data?.items} rowKey={(r) => r.code} loading={q.isLoading} error={q.error} onRetry={() => q.refetch()} onRowClick={(r) => navigate(`/supply/orders/${r.code}`)} initialSort={{ key: 'risk', dir: 'desc' }} data-testid="po-table" />
    </div>
  );
}
