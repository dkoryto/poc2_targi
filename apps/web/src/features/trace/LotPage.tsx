import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router';
import { ArrowLeft, Ban, ClipboardPlus, Download } from 'lucide-react';
import s from './trace.module.css';
import { useAddInspection, useBlockLot, useLot, useLotForward } from './api';
import { Hash } from './SerialPage';
import { nodeStatusLabel, nodeStatusTone } from './GenealogyTree';
import type { BlockLotResponse, InspectionResult } from '@/api/types';
import { Button, Card, ConfirmDialog, Dialog, DocStatusChip, ErrorState, FormField, Input, LoadingState, Select, StatusChip, Textarea, Timeline, useToast } from '@/components/ui';
import { useAuth } from '@/features/auth/auth';
import { downloadFile } from '@/lib/download';
import { dateInputValue, fmtDate, fmtDateTime, fmtNumber } from '@/lib/format';

const CAN_QUALITY = ['QualityInspector', 'DemoPresenter', 'Administrator'];

export function LotStatusChip({ status, small }: { status: string; small?: boolean }) {
  const { t } = useTranslation();
  return <StatusChip tone={nodeStatusTone(status)} label={t(`status.lot.${status}`, { defaultValue: status })} small={small} />;
}

export function LotPage() {
  const { t } = useTranslation();
  const { lot: lotNumber = '' } = useParams<{ lot: string }>();
  const toast = useToast();
  const { user } = useAuth();
  const lot = useLot(lotNumber);
  const forward = useLotForward(lotNumber);
  const block = useBlockLot(lotNumber);
  const inspect = useAddInspection(lotNumber);
  const [blockOpen, setBlockOpen] = useState(false);
  const [reason, setReason] = useState('');
  const [ncrTitle, setNcrTitle] = useState('');
  const [result, setResult] = useState<BlockLotResponse | null>(null);
  const [inspOpen, setInspOpen] = useState(false);
  const [insp, setInsp] = useState<{ result: InspectionResult; notes: string; inspectedAt: string }>({ result: 'Passed', notes: '', inspectedAt: dateInputValue(new Date()) });
  const canQuality = !!user && CAN_QUALITY.includes(user.role);
  const d = lot.data;

  const doBlock = async () => {
    try {
      const r = await block.mutateAsync({ reason, ncrTitle });
      setResult(r);
      setBlockOpen(false);
      toast.critical(t('trace.blocked', { lot: lotNumber }), t('trace.blockedDetail', { orders: r.affected.orders.length, serials: r.affected.serials.length, passports: r.affected.passports.length }));
    } catch (e) {
      toast.critical(t('common.error'), e instanceof Error ? e.message : undefined);
    }
  };
  const doInspect = async () => {
    try {
      await inspect.mutateAsync({ result: insp.result, notes: insp.notes || undefined, inspectedAt: new Date(insp.inspectedAt).toISOString() });
      setInspOpen(false);
      toast.ok(t('trace.inspectionAdded'));
    } catch (e) {
      toast.critical(t('common.error'), e instanceof Error ? e.message : undefined);
    }
  };

  return (
    <div className="page" data-testid="lot-page">
      <div className="page-header">
        <div>
          <Link to="/trace/lots" className={["row", s.backLink].join(" ")} style={{ fontSize: 'var(--fs-xs)' }}><ArrowLeft size={12} aria-hidden />{t('trace.allLots')}</Link>
          <h1 className="mono">{lotNumber}</h1>
          {d && <p>{d.partName ?? d.partCode} · {d.supplierName ?? d.supplierCode} · {fmtNumber(d.quantity)} {d.unit}</p>}
        </div>
        {d && (
          <div className="row">
            <LotStatusChip status={d.status} />
            {canQuality && (
              <>
                <Button icon={<ClipboardPlus size={14} />} onClick={() => setInspOpen(true)} data-testid="btn-add-inspection">{t('trace.addInspection')}</Button>
                <Button variant="danger" icon={<Ban size={14} />} onClick={() => setBlockOpen(true)} disabled={d.status === 'Blocked' || d.status === 'Recalled'} data-testid="btn-block-lot">{t('trace.blockLot')}</Button>
              </>
            )}
          </div>
        )}
      </div>
      {lot.isLoading && <LoadingState />}
      {lot.isError && <ErrorState error={lot.error} onRetry={() => lot.refetch()} />}
      {result && (
        <Card title={t('trace.blockResult')} data-testid="block-result">
          <div className={s.panelList}>
            <div>{t('trace.affectedOrders')}: <strong>{result.affected.orders.join(', ') || '—'}</strong></div>
            <div>{t('trace.affectedSerials')}: <strong>{result.affected.serials.join(', ') || '—'}</strong></div>
            <div>{t('trace.affectedPassports')}: <strong>{result.affected.passports.map((p) => <Link key={p} to={`/passports/${p}`} style={{ marginRight: 8 }}>{p}</Link>)}{result.affected.passports.length === 0 && '—'}</strong></div>
          </div>
        </Card>
      )}
      {d && (
        <div className={s.layout}>
          <div className="stack">
            <Card title={t('trace.lotData')}>
              <dl className={s.meta}>
                <div className={s.metaItem}><dt>{t('supply.part')}</dt><dd className="mono">{d.partCode}</dd></div>
                <div className={s.metaItem}><dt>{t('supply.heatNumber')}</dt><dd className="mono">{d.heatNumber ?? '—'}</dd></div>
                <div className={s.metaItem}><dt>{t('supply.supplier')}</dt><dd>{d.supplierName ?? d.supplierCode}</dd></div>
                <div className={s.metaItem}><dt>{t('supply.order')}</dt><dd>{d.poCode ? <Link to={`/supply/orders/${d.poCode}`}>{d.poCode}</Link> : '—'}</dd></div>
                <div className={s.metaItem}><dt>{t('trace.receivedOn')}</dt><dd>{fmtDate(d.receivedOn)}</dd></div>
                <div className={s.metaItem}><dt>{t('supply.producedOn')}</dt><dd>{fmtDate(d.producedOn)}</dd></div>
                <div className={s.metaItem}><dt>{t('supply.expiresOn')}</dt><dd>{fmtDate(d.expiresOn)}</dd></div>
                <div className={s.metaItem}><dt>{t('supply.qty')}</dt><dd>{fmtNumber(d.quantity)} {d.unit}</dd></div>
                <div className={s.metaItem}><dt>{t('supply.status')}</dt><dd><LotStatusChip status={d.status} small /></dd></div>
              </dl>
            </Card>
            <Card title={t('supply.documents')}>
              {d.documents.length === 0 && <p className="muted">{t('supply.noDocuments')}</p>}
              <div className={s.panelList}>
                {d.documents.map((doc) => (
                  <div key={doc.id} className={s.panelRow}>
                    <span><strong>{t(`docType.${doc.type}`)}</strong> <span className="muted">{doc.documentNumber ?? doc.fileName}</span></span>
                    <span className="row"><Hash value={doc.sha256} /><DocStatusChip status={doc.status} small />
                      <Button size="sm" variant="ghost" icon={<Download size={13} />} aria-label={t('trace.downloadDoc')} onClick={() => void downloadFile(`/documents/${doc.id}/download`, doc.fileName).catch(() => toast.critical(t('common.error')))} />
                    </span>
                  </div>
                ))}
              </div>
            </Card>
            <Card title={t('trace.inspections')}>
              {d.inspections.length === 0 ? <p className="muted">{t('trace.noInspections')}</p> : (
                <Timeline items={d.inspections.map((i) => ({ id: i.id, at: i.inspectedAt, who: i.inspector, title: <span className="row"><StatusChip tone={nodeStatusTone(i.result)} label={t(`status.inspection.${i.result}`)} small /></span>, body: i.notes }))} />
              )}
              {d.nonConformances && d.nonConformances.length > 0 && (
                <>
                  <h3 style={{ marginTop: 12 }}>{t('trace.ncr')}</h3>
                  <div className={s.panelList}>{d.nonConformances.map((n) => <div key={n.id} className={s.panelRow}><span><span className="mono">{n.code}</span> {n.title}</span><span className="muted">{n.status} · {fmtDateTime(n.raisedAt)}</span></div>)}</div>
                </>
              )}
            </Card>
          </div>
          <Card title={t('trace.traceForward')} definition={t('trace.traceForwardDef')} data-testid="trace-forward">
            {forward.isLoading && <LoadingState rows={3} />}
            {forward.isError && <ErrorState error={forward.error} onRetry={() => forward.refetch()} />}
            {forward.data && (
              <div className="stack">
                <div>
                  <h3>{t('trace.affectedOrders')} ({forward.data.orders.length})</h3>
                  <div className={s.panelList}>{forward.data.orders.map((o) => <div key={o.orderCode} className={s.panelRow}><span className="mono">{o.orderCode}</span><span className="row"><span className="muted">{t(`trace.relation.${o.relation}`)}</span><StatusChip tone={nodeStatusTone(o.status)} label={nodeStatusLabel(t, 'Order', o.status)} small /></span></div>)}{forward.data.orders.length === 0 && <span className="muted">—</span>}</div>
                </div>
                <div>
                  <h3>{t('trace.affectedSerials')} ({forward.data.serials.length})</h3>
                  <div className={s.panelList}>{forward.data.serials.map((x) => <div key={x.serial} className={s.panelRow}><Link to={`/trace/serials/${x.serial}`} className="mono">{x.serial}</Link><span className="muted">{x.orderCode} · {x.productCode}</span></div>)}{forward.data.serials.length === 0 && <span className="muted">—</span>}</div>
                </div>
                <div>
                  <h3>{t('trace.affectedPassports')} ({forward.data.passports.length})</h3>
                  <div className={s.panelList}>{forward.data.passports.map((p) => <div key={p.serial} className={s.panelRow}><Link to={`/passports/${p.serial}`} className="mono">{p.serial}</Link><StatusChip tone={nodeStatusTone(p.status)} label={t(`status.passport.${p.status}`)} small /></div>)}{forward.data.passports.length === 0 && <span className="muted">—</span>}</div>
                </div>
                {d.reservedBy.length > 0 && <div className="muted" style={{ fontSize: 'var(--fs-xs)' }}>{t('trace.reservedBy')}: {d.reservedBy.join(', ')}</div>}
              </div>
            )}
          </Card>
        </div>
      )}

      <ConfirmDialog
        open={blockOpen}
        onClose={() => setBlockOpen(false)}
        onConfirm={() => void doBlock()}
        title={t('trace.blockTitle', { lot: lotNumber })}
        confirmLabel={t('trace.blockLot')}
        danger
        loading={block.isPending}
        impact={
          forward.data ? (
            <span>{t('trace.blockImpact', { orders: forward.data.orders.length, serials: forward.data.serials.length, passports: forward.data.passports.filter((p) => p.status === 'Generated' || p.status === 'Approved').length })}</span>
          ) : undefined
        }
      >
        <div className="stack">
          <FormField label={t('trace.blockReason')} required>{(id) => <Textarea id={id} rows={3} value={reason} onChange={(e) => setReason(e.target.value)} data-testid="block-reason" />}</FormField>
          <FormField label={t('trace.ncrTitle')} required>{(id) => <Input id={id} value={ncrTitle} onChange={(e) => setNcrTitle(e.target.value)} data-testid="block-ncr" />}</FormField>
        </div>
      </ConfirmDialog>

      <Dialog open={inspOpen} onClose={() => setInspOpen(false)} title={t('trace.addInspection')} footer={<><Button variant="ghost" onClick={() => setInspOpen(false)}>{t('common.cancel')}</Button><Button variant="primary" onClick={() => void doInspect()} loading={inspect.isPending} data-testid="submit-inspection">{t('common.save')}</Button></>}>
        <div className="stack">
          <FormField label={t('trace.inspectionResult')}>{(id) => <Select id={id} value={insp.result} onChange={(e) => setInsp({ ...insp, result: e.target.value as InspectionResult })}>{(['Passed', 'Failed', 'Conditional'] as const).map((r) => <option key={r} value={r}>{t(`status.inspection.${r}`)}</option>)}</Select>}</FormField>
          <FormField label={t('trace.inspectedAt')}>{(id) => <Input id={id} type="date" value={insp.inspectedAt} onChange={(e) => setInsp({ ...insp, inspectedAt: e.target.value })} />}</FormField>
          <FormField label={t('trace.notes')}>{(id) => <Textarea id={id} rows={3} value={insp.notes} onChange={(e) => setInsp({ ...insp, notes: e.target.value })} />}</FormField>
        </div>
      </Dialog>
    </div>
  );
}
