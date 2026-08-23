import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router';
import { ArrowLeft, Check, X, Save, RefreshCw } from 'lucide-react';
import s from './planning.module.css';
import { useApproveScenario, useCompare, useRejectScenario, useRunScenario, useSaveScenario, useScenario } from './api';
import { CAN_APPROVE, CAN_RUN, ScenarioStatusChip } from './PlanningPage';
import { ScenarioResult, SolverBadge } from './ScenarioResult';
import { describeChange } from './ScenarioBuilder';
import { Button, ConfirmDialog, ErrorState, LoadingState, useToast } from '@/components/ui';
import { useAuth } from '@/features/auth/auth';
import { onDomainEvent } from '@/realtime/useLive';
import { fmtDateTime, fmtNumber } from '@/lib/format';

export function ScenarioDetailPage() {
  const { t } = useTranslation();
  const { id } = useParams<{ id: string }>();
  const toast = useToast();
  const { user } = useAuth();
  const scenario = useScenario(id);
  const done = scenario.data?.status === 'Completed' || scenario.data?.status === 'Approved' || scenario.data?.status === 'Saved' || scenario.data?.status === 'Rejected';
  const compare = useCompare(id, !!scenario.data?.after && done);
  const approve = useApproveScenario();
  const reject = useRejectScenario();
  const save = useSaveScenario();
  const run = useRunScenario();
  const [confirm, setConfirm] = useState(false);
  const canApprove = !!user && CAN_APPROVE.includes(user.role);
  const canRun = !!user && CAN_RUN.includes(user.role);

  useEffect(
    () =>
      onDomainEvent((e) => {
        if (e.name === 'PlanningScenarioCompleted' && String(e.payload.scenarioId ?? e.payload.id ?? '') === id) void scenario.refetch();
      }),
    [id, scenario],
  );

  const sc = scenario.data;
  const act = async (fn: () => Promise<unknown>, okKey: string) => {
    try {
      await fn();
      toast.ok(t(okKey));
    } catch (e) {
      toast.critical(t('common.error'), e instanceof Error ? e.message : undefined);
    }
  };

  return (
    <div className="page" data-testid="scenario-detail">
      <div className="page-header">
        <div>
          <Link to="/planning" className="row" style={{ fontSize: 'var(--fs-xs)' }}><ArrowLeft size={12} aria-hidden />{t('planning.backToList')}</Link>
          <h1>{sc?.name ?? t('planning.scenario')}</h1>
          {sc && (
            <p className="row" style={{ gap: 10 }}>
              <ScenarioStatusChip status={sc.status} small />
              <span data-testid="scenario-status" className="sr-only">{sc.status}</span>
              <span>{fmtDateTime(sc.createdAt)} · {sc.createdBy}</span>
              {sc.solver && <SolverBadge solver={sc.solver} elapsedMs={sc.elapsedMs} />}
            </p>
          )}
        </div>
        {sc && (
          <div className="row">
            {(sc.status === 'Draft' || sc.status === 'Failed' || sc.status === 'Saved') && canRun && (
              <Button icon={<RefreshCw size={14} />} onClick={() => void act(() => run.mutateAsync(sc.id), 'planning.runStarted')} loading={run.isPending} data-testid="btn-run-scenario">
                {t('planning.run')}
              </Button>
            )}
            {sc.status === 'Completed' && (
              <>
                <Button icon={<Save size={14} />} onClick={() => void act(() => save.mutateAsync(sc.id), 'planning.saved')} loading={save.isPending} disabled={!canRun} data-testid="btn-save-scenario">
                  {t('planning.save')}
                </Button>
                <Button variant="danger" icon={<X size={14} />} onClick={() => void act(() => reject.mutateAsync(sc.id), 'planning.rejected')} loading={reject.isPending} disabled={!canRun} data-testid="btn-reject-plan">
                  {t('planning.reject')}
                </Button>
                <Button variant="primary" icon={<Check size={14} />} onClick={() => setConfirm(true)} disabled={!canApprove} title={canApprove ? undefined : t('planning.approveForbidden')} data-testid="btn-approve-plan">
                  {t('planning.approve')}
                </Button>
              </>
            )}
          </div>
        )}
      </div>

      {scenario.isLoading && <LoadingState />}
      {scenario.isError && <ErrorState error={scenario.error} onRetry={() => scenario.refetch()} />}
      {sc && (
        <>
          <div className="row" style={{ fontSize: 'var(--fs-sm)' }} data-testid="scenario-changes">
            <span className="muted">{t('planning.changes')}:</span>
            {sc.changes.map((c, i) => <span key={i} style={{ background: 'var(--bg-2)', padding: '2px 8px', borderRadius: 4 }}>{describeChange(c, t)}</span>)}
          </div>
          {(sc.status === 'Running' || sc.status === 'Draft') && (
            <div className={s.running} role="status" aria-live="polite" data-testid="scenario-running">
              <div className={s.spinner} aria-hidden />
              <strong>{sc.status === 'Running' ? t('planning.running') : t('planning.draft')}</strong>
              <span className="muted">{t('planning.runningHint')}</span>
            </div>
          )}
          {sc.status === 'Failed' && <ErrorState error={new Error(sc.errorMessage ?? t('planning.failed'))} onRetry={canRun ? () => void run.mutateAsync(sc.id) : undefined} />}
          {done && sc.after && <ScenarioResult scenario={sc} compare={compare.data} />}
        </>
      )}

      <ConfirmDialog
        open={confirm}
        onClose={() => setConfirm(false)}
        onConfirm={() => { setConfirm(false); void act(() => approve.mutateAsync(sc!.id), 'planning.approved'); }}
        title={t('planning.approveTitle')}
        confirmLabel={t('planning.approve')}
        loading={approve.isPending}
        impact={
          sc?.kpiAfter ? (
            <ul style={{ margin: 0, paddingLeft: 16 }}>
              <li>{t('planning.impactMoved', { count: compare.data?.movedOperations.length ?? sc.kpiAfter.movedOperations })}</li>
              <li>{t('planning.impactLate', { count: sc.kpiAfter.lateOrders })}</li>
              <li>{t('planning.impactDowntime', { from: fmtNumber(sc.kpiBefore?.downtimeHours ?? 0), to: fmtNumber(sc.kpiAfter.downtimeHours) })}</li>
              <li>{t('planning.impactVersion')}</li>
            </ul>
          ) : undefined
        }
      >
        <p>{t('planning.approveBody')}</p>
      </ConfirmDialog>
    </div>
  );
}
