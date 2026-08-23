import { useMemo, useState, type KeyboardEvent } from 'react';
import { addDays, format, getISOWeek, isWeekend } from 'date-fns';
import { useTranslation } from 'react-i18next';
import s from './gantt.module.css';
import { buildLayout, productColor, dayX, type GanttRowMode, type GanttBar } from './ganttModel';
import type { GanttData } from '@/api/types';
import { SegmentedControl } from '@/components/ui';
import { fmtDate, fmtDateTime, fmtSigned } from '@/lib/format';

export interface GanttProps {
  data: GanttData;
  /** Render `before` as ghost bars under `data` (the "after"). */
  compare?: { before: GanttData };
  mode?: GanttRowMode;
  onModeChange?: (m: GanttRowMode) => void;
  weeks?: 4 | 8 | 12;
  onWeeksChange?: (w: 4 | 8 | 12) => void;
  today?: Date;
  showToolbar?: boolean;
  compact?: boolean;
  onSelect?: (opCode: string) => void;
}

const LABEL_W = 120;
const ROW_H = 34;
const HEADER_H = 40;

export function Gantt({ data, compare, mode: modeProp, onModeChange, weeks: weeksProp, onWeeksChange, today, showToolbar = true, compact, onSelect }: GanttProps) {
  const { t } = useTranslation();
  const [modeState, setModeState] = useState<GanttRowMode>('workCenter');
  const [weeksState, setWeeksState] = useState<4 | 8 | 12>(8);
  const mode = modeProp ?? modeState;
  const weeks = weeksProp ?? weeksState;
  const setMode = (m: GanttRowMode) => (onModeChange ? onModeChange(m) : setModeState(m));
  const setWeeks = (w: 4 | 8 | 12) => (onWeeksChange ? onWeeksChange(w) : setWeeksState(w));
  const pxPerDay = weeks === 4 ? 44 : weeks === 8 ? 24 : 16;
  const [tip, setTip] = useState<{ bar: GanttBar; x: number; y: number } | null>(null);

  const layout = useMemo(() => buildLayout(data, { mode, weeks, pxPerDay, before: compare?.before }), [data, mode, weeks, pxPerDay, compare]);
  const orders = useMemo(() => new Map(data.orders.map((o) => [o.code, o])), [data.orders]);
  const conflicts = useMemo(() => new Map(data.conflicts.map((c) => [c.operationCode, c])), [data.conflicts]);
  const rowIndex = useMemo(() => new Map(layout.rows.map((r, i) => [r.key, i])), [layout.rows]);
  const barByCode = useMemo(() => new Map(layout.bars.map((b) => [b.op.code, b])), [layout.bars]);

  const width = LABEL_W + layout.days * pxPerDay;
  const rowH = compact ? 28 : ROW_H;
  const now = today ?? new Date();
  const todayX = dayX(now, layout.start, pxPerDay);

  const weekTicks: { x: number; label: string }[] = [];
  const dayTicks: { x: number; label: string; weekend: boolean }[] = [];
  for (let d = 0; d < layout.days; d++) {
    const date = addDays(layout.start, d);
    const x = d * pxPerDay;
    if (d % 7 === 0) weekTicks.push({ x, label: `${t('gantt.week', { n: getISOWeek(date) })} · ${format(date, 'dd.MM')}` });
    dayTicks.push({ x, label: weeks === 4 ? format(date, 'EEEEE') : '', weekend: isWeekend(date) });
  }

  const onKey = (e: KeyboardEvent<SVGGElement>, bar: GanttBar) => {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      onSelect?.(bar.op.code);
    }
    if (e.key === 'Escape') setTip(null);
  };

  const visibleBars = layout.bars.filter((b) => rowIndex.has(b.rowKey) && b.x < layout.days * pxPerDay && b.x + b.width > 0);

  return (
    <div className={s.wrap} data-testid="gantt">
      {showToolbar && (
        <div className={s.toolbar}>
          <SegmentedControl
            label={t('gantt.zoom')}
            value={String(weeks) as '4' | '8' | '12'}
            onChange={(v) => setWeeks(Number(v) as 4 | 8 | 12)}
            options={[
              { value: '4', label: t('gantt.weeks', { count: 4 }) },
              { value: '8', label: t('gantt.weeks', { count: 8 }) },
              { value: '12', label: t('gantt.weeks', { count: 12 }) },
            ]}
          />
          <SegmentedControl
            label="rows"
            value={mode}
            onChange={setMode}
            options={[
              { value: 'workCenter', label: t('dashboard.viewByWorkCenter') },
              { value: 'order', label: t('dashboard.viewByOrder') },
            ]}
          />
          <div className={s.legend} style={{ marginLeft: 'auto' }}>
            <span className={s.legendItem}><span className={s.swatch} style={{ background: 'var(--info)' }} />{t('gantt.op')}</span>
            <span className={s.legendItem}><span className={s.swatch} style={{ background: 'repeating-linear-gradient(45deg,#8d9bb0 0 2px,transparent 2px 5px)', border: '1px solid #8d9bb0' }} />{t('gantt.frozen')}</span>
            <span className={s.legendItem}><span className={s.swatch} style={{ background: 'repeating-linear-gradient(-45deg,var(--crit) 0 2px,transparent 2px 5px)', border: '1px solid var(--crit)' }} />{t('gantt.materialWait')}</span>
            {compare && <span className={s.legendItem}><span className={s.swatch} style={{ border: '1px dashed var(--fg-3)' }} />{t('gantt.ghost')}</span>}
            {compare && <span className={s.legendItem}><span className={s.swatch} style={{ background: 'var(--warn)' }} />{t('gantt.changed')}</span>}
          </div>
        </div>
      )}
      <div className={s.scroll}>
        {layout.rows.length === 0 || visibleBars.length === 0 ? (
          <div className="muted" style={{ padding: 16 }}>{t('gantt.noOps')}</div>
        ) : null}
        <svg className={s.svg} width={width} height={HEADER_H + layout.rows.length * rowH} role="img" aria-label={t('dashboard.plan')}>
          <defs>
            <pattern id="g-frozen" width="6" height="6" patternUnits="userSpaceOnUse" patternTransform="rotate(45)">
              <rect width="6" height="6" fill="#6b7a90" />
              <line x1="0" y1="0" x2="0" y2="6" stroke="#9aa8bb" strokeWidth="2" />
            </pattern>
            <pattern id="g-wait" width="6" height="6" patternUnits="userSpaceOnUse" patternTransform="rotate(-45)">
              <rect width="6" height="6" fill="rgba(240,82,82,0.35)" />
              <line x1="0" y1="0" x2="0" y2="6" stroke="var(--crit)" strokeWidth="2" />
            </pattern>
            <marker id="g-arrow" markerWidth="6" markerHeight="6" refX="5" refY="3" orient="auto">
              <path d="M0,0 L6,3 L0,6 z" fill="#62718a" />
            </marker>
          </defs>
          {/* weekend shading + grid */}
          <g transform={`translate(${LABEL_W},0)`}>
            {dayTicks.map((d, i) => (
              <g key={i}>
                {d.weekend && <rect x={d.x} y={HEADER_H} width={pxPerDay} height={layout.rows.length * rowH} className={s.weekend} />}
                <line x1={d.x} y1={HEADER_H} x2={d.x} y2={HEADER_H + layout.rows.length * rowH} className={i % 7 === 0 ? s.weekLine : s.gridLine} />
                {d.label && <text x={d.x + pxPerDay / 2} y={HEADER_H - 6} textAnchor="middle" className={s.axisText}>{d.label}</text>}
              </g>
            ))}
            {weekTicks.map((w, i) => (
              <text key={i} x={w.x + 4} y={14} className={s.axisWeek}>{w.label}</text>
            ))}
            {todayX >= 0 && todayX <= layout.days * pxPerDay && (
              <g data-testid="gantt-today">
                <line x1={todayX} y1={HEADER_H - 18} x2={todayX} y2={HEADER_H + layout.rows.length * rowH} className={s.today} />
                <text x={todayX + 3} y={HEADER_H - 20} className={s.todayText}>{t('gantt.today')}</text>
              </g>
            )}
          </g>
          {/* rows */}
          {layout.rows.map((r, i) => (
            <g key={r.key} transform={`translate(0,${HEADER_H + i * rowH})`}>
              <line x1={0} y1={rowH} x2={width} y2={rowH} className={s.gridLine} />
              <text x={8} y={rowH / 2 - (r.sub ? 2 : -4)} className={s.rowLabel}>{r.label}</text>
              {r.sub && <text x={8} y={rowH / 2 + 10} className={s.rowSub}>{r.sub.length > 18 ? r.sub.slice(0, 18) + '…' : r.sub}</text>}
            </g>
          ))}
          {/* dependencies */}
          <g transform={`translate(${LABEL_W},${HEADER_H})`}>
            {data.dependencies.map((dep, i) => {
              const a = barByCode.get(dep.from);
              const b = barByCode.get(dep.to);
              if (!a || !b) return null;
              const ra = rowIndex.get(a.rowKey);
              const rb = rowIndex.get(b.rowKey);
              if (ra === undefined || rb === undefined) return null;
              const x1 = a.x + a.width;
              const y1 = ra * rowH + rowH / 2;
              const x2 = b.x;
              const y2 = rb * rowH + rowH / 2;
              const mid = x1 + Math.max(6, (x2 - x1) / 2);
              return <path key={i} d={`M${x1},${y1} L${mid},${y1} L${mid},${y2} L${x2},${y2}`} className={s.dep} markerEnd="url(#g-arrow)" />;
            })}
          </g>
          {/* ghost bars (before) */}
          {compare && (
            <g transform={`translate(${LABEL_W},${HEADER_H})`}>
              {visibleBars.filter((b) => b.ghost).map((b) => {
                const ri = rowIndex.get(b.rowKey)!;
                return <rect key={`g-${b.op.code}`} x={b.ghost!.x} y={ri * rowH + 4} width={b.ghost!.width} height={rowH - 8} rx={3} className={s.ghost} data-testid="gantt-ghost" />;
              })}
            </g>
          )}
          {/* bars */}
          <g transform={`translate(${LABEL_W},${HEADER_H})`}>
            {visibleBars.map((b) => {
              const ri = rowIndex.get(b.rowKey)!;
              const ord = orders.get(b.op.orderCode);
              const color = b.op.frozen ? 'url(#g-frozen)' : b.op.materialWait ? 'url(#g-wait)' : b.changed && compare ? 'var(--warn)' : productColor(ord?.productCode ?? b.op.orderCode);
              const conflict = conflicts.get(b.op.code);
              const label = mode === 'workCenter' ? `${b.op.orderCode}/${b.op.sequence}` : `${b.op.workCenterCode} /${b.op.sequence}`;
              const y = ri * rowH + 5;
              const h = rowH - 10;
              return (
                <g
                  key={b.op.code}
                  className={s.bar}
                  tabIndex={0}
                  role="button"
                  aria-label={`${b.op.orderCode} ${b.op.code} ${b.op.workCenterCode} ${fmtDateTime(b.op.start)} – ${fmtDateTime(b.op.end)}`}
                  data-testid={`gantt-bar-${b.op.code}`}
                  data-changed={b.changed || undefined}
                  onMouseEnter={(e) => setTip({ bar: b, x: e.clientX, y: e.clientY })}
                  onMouseMove={(e) => setTip({ bar: b, x: e.clientX, y: e.clientY })}
                  onMouseLeave={() => setTip(null)}
                  onFocus={(e) => { const r = (e.target as SVGGElement).getBoundingClientRect(); setTip({ bar: b, x: r.left + r.width / 2, y: r.top }); }}
                  onBlur={() => setTip(null)}
                  onClick={() => onSelect?.(b.op.code)}
                  onKeyDown={(e) => onKey(e, b)}
                >
                  <rect className="body" x={b.x} y={y} width={b.width} height={h} rx={3} fill={color} stroke={conflict ? 'var(--crit)' : b.changed && compare ? 'var(--warn)' : 'rgba(0,0,0,0.35)'} strokeWidth={conflict ? 2 : 1} />
                  {b.width > 40 && (
                    <text x={b.x + 5} y={y + h / 2 + 4} className={[s.barText, (b.op.frozen || b.op.materialWait) && s.barTextLight].filter(Boolean).join(' ')} clipPath={`inset(0 0 0 0)`}>
                      {label.length * 6.5 > b.width - 8 ? label.slice(0, Math.max(2, Math.floor((b.width - 8) / 6.5))) : label}
                    </text>
                  )}
                  {conflict && <circle cx={b.x + b.width - 6} cy={y + 6} r={4} fill="var(--crit)" stroke="#fff" strokeWidth={1} />}
                  {compare && b.changed && b.shiftDays !== 0 && (
                    <text x={b.x + b.width + 4} y={y + h / 2 + 4} className={s.shiftLabel} data-testid="gantt-shift">
                      {fmtSigned(b.shiftDays, 1)} d
                    </text>
                  )}
                </g>
              );
            })}
          </g>
        </svg>
      </div>
      {tip && (
        <div className={s.tooltip} style={{ left: tip.x + 12, top: tip.y + 12 }} role="tooltip">
          <dl>
            <dt>{t('gantt.order')}</dt><dd><strong>{tip.bar.op.orderCode}</strong> {orders.get(tip.bar.op.orderCode)?.productName}</dd>
            <dt>{t('gantt.op')}</dt><dd>{tip.bar.op.code}</dd>
            <dt>{t('gantt.workCenter')}</dt><dd>{tip.bar.op.workCenterCode}</dd>
            <dt>{t('gantt.start')}</dt><dd>{fmtDateTime(tip.bar.op.start)}</dd>
            <dt>{t('gantt.end')}</dt><dd>{fmtDateTime(tip.bar.op.end)}</dd>
            {orders.get(tip.bar.op.orderCode) && (<><dt>{t('gantt.due')}</dt><dd>{fmtDate(orders.get(tip.bar.op.orderCode)!.dueDate)}</dd></>)}
            {orders.get(tip.bar.op.orderCode) && (<><dt>{t('gantt.priority')}</dt><dd>{orders.get(tip.bar.op.orderCode)!.priority}</dd></>)}
            {tip.bar.changed && (<><dt>{t('gantt.shift')}</dt><dd>{fmtSigned(tip.bar.shiftDays, 1)} d</dd></>)}
            {tip.bar.op.frozen && (<><dt /><dd>{t('gantt.frozen')}</dd></>)}
            {tip.bar.op.materialWait && (<><dt /><dd style={{ color: 'var(--crit)' }}>{t('gantt.materialWait')}</dd></>)}
            {conflicts.get(tip.bar.op.code) && (<><dt>{t('gantt.conflict')}</dt><dd style={{ color: 'var(--crit)' }}>{t(`explain.${conflicts.get(tip.bar.op.code)!.reasonCode}`, { ...conflicts.get(tip.bar.op.code)!.params, defaultValue: conflicts.get(tip.bar.op.code)!.reasonCode })}</dd></>)}
          </dl>
        </div>
      )}
    </div>
  );
}
