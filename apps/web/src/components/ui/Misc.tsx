import type { ReactNode } from 'react';
import { ShieldAlert } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import s from './ui.module.css';
import { fmtDateTime, fmtNumber } from '@/lib/format';

export function Timeline({ items }: { items: { id: string; at: string; who?: string | null; title: ReactNode; body?: ReactNode }[] }) {
  return (
    <ul className={s.timeline}>
      {items.map((it) => (
        <li key={it.id} className={s.timelineItem}>
          <div>{it.title}</div>
          {it.body && <div className="muted">{it.body}</div>}
          <div className={s.timelineMeta}>
            {fmtDateTime(it.at)}
            {it.who ? ` · ${it.who}` : ''}
          </div>
        </li>
      ))}
    </ul>
  );
}

export function ProgressBar({ value, label }: { value: number; label?: string }) {
  const pct = Math.max(0, Math.min(100, value));
  return (
    <div className={s.progressRow}>
      <div className={s.progress} role="progressbar" aria-valuenow={pct} aria-valuemin={0} aria-valuemax={100} aria-label={label}>
        <div className={s.progressBar} style={{ width: `${pct}%` }} />
      </div>
      <span>{fmtNumber(pct)} %</span>
    </div>
  );
}

export function Badge({ count }: { count: number }) {
  if (count <= 0) return null;
  return <span className={s.badge}>{count > 99 ? '99+' : count}</span>;
}

export function SegmentedControl<T extends string>({ options, value, onChange, label }: { options: { value: T; label: string }[]; value: T; onChange: (v: T) => void; label: string }) {
  return (
    <div className={s.segmented} role="group" aria-label={label}>
      {options.map((o) => (
        <button key={o.value} type="button" className={s.segment} aria-pressed={value === o.value} onClick={() => onChange(o.value)}>
          {o.label}
        </button>
      ))}
    </div>
  );
}

export function DisclaimerBanner() {
  const { t } = useTranslation();
  return (
    <footer className={s.banner} role="contentinfo">
      <ShieldAlert size={14} aria-hidden />
      <span>{t('app.disclaimer')}</span>
    </footer>
  );
}
