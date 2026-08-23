import { useTranslation } from 'react-i18next';
import d from './demo.module.css';
import { Link } from 'react-router';
import { ShieldCheck, TimerOff, GitBranch, FileBadge } from 'lucide-react';
import { useScenarios, useScenario } from '@/features/planning/api';
import { useKpis } from '@/features/dashboard/api';
import { Card } from '@/components/ui';
import { fmtNumber } from '@/lib/format';

const FALLBACK = { downtimeBefore: 36, downtimeAfter: 8, riskDays: 10 };

export function SummaryPage() {
  const { t } = useTranslation();
  const scenarios = useScenarios();
  const latest = scenarios.data?.items.filter((s) => s.status === 'Completed' || s.status === 'Approved').sort((a, b) => b.createdAt.localeCompare(a.createdAt))[0];
  const sc = useScenario(latest?.id);
  const kpis = useKpis();
  const before = sc.data?.kpiBefore?.downtimeHours ?? FALLBACK.downtimeBefore;
  const after = sc.data?.kpiAfter?.downtimeHours ?? FALLBACK.downtimeAfter;
  const avoided = Math.max(0, before - after);
  const passportPct = kpis.data?.items.find((k) => k.code === 'PASSPORT_COMPLETENESS')?.value;
  const fromData = !!sc.data?.kpiAfter;
  const tiles = [
    { icon: ShieldCheck, color: 'var(--warn)', value: t('summary.daysEarlier', { count: FALLBACK.riskDays }), label: t('summary.riskDetected'), detail: t('summary.riskDetectedDetail') },
    { icon: TimerOff, color: 'var(--ok)', value: `${fmtNumber(avoided)} h`, label: t('summary.downtimeAvoided'), detail: t('summary.downtimeAvoidedDetail', { before: fmtNumber(before), after: fmtNumber(after) }) },
    { icon: GitBranch, color: 'var(--info)', value: '100 %', label: t('summary.traceability'), detail: t('summary.traceabilityDetail') },
    { icon: FileBadge, color: 'var(--ok)', value: t('summary.oneClick'), label: t('summary.passport'), detail: passportPct != null ? t('summary.passportDetail', { pct: fmtNumber(passportPct) }) : t('summary.passportDetailNoData') },
  ];
  return (
    <div className="page" data-testid="summary-page">
      <div className="page-header"><div><h1>{t('summary.title')}</h1><p>{t('summary.subtitle')}</p></div><Link to="/">{t('nav.controlRoom')} →</Link></div>
      <div className={d.tiles}>
        {tiles.map((x) => (
          <Card key={x.label}>
            <div className="stack" style={{ alignItems: 'flex-start', gap: 8, padding: 8 }}>
              <x.icon size={28} color={x.color} aria-hidden />
              <div className={d.tileValue}>{x.value}</div>
              <div style={{ fontWeight: 600 }}>{x.label}</div>
              <div className="muted" style={{ fontSize: 'var(--fs-sm)' }}>{x.detail}</div>
            </div>
          </Card>
        ))}
      </div>
      <Card title={t('summary.chain')}>
        <p style={{ fontSize: 'var(--fs-lg)', lineHeight: 1.6, margin: 0 }}>{t('summary.chainText')}</p>
      </Card>
      <p className="muted" style={{ fontSize: 'var(--fs-xs)' }}>{fromData ? t('summary.fromScenario', { name: latest?.name ?? '' }) : t('summary.demoValues')}</p>
    </div>
  );
}
