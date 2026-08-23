import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Database, HardDrive, Cpu, Brain, Radio, AlertOctagon } from 'lucide-react';
import { api } from '@/api/client';
import { keys } from '@/api/keys';
import type { AdminSettings, AdminStatus } from '@/api/types';
import { Card, ErrorState, LoadingState, StatusChip } from '@/components/ui';
import { useDemoStatus } from '@/features/demo/api';
import { useLiveStatus } from '@/components/layout/AppShell';
import { fmtDateTime, fmtNumber } from '@/lib/format';

const ICONS: Record<string, typeof Database> = { postgres: Database, minio: HardDrive, 'planning-engine': Cpu, 'local-ai': Brain };

export function useAdminStatusLive() {
  return useQuery({ queryKey: keys.admin.status, queryFn: () => api.get<AdminStatus>('/admin/status'), refetchInterval: 10_000 });
}
export function useAdminSettings() {
  return useQuery({ queryKey: keys.admin.settings, queryFn: () => api.get<AdminSettings>('/admin/settings'), staleTime: 60_000 });
}

export function AdminPage() {
  const { t } = useTranslation();
  const status = useAdminStatusLive();
  const settings = useAdminSettings();
  const demo = useDemoStatus();
  const live = useLiveStatus();
  return (
    <div className="page" data-testid="admin-page">
      <div className="page-header"><div><h1>{t('admin.title')}</h1><p>{t('admin.subtitle')}</p></div></div>
      <Card title={t('admin.status')} definition={t('admin.statusDef')}>
        {status.isLoading && <LoadingState rows={2} />}
        {status.isError && <ErrorState error={status.error} onRetry={() => status.refetch()} />}
        {status.data && (
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(5, minmax(0, 1fr))', gap: 10 }} data-testid="service-cards">
            {status.data.services.map((s) => {
              const Icon = ICONS[s.name] ?? Radio;
              return (
                <div key={s.name} style={{ border: '1px solid var(--border)', borderRadius: 6, padding: 12, background: 'var(--bg-2)', display: 'flex', flexDirection: 'column', gap: 6 }} data-testid={`service-${s.name}`}>
                  <span className="row"><Icon size={16} aria-hidden /> <strong>{t(`admin.services.${s.name}`, { defaultValue: s.name })}</strong></span>
                  <StatusChip tone={s.status === 'up' ? 'ok' : s.status === 'down' ? 'critical' : 'neutral'} label={t(`status.service.${s.status}`)} small />
                  <span className="muted" style={{ fontSize: 11 }}>{s.latencyMs != null ? `${fmtNumber(s.latencyMs)} ms` : '—'}</span>
                </div>
              );
            })}
            <div style={{ border: '1px solid var(--border)', borderRadius: 6, padding: 12, background: 'var(--bg-2)', display: 'flex', flexDirection: 'column', gap: 6 }} data-testid="service-signalr">
              <span className="row"><Radio size={16} aria-hidden /> <strong>SignalR</strong></span>
              <StatusChip tone={live === 'connected' ? 'ok' : live === 'connecting' ? 'warn' : 'critical'} label={t(`admin.live.${live}`)} small />
            </div>
          </div>
        )}
      </Card>
      <div className="grid-2">
        <Card title={t('admin.recentErrors')} definition={t('admin.recentErrorsDef')}>
          {status.data && status.data.recentErrors.length === 0 && <p className="muted">{t('admin.noErrors')}</p>}
          {status.data && status.data.recentErrors.length > 0 && (
            <ul className="stack" style={{ listStyle: 'none', margin: 0, padding: 0, gap: 6, fontSize: 'var(--fs-sm)' }}>
              {status.data.recentErrors.map((e, i) => <li key={i} className="row" style={{ flexWrap: 'nowrap', alignItems: 'flex-start' }}><AlertOctagon size={14} color="var(--crit)" aria-hidden style={{ marginTop: 2 }} /><span><span className="muted">{fmtDateTime(e.at)} · {e.operation}</span><br />{e.message}</span></li>)}
            </ul>
          )}
        </Card>
        <Card title={t('admin.demoStatus')}>
          {demo.data ? (
            <dl style={{ margin: 0, display: 'grid', gridTemplateColumns: 'auto 1fr', gap: '4px 12px', fontSize: 'var(--fs-sm)' }}>
              <dt className="muted">{t('admin.demoMode')}</dt><dd style={{ margin: 0 }}>{demo.data.demoMode ? t('common.yes') : t('common.no')}</dd>
              <dt className="muted">{t('demo.seedVersion')}</dt><dd style={{ margin: 0 }} className="mono">{demo.data.seedVersion}</dd>
              <dt className="muted">{t('admin.seededAt')}</dt><dd style={{ margin: 0 }}>{fmtDateTime(demo.data.seededAt)}</dd>
              <dt className="muted">{t('admin.lastReset')}</dt><dd style={{ margin: 0 }}>{demo.data.lastResetMs != null ? `${fmtNumber(demo.data.lastResetMs)} ms` : '—'}</dd>
            </dl>
          ) : <LoadingState rows={2} />}
        </Card>
      </div>
      <Card title={t('admin.settings')} definition={t('admin.settingsDef')}>
        {settings.isLoading && <LoadingState rows={3} />}
        {settings.isError && <ErrorState error={settings.error} onRetry={() => settings.refetch()} />}
        {settings.data && (
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, minmax(0, 1fr))', gap: 12 }} data-testid="settings-tables">
            <SettingsTable title={t('admin.riskWeights')} rows={settings.data.riskWeights.map((r) => ({ code: r.code, label: t(`risk.factors.${r.code}`, { defaultValue: r.code }), value: fmtNumber(r.weight, 2) }))} />
            <SettingsTable title={t('admin.objectiveWeights')} rows={settings.data.objectiveWeights.map((r) => ({ code: r.code, label: t(`admin.objective.${r.code}`, { defaultValue: r.code }), value: fmtNumber(r.value, 1) }))} />
            <SettingsTable title={t('admin.thresholds')} rows={settings.data.thresholds.map((r) => ({ code: r.code, label: t(`admin.threshold.${r.code}`, { defaultValue: r.code }), value: `${fmtNumber(r.value, Number.isInteger(r.value) ? 0 : 1)}${r.unit ? ` ${r.unit}` : ''}` }))} />
          </div>
        )}
        <p className="muted" style={{ fontSize: 'var(--fs-xs)', marginTop: 8 }}>{t('admin.readOnly')}</p>
      </Card>
    </div>
  );
}

function SettingsTable({ title, rows }: { title: string; rows: { code: string; label: string; value: string }[] }) {
  return (
    <div>
      <h3 style={{ marginBottom: 6 }}>{title}</h3>
      <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 'var(--fs-sm)' }}>
        <tbody>
          {rows.map((r) => <tr key={r.code} style={{ borderTop: '1px solid var(--border)' }}><td style={{ padding: '4px 6px' }}>{r.label}</td><td style={{ padding: '4px 6px', textAlign: 'right', fontVariantNumeric: 'tabular-nums' }}>{r.value}</td></tr>)}
        </tbody>
      </table>
    </div>
  );
}
