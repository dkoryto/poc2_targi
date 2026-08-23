import { Fragment, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useSearchParams } from 'react-router';
import { ChevronDown, ChevronRight, Download } from 'lucide-react';
import a from './audit.module.css';
import { useAudit } from './api';
import { JsonDiff } from './JsonDiff';
import type { AuditEvent } from '@/api/types';
import { Button, Card, EmptyState, ErrorState, FilterBar, FormField, Input, ScrollArea, Select, Skeleton, useIsMobile, useToast } from '@/components/ui';
import { downloadFile } from '@/lib/download';
import { fmtDateTime } from '@/lib/format';

const ENTITIES = ['', 'PurchaseOrderLine', 'Shipment', 'MaterialLot', 'QualityDocument', 'PlanningScenario', 'PlanningBaseline', 'Passport', 'ProductionOrder', 'Serial', 'User'];
const PAGE_SIZE = 50;

export function AuditPage() {
  const { t } = useTranslation();
  const toast = useToast();
  const [params, setParams] = useSearchParams();
  const f = { entity: params.get('entity') ?? '', code: params.get('code') ?? '', user: params.get('user') ?? '', from: params.get('from') ?? '', to: params.get('to') ?? '', page: Number(params.get('page') ?? 1) };
  const audit = useAudit({ entity: f.entity || undefined, code: f.code || undefined, user: f.user || undefined, from: f.from || undefined, to: f.to || undefined, page: f.page, pageSize: PAGE_SIZE });
  const [open, setOpen] = useState<string | null>(null);
  const isMobile = useIsMobile();
  const set = (k: string, v: string) => { const p = new URLSearchParams(params); if (v) p.set(k, v); else p.delete(k); if (k !== 'page') p.delete('page'); setParams(p); };
  const total = audit.data?.total ?? 0;
  const pages = Math.max(1, Math.ceil(total / PAGE_SIZE));
  const qs = Object.entries({ entity: f.entity, code: f.code, user: f.user, from: f.from, to: f.to }).filter(([, v]) => v).map(([k, v]) => `${k}=${encodeURIComponent(v)}`).join('&');

  return (
    <div className="page" data-testid="audit-page">
      <div className="page-header">
        <div><h1>{t('audit.title')}</h1><p>{t('audit.subtitle')}</p></div>
        <Button icon={<Download size={14} />} onClick={() => void downloadFile(`/audit?${qs}${qs ? '&' : ''}format=csv`, 'audit.csv').catch(() => toast.critical(t('common.error')))} data-testid="audit-export">{t('audit.exportCsv')}</Button>
      </div>
      <Card>
        <FilterBar activeCount={[f.entity, f.code, f.user, f.from, f.to].filter(Boolean).length} onClear={() => setParams({})} data-testid="audit-filters">
        <div className={a.filters}>
          <FormField label={t('audit.entity')}>{(id) => <Select id={id} value={f.entity} onChange={(e) => set('entity', e.target.value)}>{ENTITIES.map((e) => <option key={e} value={e}>{e ? t(`audit.entities.${e}`, { defaultValue: e }) : t('common.all')}</option>)}</Select>}</FormField>
          <FormField label={t('audit.code')}>{(id) => <Input id={id} value={f.code} onChange={(e) => set('code', e.target.value)} placeholder="WO-2026-014" />}</FormField>
          <FormField label={t('audit.user')}>{(id) => <Input id={id} value={f.user} onChange={(e) => set('user', e.target.value)} />}</FormField>
          <FormField label={t('common.from')}>{(id) => <Input id={id} type="date" value={f.from} onChange={(e) => set('from', e.target.value)} />}</FormField>
          <FormField label={t('common.to')}>{(id) => <Input id={id} type="date" value={f.to} onChange={(e) => set('to', e.target.value)} />}</FormField>
        </div>
        </FilterBar>
      </Card>
      <Card flush>
        {audit.isLoading && <div style={{ padding: 12 }} className="stack">{[1, 2, 3, 4].map((i) => <Skeleton key={i} height={18} />)}</div>}
        {audit.isError && <ErrorState error={audit.error} onRetry={() => audit.refetch()} />}
        {audit.data && audit.data.items.length === 0 && <EmptyState />}
        {audit.data && audit.data.items.length > 0 && isMobile && (
          <div className={a.cardList} data-testid="audit-table">
            {audit.data.items.map((r: AuditEvent) => (
              <Fragment key={r.id}>
                <button type="button" className={a.rowCard} onClick={() => setOpen((o) => (o === r.id ? null : r.id))} aria-expanded={open === r.id} data-testid={`audit-row-${r.id}`}>
                  <span className={a.rowCardHead}>
                    <strong>{r.action}</strong>
                    {open === r.id ? <ChevronDown size={14} aria-hidden /> : <ChevronRight size={14} aria-hidden />}
                  </span>
                  <span className={a.rowCardMeta}>{fmtDateTime(r.occurredAt)} · {r.user}</span>
                  <span className={['mono', a.rowCardMeta].join(' ')}>{r.entity} {r.entityCode}</span>
                </button>
                {open === r.id && (
                  <div className={a.detail} data-testid="audit-detail">
                    <JsonDiff before={r.before} after={r.after} />
                    <div className="muted" style={{ fontSize: 11, marginTop: 6 }}>{t('audit.correlationId')}: <span className="mono">{r.correlationId}</span></div>
                  </div>
                )}
              </Fragment>
            ))}
          </div>
        )}
        {audit.data && audit.data.items.length > 0 && !isMobile && (
          <ScrollArea label={t('audit.title')}>
          <table className={a.table} data-testid="audit-table">
            <thead>
              <tr style={{ textAlign: 'left', color: 'var(--fg-2)', fontSize: 11, textTransform: 'uppercase', letterSpacing: '0.05em' }}>
                <th style={{ padding: '8px 10px', width: 28 }} />
                <th style={{ padding: '8px 10px' }}>{t('audit.occurredAt')}</th>
                <th style={{ padding: '8px 10px' }}>{t('audit.user')}</th>
                <th style={{ padding: '8px 10px' }}>{t('audit.action')}</th>
                <th style={{ padding: '8px 10px' }}>{t('audit.entity')}</th>
                <th style={{ padding: '8px 10px' }}>{t('audit.source')}</th>
                <th style={{ padding: '8px 10px' }}>{t('audit.correlationId')}</th>
              </tr>
            </thead>
            <tbody>
              {audit.data.items.map((r: AuditEvent) => (
                <Fragment key={r.id}>
                  <tr style={{ borderTop: '1px solid var(--border)', cursor: 'pointer' }} onClick={() => setOpen((o) => (o === r.id ? null : r.id))} data-testid={`audit-row-${r.id}`}>
                    <td style={{ padding: '6px 10px' }}><button type="button" aria-expanded={open === r.id} aria-label={t('common.details')} style={{ background: 'none', border: 0, color: 'inherit', cursor: 'pointer', padding: 0 }}>{open === r.id ? <ChevronDown size={14} /> : <ChevronRight size={14} />}</button></td>
                    <td style={{ padding: '6px 10px', whiteSpace: 'nowrap' }}>{fmtDateTime(r.occurredAt)}</td>
                    <td style={{ padding: '6px 10px' }}>{r.user}</td>
                    <td style={{ padding: '6px 10px' }}><strong>{r.action}</strong></td>
                    <td style={{ padding: '6px 10px' }}><span className="mono">{r.entity} {r.entityCode}</span></td>
                    <td style={{ padding: '6px 10px' }} className="muted">{r.source}</td>
                    <td style={{ padding: '6px 10px' }} className="mono muted" title={r.correlationId}>{r.correlationId.slice(0, 8)}</td>
                  </tr>
                  {open === r.id && (
                    <tr style={{ background: 'var(--bg-1)' }} data-testid="audit-detail">
                      <td />
                      <td colSpan={6} style={{ padding: '8px 10px' }}>
                        <JsonDiff before={r.before} after={r.after} />
                        <div className="muted" style={{ fontSize: 11, marginTop: 6 }}>{t('audit.correlationId')}: <span className="mono">{r.correlationId}</span></div>
                      </td>
                    </tr>
                  )}
                </Fragment>
              ))}
            </tbody>
          </table>
          </ScrollArea>
        )}
        {audit.data && pages > 1 && (
          <div className="row" style={{ justifyContent: 'space-between', padding: 10, borderTop: '1px solid var(--border)' }}>
            <span className="muted">{t('audit.pageOf', { page: f.page, pages, total })}</span>
            <span className="row">
              <Button size="sm" disabled={f.page <= 1} onClick={() => set('page', String(f.page - 1))}>{t('common.previous')}</Button>
              <Button size="sm" disabled={f.page >= pages} onClick={() => set('page', String(f.page + 1))}>{t('common.next')}</Button>
            </span>
          </div>
        )}
      </Card>
    </div>
  );
}
