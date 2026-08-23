import { useTranslation } from 'react-i18next';
import { ArrowRight } from 'lucide-react';
import s from './supply.module.css';
import type { EndangeredOrder, RiskSummary } from '@/api/types';
import { RiskBadge, StatusChip } from '@/components/ui';
import { fmtDate, fmtNumber } from '@/lib/format';

export function RiskExplain({ risk, before, endangered, unit }: { risk: RiskSummary; before?: RiskSummary | null; endangered?: EndangeredOrder[]; unit?: string }) {
  const { t } = useTranslation();
  const top = [...risk.factors].sort((a, b) => b.contribution - a.contribution).slice(0, 3);
  const orders = endangered ?? risk.endangeredOrders ?? [];
  return (
    <div className={s.riskBox} data-testid="risk-explain">
      {before ? (
        <div className={s.riskCompare}>
          <div>
            <div className="muted" style={{ fontSize: 11 }}>{t('risk.before')}</div>
            <div className={s.riskScore}>{Math.round(before.score)}</div>
            <RiskBadge category={before.category} />
          </div>
          <ArrowRight size={20} aria-hidden />
          <div>
            <div className="muted" style={{ fontSize: 11 }}>{t('risk.after')}</div>
            <div className={s.riskScore} data-testid="risk-after-score">{Math.round(risk.score)}</div>
            <RiskBadge category={risk.category} />
          </div>
        </div>
      ) : (
        <div className="row" style={{ justifyContent: 'space-between' }}>
          <span className={s.riskScore}>{Math.round(risk.score)}</span>
          <RiskBadge category={risk.category} />
        </div>
      )}
      <div className="muted" style={{ fontSize: 11 }}>{t('app.ruleBased')}</div>
      <div>
        <h3>{t('risk.why')}</h3>
        <div className="muted" style={{ fontSize: 'var(--fs-xs)', marginBottom: 6 }}>{t('risk.topFactors')}</div>
        {top.map((f) => (
          <div key={f.code} className={s.factor}>
            <span>{t(`risk.factors.${f.code}`, { defaultValue: f.code })}</span>
            <span className={s.factorBar}><span style={{ width: `${Math.min(100, f.raw)}%` }} /></span>
            <span className="mono" title={`${t('risk.contribution')} ${fmtNumber(f.contribution, 1)}`}>+{fmtNumber(f.contribution, 1)}</span>
          </div>
        ))}
      </div>
      <div>
        <h3>{t('risk.endangeredOrders')}</h3>
        {orders.length === 0 ? (
          <StatusChip tone="ok" label={t('risk.noEndangered')} small />
        ) : (
          <ul style={{ margin: '6px 0 0', paddingLeft: 18, fontSize: 'var(--fs-sm)' }} data-testid="endangered-orders">
            {orders.map((o) => (
              <li key={o.orderCode}>
                <strong>{o.orderCode}</strong> · {t('risk.requiredOn', { date: fmtDate(o.requiredOn) })} · {t('risk.shortage', { qty: fmtNumber(o.shortage), unit: unit ?? t('common.pcs') })}
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}
