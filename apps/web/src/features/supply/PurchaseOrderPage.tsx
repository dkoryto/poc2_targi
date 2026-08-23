import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router';
import { Truck, ArrowLeft } from 'lucide-react';
import { usePurchaseOrder } from './api';
import { LineDrawer } from './LineDrawer';
import { AdviceDialog } from './AdviceDialog';
import { EtaCell } from './SupplyListPage';
import { SiteChip } from '@/features/sites/SiteSwitch';
import { Button, Card, DataTable, DocStatusChip, ErrorState, LoadingState, PoStatusChip, ProgressBar, RiskBadge, Timeline, type Column } from '@/components/ui';
import type { PurchaseOrderLine } from '@/api/types';
import { fmtDate } from '@/lib/format';
import { useAuth } from '@/features/auth/auth';

export function PurchaseOrderPage() {
  const { t } = useTranslation();
  const { code = '' } = useParams();
  const { user } = useAuth();
  const q = usePurchaseOrder(code);
  const [lineId, setLineId] = useState<string | null>(null);
  const [advice, setAdvice] = useState(false);
  const line = q.data?.lines.find((l) => l.id === lineId) ?? null;
  const canAdvice = user?.role === 'SupplierUser' || user?.role === 'InboundCoordinator' || user?.role === 'DemoPresenter' || user?.role === 'Administrator';

  const columns: Column<PurchaseOrderLine>[] = [
    { key: 'no', header: '#', render: (l) => l.lineNo, sortValue: (l) => l.lineNo, width: 40 },
    { key: 'part', header: t('supply.part'), render: (l) => <span><strong>{l.partCode}</strong> <span className="muted">{l.partName}</span></span>, sortValue: (l) => l.partCode },
    { key: 'qty', header: t('supply.qty'), render: (l) => `${l.quantity} ${l.unit}`, sortValue: (l) => l.quantity, align: 'right' },
    { key: 'status', header: t('supply.status'), render: (l) => <PoStatusChip status={l.status} small />, sortValue: (l) => l.status },
    { key: 'progress', header: t('supply.progress'), render: (l) => <ProgressBar value={l.progressPercent} label={t('supply.progress')} />, sortValue: (l) => l.progressPercent, width: 140 },
    { key: 'required', header: t('supply.required'), render: (l) => fmtDate(l.requiredDate), sortValue: (l) => l.requiredDate },
    { key: 'eta', header: t('supply.eta'), render: (l) => <EtaCell eta={l.eta} required={l.requiredDate} />, sortValue: (l) => l.eta },
    { key: 'lot', header: t('supply.lotNumber'), render: (l) => l.lotNumber ?? '—', sortValue: (l) => l.lotNumber },
    { key: 'docs', header: t('supply.documents'), render: (l) => <span className="row">{l.documents.length === 0 ? <span className="muted">—</span> : l.documents.map((d) => <DocStatusChip key={d.id} status={d.status} small />)}</span> },
    { key: 'risk', header: t('supply.risk'), render: (l) => <RiskBadge category={l.risk.category} score={l.risk.score} small />, sortValue: (l) => l.risk.score },
    { key: 'act', header: '', render: (l) => <Button size="sm" onClick={(e) => { e.stopPropagation(); setLineId(l.id); }} data-testid={`edit-line-${l.lineNo}`}>{t('supply.editLine')}</Button> },
  ];

  return (
    <div className="page" data-testid="po-page">
      <div className="page-header">
        <div>
          <Link to="/supply" className="row" style={{ fontSize: 'var(--fs-xs)' }}><ArrowLeft size={12} /> {t('supply.orders')}</Link>
          <h1>{code}</h1>
          {q.data && (
            <p>
              {q.data.supplier.code} · {q.data.supplier.name} · {q.data.supplier.city}, {q.data.supplier.country} · {t('supply.ordered')} {fmtDate(q.data.orderedAt)}
              {q.data.siteCode ? <> · <SiteChip code={q.data.siteCode} /></> : null}
            </p>
          )}
        </div>
        <div className="row">
          {q.data && <PoStatusChip status={q.data.status} />}
          {q.data && canAdvice && <Button icon={<Truck size={14} />} onClick={() => setAdvice(true)}>{t('supply.advice')}</Button>}
        </div>
      </div>
      {q.isLoading && <LoadingState />}
      {q.isError && <ErrorState error={q.error} onRetry={() => q.refetch()} />}
      {q.data && (
        <>
          <DataTable columns={columns} rows={q.data.lines} rowKey={(l) => l.id} onRowClick={(l) => setLineId(l.id)} selectedKey={lineId} data-testid="po-lines" />
          <Card title={t('supply.history')}>
            {q.data.history.length === 0 ? <p className="muted">{t('supply.noHistory')}</p> : (
              <Timeline items={q.data.history.map((h) => ({ id: h.id, at: h.occurredAt, who: h.user, title: <span><strong>{h.action}</strong>{h.field ? ` · ${h.field}` : ''}{h.before || h.after ? `: ${h.before ?? '—'} → ${h.after ?? '—'}` : ''}</span>, body: h.comment }))} />
            )}
          </Card>
          <LineDrawer poCode={code} line={line} onClose={() => setLineId(null)} />
          {advice && <AdviceDialog open={advice} onClose={() => setAdvice(false)} po={q.data} />}
        </>
      )}
    </div>
  );
}
