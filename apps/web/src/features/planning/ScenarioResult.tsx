import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ArrowDownRight, ArrowUpRight, Minus, Cpu, AlertTriangle, Clock, Info, CheckCircle2, OctagonAlert } from 'lucide-react';
import s from './planning.module.css';
import type { Consequence, Explanation, GanttData, PlanKpi, PlanningScenario, ScenarioCompare } from '@/api/types';
import { Card, DataTable, SegmentedControl, StatusChip, type Column } from '@/components/ui';
import { Gantt } from '@/components/gantt/Gantt';
import { explainText } from '@/lib/explain';
import { fmtDateTime, fmtNumber, fmtSigned } from '@/lib/format';

type KpiKey = keyof PlanKpi;
const KPI_ORDER: { key: KpiKey; lowerIsBetter: boolean; unit: 'h' | 'd' | '' | '%'; testId?: string }[] = [
  { key: 'downtimeHours', lowerIsBetter: true, unit: 'h', testId: 'kpi-delta-downtime' },
  { key: 'lateOrders', lowerIsBetter: true, unit: '' },
  { key: 'totalLatenessDays', lowerIsBetter: true, unit: 'd' },
  { key: 'movedOperations', lowerIsBetter: true, unit: '', testId: 'kpi-delta-moved' },
  { key: 'onTimeRate', lowerIsBetter: false, unit: '%' },
];

function kpiVal(v: number, unit: string): string {
  if (unit === '%') return `${fmtNumber(v <= 1 ? v * 100 : v, 0)} %`;
  return `${fmtNumber(v, Number.isInteger(v) ? 0 : 1)}${unit ? ` ${unit}` : ''}`;
}

export function KpiCompare({ before, after }: { before: PlanKpi; after: PlanKpi }) {
  const { t } = useTranslation();
  return (
    <div className={s.kpiCompare} data-testid="kpi-compare">
      {KPI_ORDER.map((k) => {
        const b = before[k.key] ?? 0;
        const a = after[k.key] ?? 0;
        const rawDelta = k.unit === '%' && a <= 1 ? (a - b) * 100 : a - b;
        const good = rawDelta === 0 ? null : k.lowerIsBetter ? rawDelta < 0 : rawDelta > 0;
        const Icon = rawDelta > 0 ? ArrowUpRight : rawDelta < 0 ? ArrowDownRight : Minus;
        return (
          <div key={k.key} className={s.kpiCard} data-testid={k.testId}>
            <h4>{t(`planning.kpi.${k.key}`)}</h4>
            <div className={s.kpiVals}>
              <span className={s.kpiBefore} aria-label={t('risk.before')}>{kpiVal(b, k.unit)}</span>
              <span className={s.kpiAfter} aria-label={t('risk.after')} data-testid="kpi-after-value">{kpiVal(a, k.unit)}</span>
            </div>
            <span className={[s.delta, good === true && s.deltaGood, good === false && s.deltaBad, good === null && s.deltaNeutral].filter(Boolean).join(' ')}>
              <Icon size={12} aria-hidden />
              {rawDelta === 0 ? t('common.noChange') : `${fmtSigned(rawDelta, Number.isInteger(rawDelta) ? 0 : 1)}${k.unit === '%' ? ' pp' : k.unit ? ` ${k.unit}` : ''}`}
            </span>
          </div>
        );
      })}
    </div>
  );
}

const TONE: Record<string, string> = {
  ORDER_DELAYED_MATERIAL_SHORTAGE: s.explanationWarn!,
  ORDER_LATE_DUE: s.explanationCrit!,
  ORDER_PULLED_FORWARD: s.explanationOk!,
  DOWNTIME_REDUCED: s.explanationOk!,
  CAPACITY_REDUCED: s.explanationWarn!,
  FALLBACK_USED: s.explanationWarn!,
};

export function Explanations({ items }: { items: Explanation[] }) {
  const { t } = useTranslation();
  if (items.length === 0) return <p className="muted">{t('planning.noExplanations')}</p>;
  return (
    <ol className={s.explanations} aria-label={t('planning.explanations')}>
      {items.map((e, i) => (
        <li key={`${e.reasonCode}-${e.orderCode}-${i}`} className={[s.explanation, TONE[e.reasonCode]].filter(Boolean).join(' ')} data-testid={`explanation-${e.reasonCode}`}>
          <span className="mono muted" style={{ fontSize: 11, paddingTop: 2 }}>{i + 1}.</span>
          <span>
            {explainText(t, e.reasonCode, e.params)}
            {e.orderCode && <span className="muted" style={{ marginLeft: 6, fontSize: 11 }}>[{e.orderCode}]</span>}
          </span>
        </li>
      ))}
    </ol>
  );
}

export function Consequences({ items }: { items: Consequence[] }) {
  const { t } = useTranslation();
  if (items.length === 0) return null;
  const icon = { info: <Info size={14} color="var(--info)" aria-hidden />, warn: <AlertTriangle size={14} color="var(--warn)" aria-hidden />, critical: <OctagonAlert size={14} color="var(--crit)" aria-hidden /> };
  return (
    <ul className="stack" style={{ gap: 6, paddingLeft: 0, listStyle: 'none', margin: 0 }} aria-label={t('planning.consequences')}>
      {items.map((c, i) => (
        <li key={i} className="row" style={{ fontSize: 'var(--fs-sm)', flexWrap: 'nowrap' }}>
          {icon[c.kind]}
          <span>{c.text ?? (c.textKey ? t(c.textKey, { ...(c.params ?? {}), defaultValue: c.textKey }) : '')}</span>
        </li>
      ))}
    </ul>
  );
}

