import { ArrowDownRight, ArrowUpRight, Minus, CheckCircle2, AlertTriangle, OctagonAlert, Info } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router';
import s from './ui.module.css';
import { Tooltip } from './Tooltip';
import type { Kpi } from '@/api/types';
import { fmtNumber, fmtSigned } from '@/lib/format';

const KPI_ROUTES: Record<Kpi['code'], string> = {
  MATERIAL_READINESS: '/planning',
  OTIF: '/supply',
  HIGH_RISK_DELIVERIES: '/supply?riskCategory=High',
  PREDICTED_DOWNTIME_H: '/planning',
  ORDER_ON_TIME: '/planning',
  PASSPORT_COMPLETENESS: '/passports',
};

/** True when a rising value is good (e.g. OTIF), false when rising is bad (downtime). */
const HIGHER_IS_BETTER: Record<Kpi['code'], boolean> = {
  MATERIAL_READINESS: true,
  OTIF: true,
  HIGH_RISK_DELIVERIES: false,
  PREDICTED_DOWNTIME_H: false,
  ORDER_ON_TIME: true,
  PASSPORT_COMPLETENESS: true,
};

export function formatKpiValue(kpi: Pick<Kpi, 'value' | 'unit'>): { value: string; unit: string } {
  switch (kpi.unit) {
    case '%':
      return { value: fmtNumber(kpi.value, kpi.value % 1 === 0 ? 0 : 1), unit: '%' };
    case 'h':
      return { value: fmtNumber(kpi.value, kpi.value % 1 === 0 ? 0 : 1), unit: 'h' };
    default:
      return { value: fmtNumber(kpi.value, 0), unit: '' };
  }
}

export function formatTrend(kpi: Pick<Kpi, 'trend' | 'unit'>): string {
  const digits = kpi.unit === 'count' ? 0 : 1;
  const v = fmtSigned(kpi.trend, kpi.trend % 1 === 0 ? 0 : digits);
  return kpi.unit === '%' ? `${v} pp` : kpi.unit === 'h' ? `${v} h` : v;
}

export function KpiTile({ kpi, pulse }: { kpi: Kpi; pulse?: boolean }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { value, unit } = formatKpiValue(kpi);
  const good = kpi.trend === 0 ? null : HIGHER_IS_BETTER[kpi.code] ? kpi.trend > 0 : kpi.trend < 0;
  const TrendIcon = kpi.trend > 0 ? ArrowUpRight : kpi.trend < 0 ? ArrowDownRight : Minus;
  const statusIcon =
    kpi.status === 'ok' ? (
      <CheckCircle2 size={16} color="var(--ok)" aria-label={t('status.generic.ok')} />
    ) : kpi.status === 'warn' ? (
      <AlertTriangle size={16} color="var(--warn)" aria-label={t('status.generic.warn')} />
    ) : (
      <OctagonAlert size={16} color="var(--crit)" aria-label={t('status.generic.critical')} />
    );
  const cls = [
    s.kpi,
    kpi.status === 'ok' && s.kpiOk,
    kpi.status === 'warn' && s.kpiWarn,
    kpi.status === 'critical' && s.kpiCritical,
    pulse && 'pulse',
  ]
    .filter(Boolean)
    .join(' ');
  return (
    <button type="button" className={cls} onClick={() => navigate(KPI_ROUTES[kpi.code])} data-testid={`kpi-${kpi.code}`}>
      <span className={s.kpiStatusIcon}>{statusIcon}</span>
      <span className={s.kpiLabel}>
        {t(`kpi.${kpi.code}`)}
        <Tooltip content={t(`kpi.def.${kpi.code}`)}>
          <Info size={12} aria-label={t('common.definition')} />
        </Tooltip>
      </span>
      <span className={s.kpiValue}>
        <span data-testid="kpi-value">{value}</span>
        {unit && <span className={s.kpiUnit}>{unit}</span>}
      </span>
      <span className={[s.kpiTrend, good === true && s.trendUp, good === false && s.trendDown].filter(Boolean).join(' ')} title={t('common.trendVsPrev')}>
        <TrendIcon size={13} aria-hidden />
        <span data-testid="kpi-trend">{kpi.trend === 0 ? t('common.noChange') : formatTrend(kpi)}</span>
        <span className="sr-only">{t('common.trendVsPrev')}</span>
      </span>
    </button>
  );
}
