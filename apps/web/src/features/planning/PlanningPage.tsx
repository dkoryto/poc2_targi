import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router';
import { Play, Wand2, Clock3 } from 'lucide-react';
import s from './planning.module.css';
import { useBaseline, useCreateScenario, usePresets, useRunScenario, useScenarios } from './api';
import { describeChange, ScenarioBuilder } from './ScenarioBuilder';
import { SolverBadge } from './ScenarioResult';
import type { PlanningScenarioSummary, ScenarioChange, ScenarioStatus } from '@/api/types';
import { Button, Card, DataTable, ErrorState, LoadingState, StatusChip, type Column, useToast } from '@/components/ui';
import { Gantt } from '@/components/gantt/Gantt';
import { useAuth } from '@/features/auth/auth';
import { useSite } from '@/features/sites/sites';
import { Star } from 'lucide-react';
import { fmtDateTime, fmtNumber } from '@/lib/format';

export const CAN_RUN = ['ProductionPlanner', 'DemoPresenter', 'Administrator', 'OperationsDirector'];
export const CAN_APPROVE = ['ProductionPlanner', 'DemoPresenter'];

const STATUS_TONE: Record<ScenarioStatus, 'ok' | 'warn' | 'info' | 'neutral' | 'critical'> = { Draft: 'neutral', Running: 'info', Completed: 'info', Failed: 'critical', Approved: 'ok', Rejected: 'neutral', Saved: 'neutral' };
export function ScenarioStatusChip({ status, small }: { status: ScenarioStatus; small?: boolean }) {
  const { t } = useTranslation();
  return <StatusChip tone={STATUS_TONE[status] ?? 'neutral'} label={t(`planning.status.${status}`, { defaultValue: status })} small={small} />;
}

/**
 * The API sends `titleKey` either bare (`ACT40_DELAY`) or fully qualified
 * (`planning.presets.ACT40_DELAY`); resolve both, falling back to the raw value.
 */
function presetTitle(titleKey: string, t: (k: string, o?: Record<string, unknown>) => string): string {
  const qualified = titleKey.includes('.') ? titleKey : `planning.presets.${titleKey}`;
  const viaQualified = t(qualified, { defaultValue: '' });
  if (viaQualified) return viaQualified;
  return t(titleKey, { defaultValue: titleKey });
}

