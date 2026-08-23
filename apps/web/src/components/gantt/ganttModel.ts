import { addDays, differenceInMinutes, startOfDay, parseISO } from 'date-fns';
import type { GanttData, GanttOperation } from '@/api/types';

export interface GanttBar {
  op: GanttOperation;
  rowKey: string;
  x: number;
  width: number;
  ghost?: { x: number; width: number } | null;
  shiftDays: number;
  changed: boolean;
}

export interface GanttLayout {
  start: Date;
  end: Date;
  days: number;
  pxPerDay: number;
  rows: { key: string; label: string; sub?: string }[];
  bars: GanttBar[];
}

export type GanttRowMode = 'workCenter' | 'order';

export function productColor(productCode: string): string {
  const palette = ['var(--chart-1)', 'var(--chart-2)', 'var(--chart-3)', 'var(--chart-4)', 'var(--chart-5)', 'var(--chart-6)'];
  let h = 0;
  for (const ch of productCode) h = (h * 31 + ch.charCodeAt(0)) >>> 0;
  return palette[h % palette.length]!;
}

export function dayX(d: Date, start: Date, pxPerDay: number): number {
  return (differenceInMinutes(d, start) / 1440) * pxPerDay;
}

export function buildLayout(
  data: GanttData,
  opts: { mode: GanttRowMode; weeks: number; pxPerDay: number; before?: GanttData; start?: Date },
): GanttLayout {
  const start = startOfDay(opts.start ?? parseISO(data.horizonStart));
  const days = opts.weeks * 7;
  const end = addDays(start, days);
  const pxPerDay = opts.pxPerDay;

  const rows =
    opts.mode === 'workCenter'
      ? data.workCenters.map((w) => ({ key: w.code, label: w.code, sub: w.name }))
      : data.orders.map((o) => ({ key: o.code, label: o.code, sub: o.productCode }));

  const beforeByCode = new Map<string, GanttOperation>();
  opts.before?.operations.forEach((o) => beforeByCode.set(o.code, o));

  const bars: GanttBar[] = [];
  for (const op of data.operations) {
    const s = parseISO(op.start);
    const e = parseISO(op.end);
    const x = dayX(s, start, pxPerDay);
    const width = Math.max(3, dayX(e, start, pxPerDay) - x);
    const rowKey = opts.mode === 'workCenter' ? op.workCenterCode : op.orderCode;
    const prev = beforeByCode.get(op.code);
    let ghost: GanttBar['ghost'] = null;
    let shiftDays = op.shiftDays ?? 0;
    let changed = op.changed ?? false;
    if (prev) {
      const ps = parseISO(prev.start);
      const pe = parseISO(prev.end);
      const sameRow = (opts.mode === 'workCenter' ? prev.workCenterCode : prev.orderCode) === rowKey;
      const moved = ps.getTime() !== s.getTime() || pe.getTime() !== e.getTime() || !sameRow;
      if (moved) {
        changed = true;
        const gx = dayX(ps, start, pxPerDay);
        ghost = { x: gx, width: Math.max(3, dayX(pe, start, pxPerDay) - gx) };
        if (!op.shiftDays) shiftDays = Math.round((differenceInMinutes(s, ps) / 1440) * 10) / 10;
      }
    }
    bars.push({ op, rowKey, x, width, ghost, shiftDays, changed });
  }
  return { start, end, days, pxPerDay, rows, bars };
}
