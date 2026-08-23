import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useTranslation } from 'react-i18next';
import { Upload } from 'lucide-react';
import s from './supply.module.css';
import { useChangeEta, useLineImpact, usePatchLine, useUploadDocument } from './api';
import { RiskExplain } from './RiskExplain';
import { Button, DateInput, DocStatusChip, Drawer, FileInput, FormField, FormGrid, Input, PoStatusChip, RiskBadge, Select, Tabs, Textarea, useToast, FormAlert } from '@/components/ui';
import { DOCUMENT_TYPES, ETA_REASONS, PO_LINE_STATUSES, type EtaChangeResponse, type EtaReason, type PurchaseOrderLine, type RiskSummary } from '@/api/types';
import { isConflict } from '@/api/client';
import { useFormErrors } from '@/lib/formErrors';
import { dateInputValue, fmtBytes, fmtDate, fmtDateTime } from '@/lib/format';
import { useAuth } from '@/features/auth/auth';

const statusSchema = z.object({
  status: z.enum(PO_LINE_STATUSES as [string, ...string[]]),
  progressPercent: z.coerce.number().min(0).max(100),
  lotNumber: z.string().optional(),
  heatNumber: z.string().optional(),
  producedOn: z.string().optional(),
  expiresOn: z.string().optional(),
  quantity: z.coerce.number().positive(),
  comment: z.string().optional(),
});
type StatusForm = z.infer<typeof statusSchema>;

export const etaSchema = z.object({
  eta: z.string().min(1),
  reason: z.enum(ETA_REASONS as [string, ...string[]]),
  comment: z.string().max(500).optional(),
});
type EtaForm = z.infer<typeof etaSchema>;

const ALLOWED_EXT = ['pdf', 'png', 'jpg', 'jpeg'];
const MAX_BYTES = 10 * 1024 * 1024;

