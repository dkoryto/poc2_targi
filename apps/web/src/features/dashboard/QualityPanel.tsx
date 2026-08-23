import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import s from './dashboard.module.css';
import type { QualityStatus } from '@/api/types';
import { StatusChip } from '@/components/ui';

export function QualityPanel({ data }: { data: QualityStatus }) {
  const { t } = useTranslation();
  const p = data.passports;
  const d = data.documents;
  const pTotal = p.draft + p.pendingReview + p.approved + p.generated + p.invalidated || 1;
  const dTotal = d.pending + d.verifying + d.accepted + d.rejected + d.requiresCompletion || 1;
  const pSegs = [
    { k: 'Generated', v: p.generated, c: 'var(--ok)' },
    { k: 'Approved', v: p.approved, c: '#34d399' },
    { k: 'PendingReview', v: p.pendingReview, c: 'var(--info)' },
    { k: 'Draft', v: p.draft, c: 'var(--fg-3)' },
    { k: 'Invalidated', v: p.invalidated, c: 'var(--crit)' },
  ];
  const dSegs = [
    { k: 'Accepted', v: d.accepted, c: 'var(--ok)' },
    { k: 'Verifying', v: d.verifying, c: 'var(--info)' },
    { k: 'Pending', v: d.pending, c: 'var(--fg-3)' },
    { k: 'RequiresCompletion', v: d.requiresCompletion, c: 'var(--warn)' },
    { k: 'Rejected', v: d.rejected, c: 'var(--crit)' },
  ];
  return (
    <div className={s.quality} data-testid="quality-panel">
      <div className={s.qBlock}>
        <h3>{t('dashboard.passportFunnel')}</h3>
        <div className={s.qBar} aria-hidden>
          {pSegs.map((x) => (
            <span key={x.k} style={{ width: `${(x.v / pTotal) * 100}%`, background: x.c }} />
          ))}
        </div>
        {pSegs.map((x) => (
          <div key={x.k} className={s.qRow}>
            <span className="row"><span className={s.legendDot} style={{ background: x.c }} />{t(`status.passport.${x.k}`)}</span>
            <strong>{x.v}</strong>
          </div>
        ))}
        <Link to="/passports" style={{ fontSize: 'var(--fs-xs)' }}>{t('dashboard.openPassports')} →</Link>
      </div>
      <div className={s.qBlock}>
        <h3>{t('dashboard.documents')}</h3>
        <div className={s.qBar} aria-hidden>
          {dSegs.map((x) => (
            <span key={x.k} style={{ width: `${(x.v / dTotal) * 100}%`, background: x.c }} />
          ))}
        </div>
        {dSegs.map((x) => (
          <div key={x.k} className={s.qRow}>
            <span className="row"><span className={s.legendDot} style={{ background: x.c }} />{t(`status.doc.${x.k}`)}</span>
            <strong>{x.v}</strong>
          </div>
        ))}
      </div>
      <div className={s.qBlock}>
        <div className={s.qStat}><h3>{t('dashboard.openNcr')}</h3><span className={s.qBig}>{data.openNonConformances}</span></div>
        <StatusChip tone={data.openNonConformances > 0 ? 'warn' : 'ok'} label={data.openNonConformances > 0 ? t('status.generic.warn') : t('status.generic.ok')} small />
        <div className={s.qStat}><h3>{t('dashboard.lotsBlocked')}</h3><span className={s.qBig} style={{ color: data.lotsBlocked > 0 ? 'var(--crit)' : undefined }}>{data.lotsBlocked}</span></div>
        <Link to="/trace" style={{ fontSize: 'var(--fs-xs)' }}>{t('dashboard.openLots')} →</Link>
      </div>
      <div className={s.qBlock}>
        <div className={s.qStat}><h3>{t('dashboard.readyForAcceptance')}</h3><span className={s.qBig} style={{ color: 'var(--ok)' }}>{data.readyForAcceptance}</span></div>
        <StatusChip tone="ok" label={t('status.passport.Approved')} small />
      </div>
    </div>
  );
}
