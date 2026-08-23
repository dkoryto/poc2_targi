import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Plus, Trash2 } from 'lucide-react';
import s from './planning.module.css';
import type { PlanningBaseline, ScenarioChange, ScenarioChangeType } from '@/api/types';
import { SCENARIO_CHANGE_TYPES } from '@/api/types';
import { Button, Dialog, FormField, Input, Select } from '@/components/ui';
import { usePurchaseOrder, usePurchaseOrders } from '@/features/supply/api';
import { useLots } from '@/features/trace/api';

export function describeChange(c: ScenarioChange, t: (k: string, o?: Record<string, unknown>) => string): string {
  switch (c.type) {
    case 'DELAY_INBOUND':
      return t('planning.change.DELAY_INBOUND_desc', { line: c.poCode ? `${c.poCode}${c.partCode ? ` · ${c.partCode}` : ''}` : c.poLineId, days: c.days });
    case 'BLOCK_LOT':
      return t('planning.change.BLOCK_LOT_desc', { lot: c.lotNumber });
    case 'PRIORITY':
      return t('planning.change.PRIORITY_desc', { order: c.orderCode, priority: c.priority });
    case 'CAPACITY':
      return t('planning.change.CAPACITY_desc', { wc: c.workCenterCode, pct: Math.round(c.factor * 100) });
    case 'DELAY_ORDER':
      return t('planning.change.DELAY_ORDER_desc', { order: c.orderCode, days: c.days });
  }
}

interface Draft {
  type: ScenarioChangeType;
  poCode: string;
  poLineId: string;
  partCode: string;
  lotNumber: string;
  orderCode: string;
  workCenterCode: string;
  days: string;
  priority: string;
  factor: string;
}
const EMPTY: Draft = { type: 'DELAY_INBOUND', poCode: '', poLineId: '', partCode: '', lotNumber: '', orderCode: '', workCenterCode: '', days: '10', priority: '5', factor: '0.5' };

function toChange(d: Draft): ScenarioChange | null {
  switch (d.type) {
    case 'DELAY_INBOUND':
      return d.poLineId && Number(d.days) > 0 ? { type: 'DELAY_INBOUND', poLineId: d.poLineId, days: Number(d.days), poCode: d.poCode, partCode: d.partCode } : null;
    case 'BLOCK_LOT':
      return d.lotNumber ? { type: 'BLOCK_LOT', lotNumber: d.lotNumber } : null;
    case 'PRIORITY':
      return d.orderCode ? { type: 'PRIORITY', orderCode: d.orderCode, priority: Math.min(5, Math.max(1, Number(d.priority) || 1)) } : null;
    case 'CAPACITY':
      return d.workCenterCode ? { type: 'CAPACITY', workCenterCode: d.workCenterCode, factor: Math.min(1, Math.max(0.1, Number(d.factor) || 0.5)) } : null;
    case 'DELAY_ORDER':
      return d.orderCode && Number(d.days) > 0 ? { type: 'DELAY_ORDER', orderCode: d.orderCode, days: Number(d.days) } : null;
  }
}

