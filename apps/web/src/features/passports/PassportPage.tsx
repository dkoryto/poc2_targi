import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router';
import { ArrowLeft, Check, CheckCircle2, XCircle, FileDown, FilePlus2, ShieldAlert, GitBranch } from 'lucide-react';
import s from './passports.module.css';
import { useApprovePassport, useGeneratePassport, usePassport } from './api';
import { PassportStatusChip } from './PassportsPage';
import { Hash } from '@/features/trace/SerialPage';
import { nodeStatusTone } from '@/features/trace/GenealogyTree';
import type { MissingItem, PassportVersion } from '@/api/types';
import { ApiError, buildUrl, getToken } from '@/api/client';
import { Button, Card, ConfirmDialog, DataTable, ErrorState, LoadingState, StatusChip, Timeline, type Column, useToast } from '@/components/ui';
import { useAuth } from '@/features/auth/auth';
import { RecordSite } from '@/features/sites/SiteSwitch';
import { downloadFile } from '@/lib/download';
import { fmtBytes, fmtDateTime } from '@/lib/format';
import { useEffect } from 'react';

const CAN_QUALITY = ['QualityInspector', 'DemoPresenter'];

export function missingLabel(t: (k: string, o?: Record<string, unknown>) => string, m: MissingItem): string {
  return t(m.labelKey ?? `passports.missing.${m.code}`, { ...(m.params ?? {}), defaultValue: m.code });
}

function QrImage({ serial }: { serial: string }) {
  const [src, setSrc] = useState<string | null>(null);
  useEffect(() => {
    let url: string | null = null;
    let cancelled = false;
    const headers: Record<string, string> = {};
    const token = getToken();
    if (token) headers.Authorization = `Bearer ${token}`;
    fetch(buildUrl(`/passports/${encodeURIComponent(serial)}/qr`), { headers })
      .then((r) => (r.ok ? r.blob() : Promise.reject(new Error('qr'))))
      .then((b) => { if (!cancelled) { url = URL.createObjectURL(b); setSrc(url); } })
      .catch(() => setSrc(null));
    return () => { cancelled = true; if (url) URL.revokeObjectURL(url); };
  }, [serial]);
  if (!src) return <div className={s.qr} aria-hidden style={{ background: 'var(--bg-3)' }} />;
  return <img className={s.qr} src={src} alt={`QR ${serial}`} data-testid="passport-qr" />;
}