export function LineDrawer({ poCode, line, onClose }: { poCode: string; line: PurchaseOrderLine | null; onClose: () => void }) {
  const { t } = useTranslation();
  const toast = useToast();
  const { user } = useAuth();
  const [tab, setTab] = useState('eta');
  const [etaResult, setEtaResult] = useState<{ before: RiskSummary; res: EtaChangeResponse } | null>(null);
  const canEdit = user?.role === 'SupplierUser' || user?.role === 'InboundCoordinator' || user?.role === 'DemoPresenter' || user?.role === 'Administrator';
  const impact = useLineImpact(poCode, line && tab === 'impact' ? line.id : null);
  const patch = usePatchLine(poCode);
  const changeEta = useChangeEta(poCode);
  const upload = useUploadDocument(poCode);

  const etaForm = useForm<EtaForm>({ resolver: zodResolver(etaSchema), values: line ? { eta: dateInputValue(line.eta), reason: 'LOGISTICS', comment: '' } : undefined });
  const statusForm = useForm<StatusForm>({
    resolver: zodResolver(statusSchema),
    values: line ? { status: line.status, progressPercent: line.progressPercent, lotNumber: line.lotNumber ?? '', heatNumber: line.heatNumber ?? '', producedOn: dateInputValue(line.producedOn), expiresOn: dateInputValue(line.expiresOn), quantity: line.quantity, comment: '' } : undefined,
  });
  const [docType, setDocType] = useState<string>('MATERIAL_CERT');
  const [docNumber, setDocNumber] = useState('');
  const [issuedOn, setIssuedOn] = useState('');
  const [file, setFile] = useState<File | null>(null);
  const [fileError, setFileError] = useState<string | null>(null);
  const docErrors = useFormErrors();
  const etaErrors = useFormErrors();
  const statusErrors = useFormErrors();

  if (!line) return null;

  const submitEta = etaForm.handleSubmit(async (v) => {
    const before = line.risk;
    try {
      const res = await changeEta.mutateAsync({ lineId: line.id, body: { eta: v.eta, reason: v.reason as EtaReason, comment: v.comment } });
      setEtaResult({ before, res });
      toast.ok(t('supply.etaUpdated', { category: t(`risk.${res.risk.category}`), score: Math.round(res.risk.score) }));
      etaErrors.clear();
    } catch (e) {
      if (isConflict(e)) etaErrors.setFormError(t('common.conflict'));
      else etaErrors.fromApi(e, t('common.error'));
    }
  });

  const submitStatus = statusForm.handleSubmit(async (v) => {
    try {
      await patch.mutateAsync({ lineId: line.id, rowVersion: line.rowVersion, patch: { status: v.status as PurchaseOrderLine['status'], progressPercent: v.progressPercent, lotNumber: v.lotNumber || undefined, heatNumber: v.heatNumber || undefined, producedOn: v.producedOn || undefined, expiresOn: v.expiresOn || undefined, quantity: v.quantity, comment: v.comment || undefined } });
      toast.ok(t('common.saved'));
      statusErrors.clear();
    } catch (e) {
      if (isConflict(e)) statusErrors.setFormError(t('common.conflict'));
      else statusErrors.fromApi(e, t('common.error'));
    }
  });

  const onFile = (f: File | null) => {
    setFileError(null);
    if (!f) return setFile(null);
    const ext = f.name.split('.').pop()?.toLowerCase() ?? '';
    if (!ALLOWED_EXT.includes(ext)) { setFileError(t('supply.fileType')); setFile(null); return; }
    if (f.size > MAX_BYTES) { setFileError(t('supply.fileTooLarge')); setFile(null); return; }
    setFile(f);
  };
  const submitDoc = async () => {
    // Previously any missing field put "required" on the file input, which pointed at the wrong
    // control when it was the document number or the issue date that was blank.
    const ok = docErrors.requireFields({ documentNumber: docNumber, issuedOn }, t('common.required'));
    if (!file) setFileError(t('common.required'));
    if (!file || !ok) return;
    setFileError(null);
    const fd = new FormData();
    fd.append('file', file);
    fd.append('type', docType);
    fd.append('poLineId', line.id);
    if (line.lotNumber) fd.append('lotNumber', line.lotNumber);
    fd.append('documentNumber', docNumber);
    fd.append('issuedOn', issuedOn);
    try {
      const d = await upload.mutateAsync(fd);
      toast.ok(t('supply.uploaded', { status: t(`status.doc.${d.status}`) }));
      setFile(null); setDocNumber(''); setIssuedOn('');
      docErrors.clear();
    } catch (e) {
      docErrors.fromApi(e, t('common.error'));
    }
  };

  return (
    <Drawer open={!!line} onClose={onClose} title={`${poCode} · ${t('supply.line')} ${line.lineNo} · ${line.partCode}`} wide>
      <dl className={s.meta}>
        <div className={s.metaItem}><dt>{t('supply.part')}</dt><dd>{line.partName}</dd></div>
        <div className={s.metaItem}><dt>{t('supply.qty')}</dt><dd>{line.quantity} {line.unit}</dd></div>
        <div className={s.metaItem}><dt>{t('supply.required')}</dt><dd>{fmtDate(line.requiredDate)}</dd></div>
        <div className={s.metaItem}><dt>{t('supply.eta')}</dt><dd>{fmtDate(line.eta)}</dd></div>
        <div className={s.metaItem}><dt>{t('supply.status')}</dt><dd><PoStatusChip status={line.status} small /></dd></div>
        <div className={s.metaItem}><dt>{t('supply.risk')}</dt><dd><RiskBadge category={line.risk.category} score={line.risk.score} small /></dd></div>
        <div className={s.metaItem}><dt>{t('supply.lotNumber')}</dt><dd>{line.lotNumber ?? '—'}</dd></div>
        <div className={s.metaItem}><dt>{t('supply.shipment')}</dt><dd>{line.shipmentCode ?? '—'}</dd></div>
      </dl>
      <div style={{ marginTop: 12 }}>
        <Tabs value={tab} onChange={setTab} items={[{ key: 'eta', label: t('supply.changeEta') }, { key: 'status', label: t('supply.updateStatus') }, { key: 'docs', label: t('supply.documents') }, { key: 'impact', label: t('supply.impact') }]} />
      </div>

      {tab === 'eta' && (
        <div className={s.section}>
          <form onSubmit={submitEta} noValidate data-testid="eta-form">
            <FormAlert message={etaErrors.formError} />
            <FormGrid>
              <FormField label={t('supply.newEta')} required error={etaErrors.fields.eta ?? (etaForm.formState.errors.eta && t('common.required'))}>{(id) => <DateInput id={id} invalid={!!etaForm.formState.errors.eta} disabled={!canEdit} {...etaForm.register('eta')} />}</FormField>
              <FormField label={t('supply.reason')} required error={etaErrors.fields.reason ?? (etaForm.formState.errors.reason && t('common.required'))}>
                {(id) => (
                  <Select id={id} disabled={!canEdit} {...etaForm.register('reason')}>
                    {ETA_REASONS.map((r) => <option key={r} value={r}>{t(`etaReason.${r}`)}</option>)}
                  </Select>
                )}
              </FormField>
              <FormField label={t('supply.comment')} full hint={t('common.optional')}>{(id) => <Textarea id={id} disabled={!canEdit} {...etaForm.register('comment')} />}</FormField>
            </FormGrid>
            <div className="row" style={{ justifyContent: 'flex-end', marginTop: 10 }}>
              <Button type="submit" variant="primary" loading={changeEta.isPending} disabled={!canEdit} data-testid="submit-eta">{t('supply.submitEta')}</Button>
            </div>
          </form>
          {etaResult && <RiskExplain risk={etaResult.res.risk} before={etaResult.before} endangered={etaResult.res.endangeredOrders} unit={line.unit} />}
          {!etaResult && <RiskExplain risk={line.risk} unit={line.unit} />}
        </div>
      )}

      {tab === 'status' && (
        <form className={s.section} onSubmit={submitStatus} noValidate data-testid="status-form">
          <FormAlert message={statusErrors.formError} />
          <FormGrid>
            <FormField label={t('supply.status')} required>{(id) => <Select id={id} disabled={!canEdit} {...statusForm.register('status')}>{PO_LINE_STATUSES.map((st) => <option key={st} value={st}>{t(`status.po.${st}`)}</option>)}</Select>}</FormField>
            <FormField label={`${t('supply.progress')} (%)`} error={statusForm.formState.errors.progressPercent?.message}>{(id) => <Input id={id} type="number" min={0} max={100} disabled={!canEdit} {...statusForm.register('progressPercent')} />}</FormField>
            <FormField label={t('supply.lotNumber')}>{(id) => <Input id={id} disabled={!canEdit} {...statusForm.register('lotNumber')} />}</FormField>
            <FormField label={t('supply.heatNumber')}>{(id) => <Input id={id} disabled={!canEdit} {...statusForm.register('heatNumber')} />}</FormField>
            <FormField label={t('supply.producedOn')}>{(id) => <DateInput id={id} disabled={!canEdit} {...statusForm.register('producedOn')} />}</FormField>
            <FormField label={t('supply.expiresOn')}>{(id) => <DateInput id={id} disabled={!canEdit} {...statusForm.register('expiresOn')} />}</FormField>
            <FormField label={`${t('supply.qty')} (${line.unit})`} error={statusForm.formState.errors.quantity?.message}>{(id) => <Input id={id} type="number" min={1} disabled={!canEdit} {...statusForm.register('quantity')} />}</FormField>
            <FormField label={t('supply.comment')}>{(id) => <Input id={id} disabled={!canEdit} {...statusForm.register('comment')} />}</FormField>
          </FormGrid>
          <div className="row" style={{ justifyContent: 'flex-end' }}>
            <Button type="submit" variant="primary" loading={patch.isPending} disabled={!canEdit}>{t('common.save')}</Button>
          </div>
        </form>
      )}

      {tab === 'docs' && (
        <div className={s.section}>
          <h3>{t('supply.documents')}</h3>
          {line.documents.length === 0 && <p className="muted">{t('supply.noDocuments')}</p>}
          {line.documents.map((d) => (
            <div key={d.id} className={s.docRow}>
              <span>
                <strong>{t(`docType.${d.type}`)}</strong> · {d.documentNumber ?? d.fileName}
                <br />
                <span className="muted" style={{ fontSize: 11 }}>{d.fileName} · {fmtBytes(d.sizeBytes)} · SHA-256 {d.sha256.slice(0, 12)}… · {fmtDateTime(d.uploadedAt)}</span>
              </span>
              <DocStatusChip status={d.status} small />
              <a href={`/api/v1/documents/${d.id}/download`} target="_blank" rel="noreferrer" style={{ fontSize: 'var(--fs-xs)' }}>PDF</a>
            </div>
          ))}
          {canEdit && (
            <div style={{ borderTop: '1px solid var(--border)', paddingTop: 10 }} data-testid="doc-upload">
              <h3 style={{ marginBottom: 8 }}>{t('supply.addDocument')}</h3>
              <FormAlert message={docErrors.formError} />
              <FormGrid>
                <FormField label={t('supply.docType')} required>{(id) => <Select id={id} value={docType} onChange={(e) => setDocType(e.target.value)}>{DOCUMENT_TYPES.map((d) => <option key={d} value={d}>{t(`docType.${d}`)}</option>)}</Select>}</FormField>
                <FormField label={t('supply.docNumber')} required error={docErrors.fields.documentNumber}>{(id) => <Input id={id} value={docNumber} onChange={(e) => setDocNumber(e.target.value)} invalid={!!docErrors.fields.documentNumber} />}</FormField>
                <FormField label={t('supply.issuedOn')} required error={docErrors.fields.issuedOn}>{(id) => <DateInput id={id} value={issuedOn} onChange={(e) => setIssuedOn(e.target.value)} invalid={!!docErrors.fields.issuedOn} />}</FormField>
                <FormField label={t('supply.file')} required hint={t('supply.fileHint')} error={fileError ?? docErrors.fields.file}>{(id) => <FileInput id={id} accept=".pdf,.png,.jpg,.jpeg" onChange={(e) => onFile(e.target.files?.[0] ?? null)} invalid={!!fileError || !!docErrors.fields.file} />}</FormField>
              </FormGrid>
              <div className="row" style={{ justifyContent: 'flex-end', marginTop: 10 }}>
                <Button variant="primary" icon={<Upload size={14} />} onClick={submitDoc} loading={upload.isPending} disabled={!file}>{t('supply.upload')}</Button>
              </div>
            </div>
          )}
        </div>
      )}

      {tab === 'impact' && (
        <div className={s.section}>
          <p className="muted" style={{ fontSize: 'var(--fs-xs)' }}>{t('supply.impactHint')}</p>
          {impact.isLoading && <p className="muted">{t('common.loading')}</p>}
          {impact.data && (
            <>
              <RiskExplain risk={impact.data.risk} endangered={impact.data.endangeredOrders} unit={line.unit} />
              <div className={s.metaItem}><dt>{t('kpi.PREDICTED_DOWNTIME_H')}</dt><dd>{impact.data.predictedDowntimeHours} h</dd></div>
            </>
          )}
        </div>
      )}
    </Drawer>
  );
}
