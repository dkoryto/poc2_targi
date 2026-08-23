import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate, useParams } from 'react-router';
import { Zap } from 'lucide-react';
import { useAddShipmentEvent, useLogisticsEvents, useRaiseLogisticsEvent, useShipment, useShipments } from './api';
import { useMapData } from '@/features/dashboard/api';
import { DeliveryMap } from '@/features/dashboard/DeliveryMap';
import { useSuppliers } from '@/features/supply/api';
import { Button, Card, DataTable, Drawer, ErrorState, FormField, FormGrid, Input, LoadingState, ProgressBar, RiskBadge, Select, ShipmentStatusChip, StatusChip, Textarea, Timeline, useToast, type Column } from '@/components/ui';
import { LOGISTICS_EVENT_TYPES, type LogisticsEventType, type Severity, type Shipment } from '@/api/types';
import { fmtDate, fmtDateTime } from '@/lib/format';
import { useAuth } from '@/features/auth/auth';

const simSchema = z.object({
  type: z.enum(LOGISTICS_EVENT_TYPES as [string, ...string[]]),
  severity: z.enum(['LOW', 'MEDIUM', 'HIGH']),
  targetKind: z.enum(['shipment', 'supplier']),
  target: z.string().min(1),
  description: z.string().min(3).max(300),
});
type SimForm = z.infer<typeof simSchema>;

const SHIPMENT_EVENT_TYPES = ['Departed', 'BorderCrossed', 'Delayed', 'Arrived', 'Note'];

function ShipmentDrawer({ code, onClose }: { code: string; onClose: () => void }) {
  const { t } = useTranslation();
  const toast = useToast();
  const q = useShipment(code);
  const add = useAddShipmentEvent(code);
  const [type, setType] = useState('Note');
  const [note, setNote] = useState('');
  const sh = q.data;
  return (
    <Drawer open onClose={onClose} title={`${t('inbound.shipment')} ${code}`}>
      {q.isLoading && <LoadingState />}
      {q.isError && <ErrorState error={q.error} onRetry={() => q.refetch()} />}
      {sh && (
        <div className="stack">
          <div className="row" style={{ justifyContent: 'space-between' }}>
            <span><Link to={`/supply/orders/${sh.poCode}`}>{sh.poCode}</Link> · {sh.supplierCode} {sh.supplierName}</span>
            <ShipmentStatusChip status={sh.status} />
          </div>
          <div className="row"><RiskBadge category={sh.riskCategory} score={sh.riskScore} /> <span className="muted">{t('supply.eta')} {fmtDate(sh.eta)}</span> {sh.carrier && <span className="muted">· {sh.carrier} {sh.vehicle}</span>}</div>
          <ProgressBar value={sh.progress * 100} label={t('inbound.progress')} />
          <div className="muted" style={{ fontSize: 'var(--fs-xs)' }}>{sh.lines.map((l) => `${l.partCode} × ${l.quantity}`).join(' · ')}</div>
          <h3>{t('inbound.events')}</h3>
          {sh.events.length === 0 ? <p className="muted">{t('inbound.noEvents')}</p> : <Timeline items={sh.events.map((e) => ({ id: e.id, at: e.occurredAt, who: e.user, title: t(`inbound.shipmentEventTypes.${e.type}`, { defaultValue: e.type }), body: e.note }))} />}
          <div style={{ borderTop: '1px solid var(--border)', paddingTop: 10 }}>
            <h3 style={{ marginBottom: 8 }}>{t('inbound.addEvent')}</h3>
            <FormGrid>
              <FormField label={t('inbound.eventType')}>{(id) => <Select id={id} value={type} onChange={(e) => setType(e.target.value)}>{SHIPMENT_EVENT_TYPES.map((x) => <option key={x} value={x}>{t(`inbound.shipmentEventTypes.${x}`)}</option>)}</Select>}</FormField>
              <FormField label={t('inbound.eventNote')}>{(id) => <Input id={id} value={note} onChange={(e) => setNote(e.target.value)} />}</FormField>
            </FormGrid>
            <div className="row" style={{ justifyContent: 'flex-end', marginTop: 8 }}>
              <Button variant="primary" size="sm" loading={add.isPending} onClick={() => add.mutate({ type, occurredAt: new Date().toISOString(), note: note || undefined }, { onSuccess: () => { toast.ok(t('inbound.eventAdded')); setNote(''); }, onError: () => toast.critical(t('common.error')) })}>{t('inbound.addEvent')}</Button>
            </div>
          </div>
        </div>
      )}
    </Drawer>
  );
}

