import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router';
import s from './dashboard.module.css';
import { useHeatmap, useKpis, useMapData, usePlan, useQualityStatus } from './api';
import { Card, KpiTile, ErrorState, LoadingState, Skeleton, Button } from '@/components/ui';
import { Wand2 } from 'lucide-react';
import { DeliveryMap } from './DeliveryMap';
import { RiskHeatmap } from './RiskHeatmap';
import { QualityPanel } from './QualityPanel';
import { Gantt } from '@/components/gantt/Gantt';
import { onDomainEvent } from '@/realtime/useLive';
import { fmtDateTime } from '@/lib/format';

type PanelKey = 'map' | 'heat' | 'plan' | 'quality';
const RISK_ORDER = ['Low', 'Medium', 'High', 'Critical'];

export function DashboardPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const kpis = useKpis();
  const map = useMapData();
  const heat = useHeatmap();
  const quality = useQualityStatus();
  const plan = usePlan();
  const [focus, setFocus] = useState<PanelKey | null>(null);
  const [pulse, setPulse] = useState<Set<string>>(new Set());
  const [pulseKpi, setPulseKpi] = useState(false);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') setFocus(null); };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, []);

  useEffect(
    () =>
      onDomainEvent((e) => {
        if (e.name !== 'DeliveryRiskChanged') return;
        const p = e.payload;
        const from = String(p.previousCategory ?? p.fromCategory ?? '');
        const to = String(p.category ?? p.newCategory ?? '');
        if (RISK_ORDER.indexOf(to) <= RISK_ORDER.indexOf(from)) return;
        const codes = [p.shipmentCode, p.poCode, p.supplierCode, p.code].filter(Boolean).map(String);
        setPulse(new Set(codes));
        setPulseKpi(true);
        window.setTimeout(() => { setPulse(new Set()); setPulseKpi(false); }, 1200);
      }),
    [],
  );

  const toggle = useCallback((k: PanelKey) => setFocus((f) => (f === k ? null : k)), []);
  const panelCls = (k: PanelKey) => [s.panel, focus && focus !== k && s.hidden].filter(Boolean).join(' ');

  return (
    <div className={s.page} data-testid="dashboard">
      <div className="page-header" style={{ alignItems: 'center' }}>
        <div>
          <h1>{t('dashboard.title')}</h1>
          <p>{t('dashboard.subtitle')}{kpis.data ? ` · ${t('kpi.asOf', { time: fmtDateTime(kpis.data.asOf) })}` : ''}</p>
        </div>
      </div>
      {!focus && (
        <div className={s.kpis} data-testid="kpi-row">
          {kpis.isLoading && Array.from({ length: 6 }).map((_, i) => <Skeleton key={i} height={84} />)}
          {kpis.isError && <div style={{ gridColumn: '1 / -1' }}><ErrorState error={kpis.error} onRetry={() => kpis.refetch()} /></div>}
          {kpis.data?.items.map((k) => <KpiTile key={k.code} kpi={k} pulse={pulseKpi && k.code === 'HIGH_RISK_DELIVERIES'} />)}
        </div>
      )}
      <div className={[s.grid, focus && s.gridFocused].filter(Boolean).join(' ')}>
        <Card title={t('dashboard.map')} definition={t('dashboard.mapDef')} className={panelCls('map')} flush focusable focused={focus === 'map'} onToggleFocus={() => toggle('map')} data-testid="panel-map">
          {map.isLoading && <div style={{ padding: 12 }}><LoadingState /></div>}
          {map.isError && <ErrorState error={map.error} onRetry={() => map.refetch()} />}
          {map.data && <DeliveryMap data={map.data} pulseCodes={pulse} onOpenPo={(code) => navigate(`/supply/orders/${code}`)} />}
        </Card>
        <Card title={t('dashboard.heatmap')} definition={t('dashboard.heatmapDef')} className={panelCls('heat')} focusable focused={focus === 'heat'} onToggleFocus={() => toggle('heat')} data-testid="panel-heatmap">
          {heat.isLoading && <LoadingState />}
          {heat.isError && <ErrorState error={heat.error} onRetry={() => heat.refetch()} />}
          {heat.data && <RiskHeatmap data={heat.data} />}
        </Card>
        <Card title={t('dashboard.plan')} definition={t('dashboard.planDef')} className={panelCls('plan')} actions={<Button size="sm" icon={<Wand2 size={13} />} onClick={() => navigate('/planning')} data-testid="open-whatif">{t('dashboard.openWhatIf')}</Button>} focusable focused={focus === 'plan'} onToggleFocus={() => toggle('plan')} data-testid="panel-plan">
          {plan.isLoading && <LoadingState />}
          {plan.isError && <ErrorState error={plan.error} onRetry={() => plan.refetch()} />}
          {plan.data && <Gantt data={plan.data} compact={!focus} onSelect={() => navigate('/planning')} />}
        </Card>
        <Card title={t('dashboard.quality')} definition={t('dashboard.qualityDef')} className={panelCls('quality')} focusable focused={focus === 'quality'} onToggleFocus={() => toggle('quality')} data-testid="panel-quality">
          {quality.isLoading && <LoadingState />}
          {quality.isError && <ErrorState error={quality.error} onRetry={() => quality.refetch()} />}
          {quality.data && <QualityPanel data={quality.data} />}
        </Card>
      </div>
    </div>
  );
}
