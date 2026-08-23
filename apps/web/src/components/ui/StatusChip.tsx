import type { ReactNode } from 'react';
import {
  CheckCircle2,
  AlertTriangle,
  OctagonAlert,
  Info,
  Circle,
  Clock,
  Truck,
  PackageCheck,
  PauseCircle,
  Factory,
  Search,
  FileCheck2,
  FileX2,
  FileClock,
  Flame,
} from 'lucide-react';
import { useTranslation } from 'react-i18next';
import s from './ui.module.css';
import type { RiskCategory } from '@/api/types';

export type Tone = 'ok' | 'warn' | 'high' | 'critical' | 'info' | 'neutral';

const toneIcon: Record<Tone, ReactNode> = {
  ok: <CheckCircle2 size={13} aria-hidden />,
  warn: <AlertTriangle size={13} aria-hidden />,
  high: <Flame size={13} aria-hidden />,
  critical: <OctagonAlert size={13} aria-hidden />,
  info: <Info size={13} aria-hidden />,
  neutral: <Circle size={11} aria-hidden />,
};

export function StatusChip({
  tone,
  label,
  icon,
  small,
  title,
}: {
  tone: Tone;
  label: string;
  icon?: ReactNode;
  small?: boolean;
  title?: string;
}) {
  return (
    <span className={[s.chip, s[`tone-${tone}`], small && s.chipSm].filter(Boolean).join(' ')} title={title}>
      {icon ?? toneIcon[tone]}
      <span>{label}</span>
    </span>
  );
}

export const riskTone: Record<RiskCategory, Tone> = {
  Low: 'ok',
  Medium: 'warn',
  High: 'high',
  Critical: 'critical',
};

export function riskColorVar(c: RiskCategory): string {
  return { Low: 'var(--risk-low)', Medium: 'var(--risk-medium)', High: 'var(--risk-high)', Critical: 'var(--risk-critical)' }[c];
}

export function RiskBadge({ category, score, small }: { category: RiskCategory; score?: number; small?: boolean }) {
  const { t } = useTranslation();
  const label = score !== undefined ? `${t(`risk.${category}`)} · ${Math.round(score)}` : t(`risk.${category}`);
  return <StatusChip tone={riskTone[category]} label={label} small={small} title={t('app.ruleBased')} />;
}

const poTone: Record<string, { tone: Tone; icon: ReactNode }> = {
  Confirmed: { tone: 'info', icon: <Circle size={11} aria-hidden /> },
  InProduction: { tone: 'info', icon: <Factory size={13} aria-hidden /> },
  QualityControl: { tone: 'warn', icon: <Search size={13} aria-hidden /> },
  ReadyToShip: { tone: 'info', icon: <PackageCheck size={13} aria-hidden /> },
  Shipped: { tone: 'info', icon: <Truck size={13} aria-hidden /> },
  InTransit: { tone: 'info', icon: <Truck size={13} aria-hidden /> },
  Planned: { tone: 'neutral', icon: <Clock size={13} aria-hidden /> },
  Delayed: { tone: 'critical', icon: <AlertTriangle size={13} aria-hidden /> },
  Delivered: { tone: 'ok', icon: <CheckCircle2 size={13} aria-hidden /> },
  OnHold: { tone: 'critical', icon: <PauseCircle size={13} aria-hidden /> },
};
export function PoStatusChip({ status, small }: { status: string; small?: boolean }) {
  const { t } = useTranslation();
  const cfg = poTone[status] ?? { tone: 'neutral' as Tone, icon: undefined };
  return <StatusChip tone={cfg.tone} icon={cfg.icon} label={t(`status.po.${status}`, { defaultValue: status })} small={small} />;
}
export function ShipmentStatusChip({ status, small }: { status: string; small?: boolean }) {
  const { t } = useTranslation();
  const cfg = poTone[status] ?? { tone: 'neutral' as Tone, icon: undefined };
  return <StatusChip tone={cfg.tone} icon={cfg.icon} label={t(`status.shipment.${status}`, { defaultValue: status })} small={small} />;
}

const docTone: Record<string, { tone: Tone; icon: ReactNode }> = {
  Pending: { tone: 'neutral', icon: <FileClock size={13} aria-hidden /> },
  Verifying: { tone: 'info', icon: <Search size={13} aria-hidden /> },
  Accepted: { tone: 'ok', icon: <FileCheck2 size={13} aria-hidden /> },
  Rejected: { tone: 'critical', icon: <FileX2 size={13} aria-hidden /> },
  RequiresCompletion: { tone: 'warn', icon: <AlertTriangle size={13} aria-hidden /> },
};
export function DocStatusChip({ status, small }: { status: string; small?: boolean }) {
  const { t } = useTranslation();
  const cfg = docTone[status] ?? { tone: 'neutral' as Tone, icon: undefined };
  return <StatusChip tone={cfg.tone} icon={cfg.icon} label={t(`status.doc.${status}`, { defaultValue: status })} small={small} />;
}

const orderTone: Record<string, Tone> = {
  Planned: 'neutral',
  Released: 'info',
  InProgress: 'info',
  Completed: 'ok',
  OnHold: 'critical',
};
export function OrderStatusChip({ status, small }: { status: string; small?: boolean }) {
  const { t } = useTranslation();
  return <StatusChip tone={orderTone[status] ?? 'neutral'} label={t(`status.order.${status}`, { defaultValue: status })} small={small} />;
}

export function GenericStatusChip({ status, small }: { status: 'ok' | 'warn' | 'critical' | 'info'; small?: boolean }) {
  const { t } = useTranslation();
  return <StatusChip tone={status} label={t(`status.generic.${status}`)} small={small} />;
}
