import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate, useParams } from 'react-router';
import { ArrowLeft, Copy, Download, ExternalLink } from 'lucide-react';
import s from './trace.module.css';
import { useSerial, useTraceAudit } from './api';
import { GenealogyTree, nodeStatusLabel, nodeStatusTone } from './GenealogyTree';
import { hitRoute } from './TracePage';
import type { AuditEvent, TraceComponent, TraceNode, TraceSearchHit } from '@/api/types';
import { Button, Card, DataTable, ErrorState, LoadingState, Sheet, StatusChip, Tabs, useIsMobile, type Column, useToast } from '@/components/ui';
import { copyText, downloadFile } from '@/lib/download';
import { fmtDateTime } from '@/lib/format';
import { buildUrl } from '@/api/client';
import { RecordSite } from '@/features/sites/SiteSwitch';

export function Hash({ value, full }: { value?: string | null; full?: boolean }) {
  const { t } = useTranslation();
  const toast = useToast();
  if (!value) return <span className="muted">—</span>;
  const short = full ? value : `${value.slice(0, 12)}…${value.slice(-6)}`;
  return (
    <span className={s.hash} title={value}>
      <span>{short}</span>
      <button type="button" className="muted" style={{ background: 'none', border: 0, cursor: 'pointer', padding: 0, display: 'inline-flex' }} aria-label={t('common.copy')} onClick={() => void copyText(value).then((ok) => (ok ? toast.ok(t('common.copied')) : toast.warn(t('common.copyFailed'))))} data-testid="copy-hash">
        <Copy size={12} />
      </button>
    </span>
  );
}

function NodePanel({ node, onNavigate }: { node: TraceNode; onNavigate: (route: string) => void }) {
  const { t } = useTranslation();
  const toast = useToast();
  const meta = node.meta ?? {};
  const route = node.kind === 'Serial' || node.kind === 'Lot' || node.kind === 'Heat' || node.kind === 'PurchaseOrder' || node.kind === 'Passport' ? hitRoute({ kind: node.kind, code: node.code, label: node.label } as TraceSearchHit) : null;
  const docId = node.kind === 'Document' ? String(meta.documentId ?? meta.id ?? node.code) : null;
  return (
    <div className="stack" data-testid="trace-node-panel">
      <div>
        <div className="muted" style={{ fontSize: 11, textTransform: 'uppercase', letterSpacing: '0.05em' }}>{t(`trace.kind.${node.kind}`, { defaultValue: node.kind })}</div>
        <div className="mono" style={{ fontSize: 'var(--fs-lg)', fontWeight: 600 }}>{node.code}</div>
        <div className="muted">{node.label}</div>
      </div>
      {node.status && <StatusChip tone={nodeStatusTone(node.status)} label={nodeStatusLabel(t, node.kind, node.status)} />}
      {Object.keys(meta).length > 0 && (
        <dl className={s.meta} style={{ gridTemplateColumns: '1fr 1fr' }}>
          {Object.entries(meta).filter(([k]) => !['documentId', 'id'].includes(k)).map(([k, v]) => (
            <div key={k} className={s.metaItem}>
              <dt>{t(`trace.meta.${k}`, { defaultValue: k })}</dt>
              <dd>{k.toLowerCase().includes('sha') ? <Hash value={String(v)} /> : /At$|On$|Date$/.test(k) && typeof v === 'string' ? fmtDateTime(v) : String(v ?? '—')}</dd>
            </div>
          ))}
        </dl>
      )}
      <div className="row">
        {route && (
          <Button size="sm" icon={<ExternalLink size={13} />} onClick={() => onNavigate(route)} data-testid="trace-node-open">
            {t('common.goTo')}
          </Button>
        )}
        {docId && (
          <Button size="sm" icon={<Download size={13} />} onClick={() => void downloadFile(`/documents/${docId}/download`, String(meta.fileName ?? `${node.code}.pdf`)).catch(() => toast.critical(t('common.error')))} data-testid="trace-node-download">
            {t('trace.downloadDoc')}
          </Button>
        )}
      </div>
    </div>
  );
}