export function InboundPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { code } = useParams();
  const { user } = useAuth();
  const toast = useToast();
  const shipments = useShipments();
  const events = useLogisticsEvents();
  const suppliers = useSuppliers();
  const map = useMapData();
  const raise = useRaiseLogisticsEvent();
  const canSimulate = user?.role === 'InboundCoordinator' || user?.role === 'DemoPresenter' || user?.role === 'Administrator';
  const form = useForm<SimForm>({ resolver: zodResolver(simSchema), defaultValues: { type: 'BORDER_DELAY', severity: 'MEDIUM', targetKind: 'shipment', target: '', description: '' } });
  const targetKind = form.watch('targetKind');

  const submit = form.handleSubmit(async (v) => {
    try {
      await raise.mutateAsync({ type: v.type as LogisticsEventType, severity: v.severity as Severity, description: v.description, ...(v.targetKind === 'shipment' ? { shipmentCode: v.target } : { supplierCode: v.target }) });
      toast.ok(t('inbound.raised'));
      form.reset({ type: 'BORDER_DELAY', severity: 'MEDIUM', targetKind: 'shipment', target: '', description: '' });
    } catch {
      toast.critical(t('common.error'));
    }
  });

  const columns: Column<Shipment>[] = [
    { key: 'code', header: t('inbound.shipment'), render: (r) => <strong>{r.code}</strong>, sortValue: (r) => r.code },
    { key: 'po', header: t('supply.order'), render: (r) => r.poCode, sortValue: (r) => r.poCode },
    { key: 'supplier', header: t('supply.supplier'), render: (r) => `${r.supplierCode} · ${r.supplierName}`, sortValue: (r) => r.supplierName },
    { key: 'parts', header: t('supply.part'), render: (r) => r.lines.map((l) => `${l.partCode}×${l.quantity}`).join(', ') },
    { key: 'status', header: t('supply.status'), render: (r) => <ShipmentStatusChip status={r.status} small />, sortValue: (r) => r.status },
    { key: 'eta', header: t('supply.eta'), render: (r) => fmtDate(r.eta), sortValue: (r) => r.eta },
    { key: 'progress', header: t('inbound.progress'), render: (r) => <ProgressBar value={r.progress * 100} label={t('inbound.progress')} />, sortValue: (r) => r.progress, width: 140 },
    { key: 'risk', header: t('supply.risk'), render: (r) => <RiskBadge category={r.riskCategory} score={r.riskScore} small />, sortValue: (r) => r.riskScore },
  ];

  return (
    <div className="page" data-testid="inbound-page">
      <div className="page-header">
        <div>
          <h1>{t('inbound.title')}</h1>
          <p>{t('inbound.subtitle')}</p>
        </div>
      </div>
      <div className="grid-2" style={{ gridTemplateColumns: '3fr 2fr' }}>
        <div className="stack">
          <DataTable columns={columns} rows={shipments.data?.items} rowKey={(r) => r.code} loading={shipments.isLoading} error={shipments.error} onRetry={() => shipments.refetch()} onRowClick={(r) => navigate(`/inbound/${r.code}`)} selectedKey={code ?? null} initialSort={{ key: 'risk', dir: 'desc' }} data-testid="shipments-table" />
          <Card title={t('dashboard.map')} flush style={{ height: 320 }}>
            {map.data && <DeliveryMap data={map.data} pulseCodes={new Set()} onOpenPo={(c) => navigate(`/supply/orders/${c}`)} />}
          </Card>
        </div>
        <div className="stack">
          {canSimulate && (
            <Card title={t('inbound.simulator')} definition={t('inbound.simulatorHint')}>
              <form onSubmit={submit} noValidate className="stack" data-testid="simulator-form">
                <FormGrid>
                  <FormField label={t('inbound.eventType')} required>{(id) => <Select id={id} {...form.register('type')}>{LOGISTICS_EVENT_TYPES.map((x) => <option key={x} value={x}>{t(`logisticsEvent.${x}`)}</option>)}</Select>}</FormField>
                  <FormField label={t('inbound.severity')} required>{(id) => <Select id={id} {...form.register('severity')}>{(['LOW', 'MEDIUM', 'HIGH'] as const).map((x) => <option key={x} value={x}>{t(`logisticsEvent.severity.${x}`)}</option>)}</Select>}</FormField>
                  <FormField label={t('inbound.target')} required>{(id) => <Select id={id} {...form.register('targetKind')}><option value="shipment">{t('inbound.targetShipment')}</option><option value="supplier">{t('inbound.targetSupplier')}</option></Select>}</FormField>
                  <FormField label={targetKind === 'shipment' ? t('inbound.targetShipment') : t('inbound.targetSupplier')} required error={form.formState.errors.target && t('common.required')}>
                    {(id) => (
                      <Select id={id} {...form.register('target')} invalid={!!form.formState.errors.target}>
                        <option value="">—</option>
                        {targetKind === 'shipment' ? shipments.data?.items.map((s) => <option key={s.code} value={s.code}>{s.code} · {s.supplierCode}</option>) : suppliers.data?.items.map((s) => <option key={s.code} value={s.code}>{s.code} · {s.name}</option>)}
                      </Select>
                    )}
                  </FormField>
                  <FormField label={t('inbound.description')} required full error={form.formState.errors.description && t('common.required')}>{(id) => <Textarea id={id} {...form.register('description')} invalid={!!form.formState.errors.description} />}</FormField>
                </FormGrid>
                <div className="row" style={{ justifyContent: 'flex-end' }}>
                  <Button type="submit" variant="primary" icon={<Zap size={14} />} loading={raise.isPending} data-testid="raise-event">{t('inbound.raise')}</Button>
                </div>
              </form>
            </Card>
          )}
          <Card title={t('inbound.activeEvents')}>
            {events.isLoading && <LoadingState rows={3} />}
            {events.isError && <ErrorState error={events.error} onRetry={() => events.refetch()} />}
            {events.data && events.data.items.filter((e) => e.active).length === 0 && <p className="muted">{t('inbound.noEvents')}</p>}
            <div className="stack">
              {events.data?.items.filter((e) => e.active).map((e) => (
                <div key={e.id} className="row" style={{ justifyContent: 'space-between', fontSize: 'var(--fs-sm)', borderBottom: '1px solid var(--border)', paddingBottom: 6 }}>
                  <span>
                    <strong>{t(`logisticsEvent.${e.type}`)}</strong> · {e.shipmentCode ?? e.supplierCode}
                    <br />
                    <span className="muted" style={{ fontSize: 11 }}>{e.description} · {fmtDateTime(e.raisedAt)}</span>
                  </span>
                  <StatusChip tone={e.severity === 'HIGH' ? 'critical' : e.severity === 'MEDIUM' ? 'warn' : 'info'} label={t(`logisticsEvent.severity.${e.severity}`)} small />
                </div>
              ))}
            </div>
          </Card>
        </div>
      </div>
      {code && <ShipmentDrawer code={code} onClose={() => navigate('/inbound')} />}
    </div>
  );
}