export function PlanningPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const toast = useToast();
  const { user } = useAuth();
  const { activeSite } = useSite();
  const baseline = useBaseline();
  const presets = usePresets();
  const scenarios = useScenarios();
  const create = useCreateScenario();
  const run = useRunScenario();
  const [builderOpen, setBuilderOpen] = useState(false);
  const [busyKey, setBusyKey] = useState<string | null>(null);
  const canRun = !!user && CAN_RUN.includes(user.role);

  const createAndRun = async (name: string, changes: ScenarioChange[], key: string) => {
    setBusyKey(key);
    try {
      const sc = await create.mutateAsync({ name, changes });
      await run.mutateAsync(sc.id);
      setBuilderOpen(false);
      navigate(`/planning/scenarios/${sc.id}`);
    } catch (e) {
      toast.critical(t('planning.runFailed'), e instanceof Error ? e.message : undefined);
    } finally {
      setBusyKey(null);
    }
  };

  const cols: Column<PlanningScenarioSummary>[] = [
    { key: 'name', header: t('planning.scenario'), render: (r) => <strong>{r.name}</strong>, sortValue: (r) => r.name },
    { key: 'status', header: t('supply.status'), render: (r) => <ScenarioStatusChip status={r.status} small />, sortValue: (r) => r.status },
    { key: 'created', header: t('planning.createdAt'), render: (r) => `${fmtDateTime(r.createdAt)} · ${r.createdBy}`, sortValue: (r) => r.createdAt },
    { key: 'changes', header: t('planning.changes'), align: 'right', render: (r) => r.changeCount, sortValue: (r) => r.changeCount },
    { key: 'solver', header: t('planning.solver'), render: (r) => (r.solver ? <SolverBadge solver={r.solver} /> : '—') },
    { key: 'downtime', header: t('planning.kpi.downtimeHours'), align: 'right', render: (r) => (r.kpiAfter ? `${fmtNumber(r.kpiAfter.downtimeHours)} h` : '—'), sortValue: (r) => r.kpiAfter?.downtimeHours ?? null },
  ];

  /**
   * Each plant has one headline scenario (`featured`, or the plant's `featuredScenarioKey`);
   * it is badged and sorted first so the presenter always starts from the right tile.
   */
  const isFeatured = (p: { key: string; featured?: boolean }) => p.featured === true || (!!activeSite?.featuredScenarioKey && p.key === activeSite.featuredScenarioKey);
  const orderedPresets = [...(presets.data ?? [])].sort((a, b) => Number(isFeatured(b)) - Number(isFeatured(a)));

  return (
    <div className="page" data-testid="planning-page">
      <div className="page-header">
        <div>
          <h1>{t('planning.title')}</h1>
          <p>{t('planning.subtitle')}</p>
        </div>
        <Button variant="primary" icon={<Wand2 size={14} />} onClick={() => setBuilderOpen(true)} disabled={!canRun} data-testid="btn-custom-scenario">
          {t('planning.customScenario')}
        </Button>
      </div>

      <Card title={t('planning.baseline')} definition={t('planning.baselineDef')}>
        {baseline.isLoading && <LoadingState rows={2} />}
        {baseline.isError && <ErrorState error={baseline.error} onRetry={() => baseline.refetch()} />}
        {baseline.data && (
          <div className="stack">
            <div className={s.baselineMeta} data-testid="baseline-meta">
              <span>{t('common.version')}: <strong>v{baseline.data.version}</strong></span>
              <span>{t('planning.approvedAt')}: <strong>{fmtDateTime(baseline.data.approvedAt)}</strong></span>
              <span>{t('planning.approvedBy')}: <strong>{baseline.data.approvedBy}</strong></span>
              <span>{t('planning.horizon')}: <strong>{baseline.data.gantt.horizonStart} → {baseline.data.gantt.horizonEnd}</strong></span>
            </div>
            <dl className={s.kpiStrip} style={{ margin: 0 }}>
              {(['downtimeHours', 'lateOrders', 'totalLatenessDays', 'ordersWithShortage', 'onTimeRate'] as const).map((k) => (
                <div key={k} className={s.miniKpi}>
                  <dt>{t(`planning.kpi.${k}`)}</dt>
                  <dd>{k === 'onTimeRate' ? `${fmtNumber(baseline.data.kpi[k] <= 1 ? baseline.data.kpi[k] * 100 : baseline.data.kpi[k])} %` : k === 'downtimeHours' ? `${fmtNumber(baseline.data.kpi[k])} h` : fmtNumber(baseline.data.kpi[k])}</dd>
                </div>
              ))}
            </dl>
            <Gantt data={baseline.data.gantt} compact onSelect={() => undefined} />
          </div>
        )}
      </Card>

      <Card title={t('planning.scenarios')} definition={t('planning.scenariosDef')}>
        {presets.isLoading && <LoadingState rows={2} />}
        {presets.isError && <ErrorState error={presets.error} onRetry={() => presets.refetch()} />}
        {presets.data && (
          <div className={s.tiles}>
            {orderedPresets.map((p) => (
              <button
                key={p.key}
                type="button"
                className={[s.tile, isFeatured(p) && s.tileFeatured].filter(Boolean).join(' ')}
                disabled={!canRun || busyKey !== null}
                onClick={() => void createAndRun(presetTitle(p.titleKey, t), p.changes, p.key)}
                data-testid={`scenario-tile-${p.key}`}
                title={canRun ? t('planning.runHint') : t('common.forbidden')}
              >
                <span className={s.tileTitle}>
                  {busyKey === p.key ? <Clock3 size={15} aria-hidden /> : <Play size={15} aria-hidden />}
                  {presetTitle(p.titleKey, t)}
                  {isFeatured(p) && (
                    <span className={s.featuredBadge} data-testid={`scenario-featured-${p.key}`}>
                      <Star size={11} aria-hidden />
                      {t('planning.featured')}
                    </span>
                  )}
                </span>
                <span className={s.tileChanges}>
                  {p.changes.map((c, i) => <span key={i}>{describeChange(c, t)}</span>)}
                </span>
              </button>
            ))}
            <button type="button" className={[s.tile, s.tileCustom].join(' ')} disabled={!canRun || busyKey !== null} onClick={() => setBuilderOpen(true)} data-testid="scenario-tile-custom">
              <span className={s.tileTitle}><Wand2 size={15} aria-hidden />{t('planning.customScenario')}</span>
              <span className={s.tileChanges}>{t('planning.customHint')}</span>
            </button>
          </div>
        )}
        {!canRun && <p className="muted" style={{ marginTop: 8, fontSize: 'var(--fs-xs)' }}>{t('planning.readOnly')}</p>}
      </Card>

      <Card title={t('planning.history')} flush>
        <DataTable columns={cols} rows={scenarios.data?.items} loading={scenarios.isLoading} error={scenarios.error} onRetry={() => scenarios.refetch()} rowKey={(r) => r.id} onRowClick={(r) => navigate(`/planning/scenarios/${r.id}`)} initialSort={{ key: 'created', dir: 'desc' }} emptyTitle={t('planning.noScenarios')} emptyDetail={t('planning.noScenariosDetail')} data-testid="scenario-list" />
      </Card>

      <ScenarioBuilder open={builderOpen} onClose={() => setBuilderOpen(false)} onSubmit={(name, changes) => void createAndRun(name, changes, 'custom')} baseline={baseline.data} submitting={busyKey === 'custom'} />
    </div>
  );
}
