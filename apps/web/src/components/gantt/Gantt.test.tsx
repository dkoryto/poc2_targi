import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { Gantt } from './Gantt';
import { buildLayout } from './ganttModel';
import { plan, planAfter, T0 } from '@/mocks/fixtures';
import { renderWithProviders } from '@/test/utils';

describe('Gantt', () => {
  it('renders a bar per operation and the today line', () => {
    renderWithProviders(<Gantt data={plan} today={T0} weeks={8} />);
    expect(screen.getByTestId('gantt-bar-WO-2026-014/30')).toBeInTheDocument();
    expect(screen.getByTestId('gantt-today')).toBeInTheDocument();
  });
  it('compare mode renders ghost bars and shift labels for moved operations only', () => {
    renderWithProviders(<Gantt data={planAfter} compare={{ before: plan }} today={T0} weeks={8} />);
    const ghosts = screen.getAllByTestId('gantt-ghost');
    expect(ghosts).toHaveLength(7);
    const shifts = screen.getAllByTestId('gantt-shift');
    expect(shifts.length).toBe(7);
    expect(screen.getByTestId('gantt-bar-WO-2026-014/30')).toHaveAttribute('data-changed', 'true');
    expect(screen.getByTestId('gantt-bar-WO-2026-013/20')).not.toHaveAttribute('data-changed');
  });
  it('layout computes shift days from before/after', () => {
    const layout = buildLayout(planAfter, { mode: 'workCenter', weeks: 8, pxPerDay: 24, before: plan });
    const b = layout.bars.find((x) => x.op.code === 'WO-2026-019/20')!;
    expect(b.changed).toBe(true);
    expect(b.shiftDays).toBeLessThan(-29);
    expect(b.ghost).not.toBeNull();
    const unchanged = layout.bars.find((x) => x.op.code === 'WO-2026-012/10')!;
    expect(unchanged.changed).toBe(false);
  });
  it('row mode by order lists orders', () => {
    renderWithProviders(<Gantt data={plan} mode="order" today={T0} weeks={12} />);
    expect(screen.getByText('WO-2026-019')).toBeInTheDocument();
  });
});