export function PassportPage() {
  const { t } = useTranslation();
  const { serial = '' } = useParams<{ serial: string }>();
  const toast = useToast();
  const { user } = useAuth();
  const q = usePassport(serial);
  const approve = useApprovePassport(serial);
  const generate = useGeneratePassport(serial);
  const [confirmGen, setConfirmGen] = useState(false);
  const [missingFromApi, setMissingFromApi] = useState<MissingItem[] | null>(null);
  const p = q.data;
  const canQuality = !!user && CAN_QUALITY.includes(user.role);
  const complete = p?.completeness.complete ?? false;
  const canApprove = canQuality && complete && (p?.status === 'Draft' || p?.status === 'PendingReview');
  const canGenerate = canQuality && complete && (p?.status === 'Approved' || p?.status === 'Generated');

  const doGenerate = async () => {
    setConfirmGen(false);
    try {
      const r = await generate.mutateAsync();
      setMissingFromApi(null);
      toast.ok(t('passports.generated', { version: r.version }), `SHA-256 ${r.sha256.slice(0, 16)}…`);
    } catch (e) {
      if (e instanceof ApiError && e.status === 422 && e.problem?.missing) {
        setMissingFromApi(e.problem.missing);
        toast.warn(t('passports.incomplete'));
      } else toast.critical(t('common.error'), e instanceof Error ? e.message : undefined);
    }
  };
  const doApprove = async () => {
    try {
      await approve.mutateAsync();
      toast.ok(t('passports.approved'));
    } catch (e) {
      toast.critical(t('common.error'), e instanceof Error ? e.message : undefined);
    }
  };
  const missing = missingFromApi ?? p?.completeness.missing ?? [];

  const verCols: Column<PassportVersion>[] = [
    { key: 'v', header: t('common.version'), render: (r) => <strong>v{r.version}</strong>, sortValue: (r) => r.version, card: 'title' },
    { key: 'at', header: t('passports.generatedAt'), render: (r) => `${fmtDateTime(r.generatedAt)} · ${r.generatedBy}`, sortValue: (r) => r.generatedAt },
    { key: 'sha', header: 'SHA-256', render: (r) => <Hash value={r.sha256} /> },
    { key: 'size', header: t('passports.size'), align: 'right', render: (r) => fmtBytes(r.fileSize) },
    { key: 'status', header: t('supply.status'), render: (r) => <StatusChip tone={r.status === 'Current' ? 'ok' : r.status === 'Invalidated' ? 'critical' : 'neutral'} label={t(`passports.version.${r.status}`)} small />, card: 'meta' },
    { key: 'dl', header: '', render: (r) => <Button size="sm" icon={<FileDown size={13} />} onClick={() => void downloadFile(`/passports/${encodeURIComponent(serial)}/versions/${r.version}/pdf`, `passport-${serial}-v${r.version}.pdf`, { openInNewTab: true }).catch(() => toast.critical(t('common.error')))} data-testid={`passport-pdf-${r.version}`}>PDF</Button> },
  ];

  return (
    <div className="page" data-testid="passport-page">
      <div className="page-header">
        <div>
          <Link to="/passports" className={["row", s.backLink].join(" ")} style={{ fontSize: 'var(--fs-xs)' }}><ArrowLeft size={12} aria-hidden />{t('passports.title')}</Link>
          <h1 className="mono">{serial}</h1>
          {p && <p>{p.productName ?? p.productCode} · {t('gantt.order')} {p.orderCode} · {t('passports.template')} <span className="mono">{p.templateCode}</span>{p.bomVersion ? ` · BOM ${p.bomVersion}` : ''}</p>}
          {/* The QR on the printed passport links straight to this page, so the plant must be named here. */}
          {p?.siteCode && <RecordSite code={p.siteCode} />}
        </div>
        {p && (
          <div className="row">
            <span data-testid="passport-status"><PassportStatusChip status={p.status} /></span>
            <Link to={`/trace/serials/${encodeURIComponent(serial)}`} className="row" style={{ fontSize: 'var(--fs-sm)' }}><GitBranch size={13} aria-hidden />{t('passports.openTrace')}</Link>
            {canQuality && (
              <Button icon={<Check size={14} />} onClick={() => void doApprove()} disabled={!canApprove} loading={approve.isPending} title={!complete ? t('passports.incomplete') : undefined} data-testid="btn-approve-passport">
                {t('passports.approve')}
              </Button>
            )}
            <Button variant="primary" icon={<FilePlus2 size={14} />} onClick={() => setConfirmGen(true)} disabled={!canGenerate} loading={generate.isPending} title={!complete ? t('passports.incomplete') : !canQuality ? t('common.forbidden') : p.status !== 'Approved' && p.status !== 'Generated' ? t('passports.approveFirst') : undefined} data-testid="btn-generate-passport">
              {t('passports.generate')}
            </Button>
          </div>
        )}
      </div>
      {q.isLoading && <LoadingState />}
      {q.isError && <ErrorState error={q.error} onRetry={() => q.refetch()} />}
      {p && (
        <>
          {p.status === 'Invalidated' && (
            <div className={s.banner} role="alert" data-testid="passport-invalidated">
              <ShieldAlert size={18} color="var(--crit)" aria-hidden />
              <div><strong>{t('passports.invalidatedTitle')}</strong><div className="muted" style={{ fontSize: 'var(--fs-sm)' }}>{p.invalidationReason ?? t('passports.invalidatedDefault')}{p.invalidatedAt ? ` · ${fmtDateTime(p.invalidatedAt)}` : ''}</div></div>
            </div>
          )}
          <p className="muted" style={{ fontSize: 'var(--fs-xs)', margin: 0 }}>{t('passports.demoNote')}</p>
          <div className={s.layout}>
            <div className="stack">
              <Card title={t('passports.completeness')} definition={t('passports.completenessDef')} data-testid="passport-completeness">
                {missing.length > 0 ? (
                  <div className={s.missingBox} role="status" data-testid="passport-missing">
                    <strong className="row"><XCircle size={14} aria-hidden />{t('passports.missingHeader')}</strong>
                    <ul>{missing.map((m, i) => <li key={`${m.code}-${i}`}>{missingLabel(t, m)}</li>)}</ul>
                  </div>
                ) : (
                  <div className="row" style={{ color: 'var(--ok)' }} data-testid="passport-complete"><CheckCircle2 size={16} aria-hidden /> {t('passports.allRequirementsMet')}</div>
                )}
                <ul className={s.reqList} style={{ marginTop: 10 }}>
                  {p.completeness.requirements.map((r) => (
                    <li key={r.code} className={[s.req, !r.satisfied && s.reqMissing].filter(Boolean).join(' ')} data-testid={`passport-req-${r.code}`}>
                      {r.satisfied ? <CheckCircle2 size={15} color="var(--ok)" aria-label={t('common.yes')} /> : <XCircle size={15} color="var(--crit)" aria-label={t('common.no')} />}
                      <span>{t(`passports.req.${r.code}`, { defaultValue: r.code })}</span>
                      <span className={s.evidence}>{r.evidence ?? ''}</span>
                    </li>
                  ))}
                </ul>
              </Card>
              <Card title={t('passports.components')} flush>
                <DataTable
                  columns={[
                    { key: 'part', header: t('supply.part'), render: (r) => <span className="mono">{r.partCode}</span>, sortValue: (r) => r.partCode, card: 'title' },
                    { key: 'lot', header: t('trace.lot'), render: (r) => <Link to={`/trace/lots/${encodeURIComponent(r.lotNumber)}`} className="mono">{r.lotNumber}</Link> },
                    { key: 'sup', header: t('supply.supplier'), render: (r) => `${r.supplierName ?? r.supplierCode}${r.country ? ` (${r.country})` : ''}` },
                    { key: 'cert', header: t('trace.certificate'), render: (r) => <Hash value={r.certSha256} /> },
                  ]}
                  rows={p.components}
                  rowKey={(r) => `${r.partCode}-${r.lotNumber}`}
                  emptyTitle={t('trace.noComponents')}
                />
              </Card>
              <div className="grid-2">
                <Card title={t('trace.inspections')}>
                  {p.inspections.length === 0 ? <p className="muted">{t('trace.noInspections')}</p> : <Timeline items={p.inspections.map((i) => ({ id: i.id, at: i.inspectedAt, who: i.inspector, title: <StatusChip tone={nodeStatusTone(i.result)} label={t(`status.inspection.${i.result}`)} small />, body: i.notes }))} />}
                </Card>
                <Card title={t('passports.deviations')}>
                  {p.deviations.length === 0 ? <p className="muted">{t('passports.noDeviations')}</p> : (
                    <ul className="stack" style={{ listStyle: 'none', margin: 0, padding: 0, gap: 6, fontSize: 'var(--fs-sm)' }}>
                      {p.deviations.map((d) => <li key={d.id} className="row" style={{ justifyContent: 'space-between' }}><span>{d.code ? <span className="mono">{d.code} </span> : null}{d.title}</span><span className="muted">{d.status}{d.approvedBy ? ` · ${d.approvedBy}` : ''}</span></li>)}
                    </ul>
                  )}
                </Card>
              </div>
            </div>
            <div className="stack">
              <Card title={t('passports.qr')} definition={t('passports.qrDef')}>
                <div className="row" style={{ alignItems: 'flex-start' }}>
                  <QrImage serial={serial} />
                  <dl className={s.meta} style={{ gridTemplateColumns: '1fr', flex: 1 }}>
                    <div className={s.metaItem}><dt>{t('passports.approvedBy')}</dt><dd>{p.approvedBy ?? '—'}{p.approvedAt ? <div className="muted" style={{ fontWeight: 400, fontSize: 11 }}>{fmtDateTime(p.approvedAt)}</div> : null}</dd></div>
                    <div className={s.metaItem}><dt>{t('passports.latestVersion')}</dt><dd>{p.versions.length > 0 ? `v${Math.max(...p.versions.map((v) => v.version))}` : '—'}</dd></div>
                  </dl>
                </div>
              </Card>
              <Card title={t('passports.versions')} flush>
                <DataTable columns={verCols} rows={p.versions} rowKey={(r) => String(r.version)} initialSort={{ key: 'v', dir: 'desc' }} emptyTitle={t('passports.noVersions')} emptyDetail={t('passports.noVersionsDetail')} data-testid="passport-versions" />
              </Card>
            </div>
          </div>
        </>
      )}
      <ConfirmDialog open={confirmGen} onClose={() => setConfirmGen(false)} onConfirm={() => void doGenerate()} title={t('passports.generateTitle')} confirmLabel={t('passports.generate')} loading={generate.isPending} impact={<span>{t('passports.generateImpact', { version: (p?.versions.length ?? 0) + 1 })}</span>}>
        <p>{t('passports.generateBody')}</p>
      </ConfirmDialog>
    </div>
  );
}
