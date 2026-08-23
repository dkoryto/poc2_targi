import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate, useSearchParams } from 'react-router';
import s from './trace.module.css';
import { useLots } from './api';
import { LotStatusChip } from './LotPage';
import type { LotSummary } from '@/api/types';
import { LOT_STATUSES } from '@/api/types';
import { Button, Card, DataTable, FormField, Input, Select, type Column } from '@/components/ui';
import { fmtDate, fmtNumber } from '@/lib/format';

export function LotsPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [params, setParams] = useSearchParams();
  const [q, setQ] = useState(params.get('q') ?? '');
  const status = params.get('status') ?? '';
  const partCode = params.get('partCode') ?? '';
  const lots = useLots({ q: q || undefined, status: status || undefined, partCode: partCode || undefined });
  const setParam = (k: string, v: string) => { const p = new URLSearchParams(params); if (v) p.set(k, v); else p.delete(k); setParams(p); };

  const cols: Column<LotSummary>[] = [
    { key: 'lot', header: t('trace.lot'), render: (r) => <span className="mono">{r.lotNumber}{r.heatNumber ? <span className="muted"> / {r.heatNumber}</span> : null}</span>, sortValue: (r) => r.lotNumber },
    { key: 'part', header: t('supply.part'), render: (r) => <span>{r.partCode}{r.partName ? <span className="muted"> · {r.partName}</span> : null}</span>, sortValue: (r) => r.partCode },
    { key: 'sup', header: t('supply.supplier'), render: (r) => r.supplierName ?? r.supplierCode, sortValue: (r) => r.supplierCode },
    { key: 'qty', header: t('supply.qty'), align: 'right', render: (r) => `${fmtNumber(r.quantity)} ${r.unit}`, sortValue: (r) => r.quantity },
    { key: 'recv', header: t('trace.receivedOn'), render: (r) => fmtDate(r.receivedOn), sortValue: (r) => r.receivedOn },
    { key: 'status', header: t('supply.status'), render: (r) => <LotStatusChip status={r.status} small />, sortValue: (r) => r.status },
  ];
  return (
    <div className="page" data-testid="lots-page">
      <div className="page-header"><div><h1>{t('trace.lots')}</h1><p>{t('trace.lotsSubtitle')}</p></div></div>
      <Card>
        <div className={s.filters}>
          <FormField label={t('common.search')}>{(id) => <Input id={id} value={q} onChange={(e) => { setQ(e.target.value); setParam('q', e.target.value); }} placeholder={t('trace.lotSearchPlaceholder')} />}</FormField>
          <FormField label={t('supply.status')}>{(id) => <Select id={id} value={status} onChange={(e) => setParam('status', e.target.value)}><option value="">{t('common.all')}</option>{LOT_STATUSES.map((st) => <option key={st} value={st}>{t(`status.lot.${st}`)}</option>)}</Select>}</FormField>
          <FormField label={t('supply.part')}>{(id) => <Input id={id} value={partCode} onChange={(e) => setParam('partCode', e.target.value)} placeholder="HTS-22" />}</FormField>
          <Button variant="ghost" onClick={() => { setQ(''); setParams({}); }}>{t('common.clearFilters')}</Button>
        </div>
      </Card>
      <Card flush>
        <DataTable columns={cols} rows={lots.data?.items} loading={lots.isLoading} error={lots.error} onRetry={() => lots.refetch()} rowKey={(r) => r.lotNumber} onRowClick={(r) => navigate(`/trace/lots/${encodeURIComponent(r.lotNumber)}`)} initialSort={{ key: 'recv', dir: 'desc' }} data-testid="lots-table" />
      </Card>
    </div>
  );
}
