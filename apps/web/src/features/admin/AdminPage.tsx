import { useQuery } from '@tanstack/react-query';
import a from './admin.module.css';
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

/** camelCase key (older API shape) → canonical upper-snake code used for i18n lookups. */
function toCode(key: string): string {
  return key.replace(/([a-z0-9])([A-Z])/g, '$1_$2').toUpperCase();
}

/**
 * The API has returned these weights both as `{code, weight}[]` and as a plain
 * `{camelCaseKey: number}` object. Normalise either shape so a payload change can never
 * blank the page again. Aggregate keys such as `sum` are dropped.
 */
export function normaliseWeights(input: unknown, valueField: 'weight' | 'value'): { code: string; value: number }[] {
  if (Array.isArray(input)) {
    return input
      .filter((r): r is Record<string, unknown> => !!r && typeof r === 'object')
      .map((r) => ({
        code: String(r.code ?? ''),
        value: Number(r[valueField] ?? r.value ?? r.weight ?? 0),
      }))
      .filter((r) => r.code !== '');
  }
  if (input && typeof input === 'object') {
    return Object.entries(input as Record<string, unknown>)
      .filter(([k, v]) => typeof v === 'number' && k !== 'sum' && k !== 'total')
      .map(([k, v]) => ({ code: toCode(k), value: Number(v) }));
  }
  return [];
}

/** Thresholds may also arrive as a flat object of scalars. */
export function normaliseThresholds(settings: Record<string, unknown> | undefined): { code: string; value: number; unit?: string | null }[] {
  if (!settings) return [];
  if (Array.isArray(settings.thresholds)) {
    return settings.thresholds
      .filter((r): r is Record<string, unknown> => !!r && typeof r === 'object')
      .map((r) => ({ code: String(r.code ?? ''), value: Number(r.value ?? 0), unit: (r.unit as string | null | undefined) ?? null }))
      .filter((r) => r.code !== '');
  }
  const known: { key: string; code: string; unit?: string }[] = [
    { key: 'riskNotifyThreshold', code: 'RISK_NOTIFY_THRESHOLD' },
    { key: 'solverTimeLimitMs', code: 'SOLVER_TIME_LIMIT_MS', unit: 'ms' },
    { key: 'horizonWeeks', code: 'HORIZON_WEEKS' },
  ];
  return known
    .filter((k) => typeof settings[k.key] === 'number')
    .map((k) => ({ code: k.code, value: Number(settings[k.key]), unit: k.unit ?? null }));
}

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
          <div className={a.services} data-testid="service-cards">
            {status.data.services.map((s) => {
              const Icon = ICONS[s.name] ?? Radio;
              return (
                <div key={s.name} className={a.serviceCard} data-testid={`service-${s.name}`}>
                  <span className="row"><Icon size={16} aria-hidden /> <strong>{t(`admin.services.${s.name}`, { defaultValue: s.name })}</strong></span>
                  <StatusChip tone={s.status === 'up' ? 'ok' : s.status === 'down' ? 'critical' : 'neutral'} label={t(`status.service.${s.status}`)} small />
                  <span className="muted" style={{ fontSize: 11 }}>{s.latencyMs != null ? `${fmtNumber(s.latencyMs)} ms` : '—'}</span>
                </div>
              );
            })}
            <div className={a.serviceCard} data-testid="service-signalr">
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
            <dl className={a.demoList}>
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
          <div className={a.settings} data-testid="settings-tables">
            <SettingsTable
              title={t('admin.riskWeights')}
              rows={normaliseWeights(settings.data.riskWeights, 'weight').map((r) => ({ code: r.code, label: t(`risk.factors.${r.code}`, { defaultValue: r.code }), value: fmtNumber(r.value, 2) }))}
            />
            <SettingsTable
              title={t('admin.objectiveWeights')}
              rows={normaliseWeights(settings.data.objectiveWeights, 'value').map((r) => ({ code: r.code, label: t(`admin.objective.${r.code}`, { defaultValue: r.code }), value: fmtNumber(r.value, 1) }))}
            />
            <SettingsTable
              title={t('admin.thresholds')}
              rows={normaliseThresholds(settings.data as unknown as Record<string, unknown>).map((r) => ({
                code: r.code,
                label: t(`admin.threshold.${r.code}`, { defaultValue: r.code }),
                value: `${fmtNumber(r.value, Number.isInteger(r.value) ? 0 : 1)}${r.unit ? ` ${r.unit}` : ''}`,
              }))}
            />
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
      <table className={a.settingsTable}>
        <tbody>
          {rows.map((r) => <tr key={r.code} style={{ borderTop: '1px solid var(--border)' }}><td style={{ padding: '4px 6px' }}>{r.label}</td><td style={{ padding: '4px 6px', textAlign: 'right', fontVariantNumeric: 'tabular-nums' }}>{r.value}</td></tr>)}
        </tbody>
      </table>
    </div>
  );
}