export function SerialPage() {
  const { t } = useTranslation();
  const { serial } = useParams<{ serial: string }>();
  const navigate = useNavigate();
  const data = useSerial(serial);
  const [tab, setTab] = useState('genealogy');
  const [selected, setSelected] = useState<TraceNode | null>(null);
  const isMobile = useIsMobile();
  const audit = useTraceAudit({ entity: 'Serial', code: serial }, tab === 'audit');
  const trace = data.data;

  const compCols: Column<TraceComponent>[] = [
    { key: 'part', header: t('supply.part'), render: (r) => <span><span className="mono">{r.partCode}</span>{r.partName ? <span className="muted"> · {r.partName}</span> : null}</span>, sortValue: (r) => r.partCode, card: 'title' },
    { key: 'lot', header: t('trace.lotHeat'), render: (r) => <Link to={`/trace/lots/${encodeURIComponent(r.lotNumber)}`} className="mono">{r.lotNumber}{r.heatNumber ? ` / ${r.heatNumber}` : ''}</Link>, sortValue: (r) => r.lotNumber },
    { key: 'sup', header: t('supply.supplier'), render: (r) => `${r.supplierName ?? r.supplierCode}${r.country ? ` (${r.country})` : ''}`, sortValue: (r) => r.supplierCode },
    { key: 'cert', header: t('trace.certificate'), render: (r) => <Hash value={r.certSha256} /> },
  ];
  const auditCols: Column<AuditEvent>[] = [
    { key: 'at', header: t('audit.occurredAt'), render: (r) => fmtDateTime(r.occurredAt), sortValue: (r) => r.occurredAt },
    { key: 'user', header: t('audit.user'), render: (r) => r.user },
    { key: 'action', header: t('audit.action'), render: (r) => r.action, card: 'title' },
    { key: 'entity', header: t('audit.entity'), render: (r) => <span className="mono">{r.entity} {r.entityCode}</span> },
    { key: 'corr', header: t('audit.correlationId'), render: (r) => <span className="mono muted" style={{ fontSize: 11 }}>{r.correlationId.slice(0, 8)}</span> },
  ];

  return (
    <div className="page" data-testid="serial-page">
      <div className="page-header">
        <div>
          <Link to="/trace" className={["row", s.backLink].join(" ")} style={{ fontSize: 'var(--fs-xs)' }}><ArrowLeft size={12} aria-hidden />{t('trace.backToSearch')}</Link>
          <h1 className="mono">{serial}</h1>
          {trace && <p>{trace.productName} ({trace.productCode}) · {t('gantt.order')} <Link to="/planning">{trace.orderCode}</Link> · BOM {trace.bomVersion}</p>}
          {trace?.siteCode && <RecordSite code={trace.siteCode} />}
        </div>
        {trace && (
          <div className="row">
            <StatusChip tone={nodeStatusTone(trace.status)} label={nodeStatusLabel(t, 'Serial', trace.status)} />
            <Button size="sm" onClick={() => navigate(`/passports/${encodeURIComponent(trace.serial)}`)} data-testid="open-passport">{t('trace.openPassport')}</Button>
          </div>
        )}
      </div>
      {data.isLoading && <LoadingState />}
      {data.isError && <ErrorState error={data.error} onRetry={() => data.refetch()} />}
      {trace && (
        <>
          <Tabs value={tab} onChange={setTab} items={[{ key: 'genealogy', label: t('trace.genealogy') }, { key: 'components', label: t('trace.traceBack') }, { key: 'audit', label: t('trace.history') }]} />
          {tab === 'genealogy' && (
            <div className={s.layout}>
              <Card title={t('trace.genealogy')} definition={t('trace.genealogyDef')}>
                <GenealogyTree root={trace.genealogy} selected={selected} onSelect={setSelected} />
              </Card>
              {isMobile ? (
                <Sheet open={!!selected} onClose={() => setSelected(null)} title={selected?.code ?? t('common.details')} data-testid="trace-node-sheet">
                  {selected && <NodePanel node={selected} onNavigate={(r) => { setSelected(null); navigate(r); }} />}
                </Sheet>
              ) : (
                <Card title={t('common.details')}>
                  {selected ? <NodePanel node={selected} onNavigate={navigate} /> : <p className="muted">{t('trace.selectNode')}</p>}
                </Card>
              )}
            </div>
          )}
          {tab === 'components' && (
            <Card title={t('trace.traceBack')} definition={t('trace.traceBackDef')} flush>
              <DataTable columns={compCols} rows={trace.components ?? componentsFromTree(trace.genealogy)} rowKey={(r) => `${r.partCode}-${r.lotNumber}`} emptyTitle={t('trace.noComponents')} data-testid="trace-components" />
            </Card>
          )}
          {tab === 'audit' && (
            <Card title={t('trace.history')} actions={<a href={buildUrl('/trace/audit', { entity: 'Serial', code: serial, format: 'csv' })} onClick={(e) => { e.preventDefault(); void downloadFile(`/trace/audit?entity=Serial&code=${encodeURIComponent(serial ?? '')}&format=csv`, `audit-${serial}.csv`); }} className="row" data-testid="audit-export"><Download size={13} aria-hidden />{t('audit.exportCsv')}</a>} flush>
              <DataTable columns={auditCols} rows={audit.data?.items} loading={audit.isLoading} error={audit.error} onRetry={() => audit.refetch()} rowKey={(r) => r.id} initialSort={{ key: 'at', dir: 'desc' }} />
            </Card>
          )}
        </>
      )}
    </div>
  );
}

/** Fallback: derive the components table from the genealogy tree when the API omits `components`. */
export function componentsFromTree(root: TraceNode): TraceComponent[] {
  const out: TraceComponent[] = [];
  const walk = (n: TraceNode, ctx: Partial<TraceComponent>) => {
    const next = { ...ctx };
    if (n.kind === 'Lot') {
      next.lotNumber = n.code;
      next.partCode = String(n.meta?.partCode ?? next.partCode ?? '');
      next.partName = (n.meta?.partName as string) ?? next.partName;
      next.heatNumber = (n.meta?.heatNumber as string) ?? null;
    }
    if (n.kind === 'Supplier') { next.supplierCode = n.code; next.supplierName = n.label; next.country = (n.meta?.country as string) ?? null; }
    if (n.kind === 'Document' && n.meta?.sha256) next.certSha256 = String(n.meta.sha256);
    if (n.children.length === 0 && next.lotNumber) {
      if (!out.some((c) => c.lotNumber === next.lotNumber)) out.push({ partCode: next.partCode ?? '', lotNumber: next.lotNumber, supplierCode: next.supplierCode ?? '', ...next } as TraceComponent);
    }
    n.children.forEach((c) => walk(c, next));
  };
  walk(root, {});
  return out;
}
