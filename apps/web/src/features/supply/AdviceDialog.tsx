import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useTranslation } from 'react-i18next';
import { useCreateAdvice } from './api';
import { Button, Dialog, FormField, FormGrid, Input, useToast } from '@/components/ui';
import type { PurchaseOrderDetail } from '@/api/types';

const schema = z.object({ carrier: z.string().min(1), vehicle: z.string().min(1), plannedDeparture: z.string().min(1), eta: z.string().min(1) });
type Form = z.infer<typeof schema>;

export function AdviceDialog({ open, onClose, po }: { open: boolean; onClose: () => void; po: PurchaseOrderDetail }) {
  const { t } = useTranslation();
  const toast = useToast();
  const create = useCreateAdvice();
  const [selected, setSelected] = useState<string[]>(po.lines.filter((l) => !l.shipmentCode).map((l) => l.id));
  const { register, handleSubmit, formState } = useForm<Form>({ resolver: zodResolver(schema) });
  const submit = handleSubmit(async (v) => {
    if (selected.length === 0) return;
    try {
      const sh = await create.mutateAsync({ poCode: po.code, lineIds: selected, ...v });
      toast.ok(t('supply.adviceCreated', { code: sh.code }));
      onClose();
    } catch {
      toast.critical(t('common.error'));
    }
  });
  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={`${t('supply.advice')} · ${po.code}`}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>{t('common.cancel')}</Button>
          <Button variant="primary" onClick={submit} loading={create.isPending} disabled={selected.length === 0}>{t('supply.createAdvice')}</Button>
        </>
      }
    >
      <form onSubmit={submit} noValidate className="stack">
        <fieldset style={{ border: '1px solid var(--border)', borderRadius: 4, padding: 10 }}>
          <legend style={{ fontSize: 'var(--fs-xs)', color: 'var(--fg-2)' }}>{t('supply.selectLines')}</legend>
          {po.lines.map((l) => (
            <label key={l.id} className="row" style={{ fontSize: 'var(--fs-sm)' }}>
              <input type="checkbox" checked={selected.includes(l.id)} onChange={(e) => setSelected((s) => (e.target.checked ? [...s, l.id] : s.filter((x) => x !== l.id)))} />
              {l.lineNo} · {l.partCode} × {l.quantity} {l.unit}
            </label>
          ))}
        </fieldset>
        <FormGrid>
          <FormField label={t('supply.carrier')} required error={formState.errors.carrier && t('common.required')}>{(id) => <Input id={id} {...register('carrier')} />}</FormField>
          <FormField label={t('supply.vehicle')} required error={formState.errors.vehicle && t('common.required')}>{(id) => <Input id={id} {...register('vehicle')} />}</FormField>
          <FormField label={t('supply.plannedDeparture')} required error={formState.errors.plannedDeparture && t('common.required')}>{(id) => <Input id={id} type="datetime-local" {...register('plannedDeparture')} />}</FormField>
          <FormField label={t('supply.eta')} required error={formState.errors.eta && t('common.required')}>{(id) => <Input id={id} type="date" {...register('eta')} />}</FormField>
        </FormGrid>
      </form>
    </Dialog>
  );
}
