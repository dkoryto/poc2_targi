import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import s from './dashboard.module.css';
import type { RiskHeatmap as RiskHeatmapData } from '@/api/types';

function heatColor(score: number): string {
  if (score >= 75) return 'var(--risk-critical)';
  if (score >= 50) return 'var(--risk-high)';
  if (score >= 25) return 'var(--risk-medium)';
  return 'var(--risk-low)';
}

export function RiskHeatmap({ data }: { data: RiskHeatmapData }) {
  const { t } = useTranslation();
  const [tip, setTip] = useState<{ x: number; y: number; text: string } | null>(null);
  const left = 56;
  const top = 26;
  const W = 640;
  const H = 320;
  const cw = (W - left) / Math.max(1, data.cols.length);
  const ch = (H - top) / Math.max(1, data.rows.length);
  const cell = new Map(data.cells.map((c) => [`${c.row}|${c.col}`, c]));
  return (
    <div style={{ height: '100%', display: 'flex', flexDirection: 'column' }}>
      <svg viewBox={`0 0 ${W} ${H}`} className={s.heat} role="img" aria-label={t('dashboard.heatmap')} preserveAspectRatio="xMidYMid meet" data-testid="risk-heatmap">
        {data.cols.map((c, i) => (
          <text key={c} x={left + i * cw + cw / 2} y={16} textAnchor="middle" className={s.heatAxis}>
            {t(`dashboard.categories.${c}`, { defaultValue: c })}
          </text>
        ))}
        {data.rows.map((r, j) => (
          <text key={r} x={left - 8} y={top + j * ch + ch / 2 + 4} textAnchor="end" className={s.heatAxis}>
            {r}
          </text>
        ))}
        {data.rows.map((r, j) =>
          data.cols.map((c, i) => {
            const v = cell.get(`${r}|${c}`);
            const x = left + i * cw + 2;
            const y = top + j * ch + 2;
            const has = v && v.count > 0;
            const text = has ? t('dashboard.heatCells', { count: v.count, score: Math.round(v.score) }) : t('dashboard.noHeat');
            return (
              <g key={`${r}-${c}`} className={s.heatCell} onMouseEnter={(e) => setTip({ x: e.clientX, y: e.clientY, text: `${r} × ${t(`dashboard.categories.${c}`, { defaultValue: c })}: ${text}` })} onMouseMove={(e) => setTip({ x: e.clientX, y: e.clientY, text: `${r} × ${t(`dashboard.categories.${c}`, { defaultValue: c })}: ${text}` })} onMouseLeave={() => setTip(null)} tabIndex={0} aria-label={`${r} ${c} ${text}`}>
                <rect x={x} y={y} width={cw - 4} height={ch - 4} rx={3} fill={has ? heatColor(v.score) : 'var(--bg-2)'} stroke="var(--border)" />
                {has && (
                  <>
                    <text x={x + (cw - 4) / 2} y={y + (ch - 4) / 2 + 1} textAnchor="middle" className={s.heatText} fill="var(--on-accent)">
                      {Math.round(v.score)}
                    </text>
                    <text x={x + (cw - 4) / 2} y={y + (ch - 4) / 2 + 14} textAnchor="middle" className={s.heatSub}>
                      n={v.count}
                    </text>
                  </>
                )}
              </g>
            );
          }),
        )}
      </svg>
      <div className={s.heatLegend}>
        <span>{t('dashboard.heatRegion')} × {t('dashboard.heatCategory')}</span>
        <span style={{ marginLeft: 'auto' }} />
        {(['Low', 'Medium', 'High', 'Critical'] as const).map((c) => (
          <span key={c} className={s.legendRow}><span className={s.legendDot} style={{ background: heatColor(c === 'Low' ? 0 : c === 'Medium' ? 25 : c === 'High' ? 50 : 75) }} />{t(`risk.${c}`)}</span>
        ))}
      </div>
      {tip && (
        <div role="tooltip" style={{ position: 'fixed', left: tip.x + 10, top: tip.y + 10, background: 'var(--bg-elev)', border: '1px solid var(--border-strong)', padding: '4px 8px', borderRadius: 4, fontSize: 12, zIndex: 70, pointerEvents: 'none' }}>
          {tip.text}
        </div>
      )}
    </div>
  );
}