export function SolverBadge({ solver, elapsedMs }: { solver?: string | null; elapsedMs?: number | null }) {
  const { t } = useTranslation();
  const fallback = (solver ?? '').toLowerCase().includes('fallback');
  return (
    <span className="row" data-testid="solver-badge">
      <StatusChip tone={fallback ? 'warn' : 'ok'} icon={<Cpu size={13} aria-hidden />} label={fallback ? t('planning.fallback') : solver || t('planning.solver')} title={fallback ? t('planning.fallbackHint') : undefined} />
      {elapsedMs != null && (
        <StatusChip tone="neutral" icon={<Clock size={13} aria-hidden />} label={t('planning.elapsed', { ms: fmtNumber(elapsedMs) })} small />
      )}
    </span>
  );
}

type View = 'before' | 'after' | 'compare';

export function ScenarioResult({ scenario, compare }: { scenario: PlanningScenario; compare?: ScenarioCompare | null }) {
  const { t } = useTranslation();
  const [view, setView] = useState<View>('compare');
  const before = scenario.before ?? null;
  const after = scenario.after ?? null;
  const gantt: { data: GanttData; compare?: { before: GanttData } } | null = useMemo(() => {
    if (view === 'before' && before) return { data: before };
    if (view === 'after' && after) return { data: after };
    if (after && before) return { data: after, compare: { before } };
    if (after) return { data: after };
    return before ? { data: before } : null;
  }, [view, before, after]);

  const moved = compare?.movedOperations ?? [];
  const cols: Column<(typeof moved)[number]>[] = [
    { key: 'op', header: t('gantt.op'), render: (r) => <span className="mono">{r.operationCode}</span>, sortValue: (r) => r.operationCode },
    { key: 'wc', header: t('gantt.workCenter'), render: (r) => r.workCenterCode, sortValue: (r) => r.workCenterCode },
    { key: 'before', header: t('risk.before'), render: (r) => `${fmtDateTime(r.before.start)} – ${fmtDateTime(r.before.end)}` },
    { key: 'after', header: t('risk.after'), render: (r) => `${fmtDateTime(r.after.start)} – ${fmtDateTime(r.after.end)}` },
    { key: 'shift', header: t('gantt.shift'), align: 'right', render: (r) => <span style={{ color: r.shiftDays > 0 ? 'var(--warn)' : 'var(--ok)' }}>{fmtSigned(r.shiftDays, 1)} d</span>, sortValue: (r) => r.shiftDays },
  ];

  return (
    <div className="stack" data-testid="scenario-result">
      {scenario.kpiBefore && scenario.kpiAfter && <KpiCompare before={scenario.kpiBefore} after={scenario.kpiAfter} />}
      <Card
        title={t('planning.ganttCompare')}
        definition={t('planning.ganttCompareDef')}
        actions={
          <SegmentedControl<View>
            label={t('planning.view')}
            value={view}
            onChange={setView}
            options={[
              { value: 'before', label: t('gantt.compareBefore') },
              { value: 'after', label: t('gantt.compareAfter') },
              { value: 'compare', label: t('planning.compare') },
            ]}
          />
        }
      >
        {gantt ? <Gantt data={gantt.data} compare={gantt.compare} /> : <p className="muted">{t('gantt.noOps')}</p>}
      </Card>
      <div className={s.twoCol}>
        <Card title={t('planning.explanations')} definition={t('planning.explanationsDef')}>
          {scenario.changes?.some((c) => c.type === 'BLOCK_LOT') && (
            <p className={s.simulationNote} data-testid="simulation-note">
              <Info size={13} aria-hidden /> {t('planning.simulationNote')}
            </p>
          )}
          <Explanations items={scenario.explanations ?? []} />
          {scenario.consequences && scenario.consequences.length > 0 && (
            <>
              <h3 style={{ marginTop: 12 }}>{t('planning.consequences')}</h3>
              <Consequences items={scenario.consequences} />
            </>
          )}
        </Card>
        <Card
          title={t('planning.movedOps', { count: moved.length })}
          definition={t('planning.movedOpsDef')}
          flush
        >
          <DataTable
            columns={cols}
            rows={moved}
            rowKey={(r) => r.operationCode}
            emptyTitle={t('planning.noMovedTitle')}
            emptyDetail={t('planning.noMovedDetail')}
            initialSort={{ key: 'shift', dir: 'desc' }}
            maxHeight={360}
            data-testid="moved-ops"
          />
          {typeof scenario.changesVsBaseline === 'number' && (
            <p className={s.vsBaseline} data-testid="changes-vs-baseline">
              {t('planning.changesVsBaseline', { count: scenario.changesVsBaseline })}
            </p>
          )}
        </Card>
      </div>
      {scenario.status === 'Approved' && (
        <div className="row" style={{ color: 'var(--ok)', fontSize: 'var(--fs-sm)' }}>
          <CheckCircle2 size={14} aria-hidden /> {t('planning.approvedInfo', { by: scenario.approvedBy ?? '', at: fmtDateTime(scenario.approvedAt), version: scenario.baselineVersion ?? '' })}
        </div>
      )}
    </div>
  );
}