export function ScenarioBuilder({ open, onClose, onSubmit, baseline, submitting }: { open: boolean; onClose: () => void; onSubmit: (name: string, changes: ScenarioChange[]) => void; baseline?: PlanningBaseline; submitting?: boolean }) {
  const { t } = useTranslation();
  const [name, setName] = useState('');
  const [changes, setChanges] = useState<ScenarioChange[]>([]);
  const [draft, setDraft] = useState<Draft>(EMPTY);
  const pos = usePurchaseOrders({});
  const po = usePurchaseOrder(draft.type === 'DELAY_INBOUND' && draft.poCode ? draft.poCode : undefined);
  const lots = useLots({}, open && draft.type === 'BLOCK_LOT');
  const orders = baseline?.gantt.orders ?? [];
  const wcs = baseline?.gantt.workCenters ?? [];
  const current = useMemo(() => toChange(draft), [draft]);

  const add = () => {
    if (!current) return;
    setChanges((c) => [...c, current]);
    setDraft({ ...EMPTY, type: draft.type });
  };
  const set = (patch: Partial<Draft>) => setDraft((d) => ({ ...d, ...patch }));
  const submit = () => {
    const all = current ? [...changes, current] : changes;
    if (all.length === 0) return;
    onSubmit(name.trim() || t('planning.customScenario'), all);
  };

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={t('planning.customScenario')}
      size="lg"
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>{t('common.cancel')}</Button>
          <Button variant="primary" onClick={submit} disabled={changes.length + (current ? 1 : 0) === 0} loading={submitting} data-testid="btn-create-scenario">
            {t('planning.createAndRun')}
          </Button>
        </>
      }
    >
      <div className="stack">
        <FormField label={t('planning.scenarioName')}>
          {(id) => <Input id={id} value={name} onChange={(e) => setName(e.target.value)} placeholder={t('planning.scenarioNamePlaceholder')} />}
        </FormField>
        {changes.length > 0 && (
          <ul className="stack" style={{ listStyle: 'none', padding: 0, margin: 0, gap: 4 }} aria-label={t('planning.changes')}>
            {changes.map((c, i) => (
              <li key={i} className="row" style={{ justifyContent: 'space-between', fontSize: 'var(--fs-sm)', background: 'var(--bg-2)', padding: '6px 8px', borderRadius: 4 }}>
                <span>{describeChange(c, t)}</span>
                <Button size="sm" variant="ghost" icon={<Trash2 size={13} />} onClick={() => setChanges((cs) => cs.filter((_, j) => j !== i))} aria-label={t('planning.removeChange')} />
              </li>
            ))}
          </ul>
        )}
        <div className={s.changeRow}>
          <FormField label={t('planning.changeType')}>
            {(id) => (
              <Select id={id} value={draft.type} onChange={(e) => setDraft({ ...EMPTY, type: e.target.value as ScenarioChangeType })}>
                {SCENARIO_CHANGE_TYPES.map((ty) => <option key={ty} value={ty}>{t(`planning.change.${ty}`)}</option>)}
              </Select>
            )}
          </FormField>
          {draft.type === 'DELAY_INBOUND' && (
            <>
              <FormField label={t('supply.order')}>
                {(id) => (
                  <Select id={id} value={draft.poCode} onChange={(e) => set({ poCode: e.target.value, poLineId: '', partCode: '' })}>
                    <option value="">—</option>
                    {pos.data?.items.map((p) => <option key={p.code} value={p.code}>{p.code} · {p.supplierName}</option>)}
                  </Select>
                )}
              </FormField>
              <FormField label={t('supply.line')}>
                {(id) => (
                  <Select id={id} value={draft.poLineId} disabled={!draft.poCode} onChange={(e) => { const l = po.data?.lines.find((x) => x.id === e.target.value); set({ poLineId: e.target.value, partCode: l?.partCode ?? '' }); }}>
                    <option value="">—</option>
                    {po.data?.lines.map((l) => <option key={l.id} value={l.id}>{l.lineNo} · {l.partCode} ({l.quantity} {l.unit})</option>)}
                  </Select>
                )}
              </FormField>
              <FormField label={t('planning.days')}>{(id) => <Input id={id} type="number" min={1} max={60} value={draft.days} onChange={(e) => set({ days: e.target.value })} style={{ width: 80 }} />}</FormField>
            </>
          )}
          {draft.type === 'BLOCK_LOT' && (
            <FormField label={t('trace.lot')}>
              {(id) => (
                <Select id={id} value={draft.lotNumber} onChange={(e) => set({ lotNumber: e.target.value })}>
                  <option value="">—</option>
                  {lots.data?.items.filter((l) => l.status !== 'Blocked').map((l) => <option key={l.lotNumber} value={l.lotNumber}>{l.lotNumber} · {l.partCode}</option>)}
                </Select>
              )}
            </FormField>
          )}
          {(draft.type === 'PRIORITY' || draft.type === 'DELAY_ORDER') && (
            <>
              <FormField label={t('gantt.order')}>
                {(id) => (
                  <Select id={id} value={draft.orderCode} onChange={(e) => set({ orderCode: e.target.value })}>
                    <option value="">—</option>
                    {orders.map((o) => <option key={o.code} value={o.code}>{o.code} · {o.productCode}</option>)}
                  </Select>
                )}
              </FormField>
              {draft.type === 'PRIORITY' ? (
                <FormField label={t('gantt.priority')}>{(id) => <Input id={id} type="number" min={1} max={5} value={draft.priority} onChange={(e) => set({ priority: e.target.value })} style={{ width: 80 }} />}</FormField>
              ) : (
                <FormField label={t('planning.days')}>{(id) => <Input id={id} type="number" min={1} max={60} value={draft.days} onChange={(e) => set({ days: e.target.value })} style={{ width: 80 }} />}</FormField>
              )}
            </>
          )}
          {draft.type === 'CAPACITY' && (
            <>
              <FormField label={t('gantt.workCenter')}>
                {(id) => (
                  <Select id={id} value={draft.workCenterCode} onChange={(e) => set({ workCenterCode: e.target.value })}>
                    <option value="">—</option>
                    {wcs.map((w) => <option key={w.code} value={w.code}>{w.code} · {w.name}</option>)}
                  </Select>
                )}
              </FormField>
              <FormField label={t('planning.factor')} hint={t('planning.factorHint')}>{(id) => <Input id={id} type="number" min={0.1} max={1} step={0.1} value={draft.factor} onChange={(e) => set({ factor: e.target.value })} style={{ width: 80 }} />}</FormField>
            </>
          )}
          <Button size="sm" icon={<Plus size={13} />} onClick={add} disabled={!current} data-testid="btn-add-change">
            {t('planning.addChange')}
          </Button>
        </div>
      </div>
    </Dialog>
  );
}
