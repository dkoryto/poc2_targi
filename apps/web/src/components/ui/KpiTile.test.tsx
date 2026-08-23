import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { KpiTile, formatKpiValue, formatTrend } from './KpiTile';
import { renderWithProviders } from '@/test/utils';
import { setLocale } from '@/i18n';

describe('KpiTile formatting', () => {
  it('formats percent, hours and counts', () => {
    expect(formatKpiValue({ value: 86, unit: '%' })).toEqual({ value: '86', unit: '%' });
    expect(formatKpiValue({ value: 75.5, unit: '%' }).value).toBe('75,5');
    expect(formatKpiValue({ value: 36, unit: 'h' })).toEqual({ value: '36', unit: 'h' });
    expect(formatKpiValue({ value: 3, unit: 'count' })).toEqual({ value: '3', unit: '' });
  });
  it('formats trend with sign and pp for percent', () => {
    expect(formatTrend({ trend: 2, unit: '%' })).toBe('+2 pp');
    expect(formatTrend({ trend: -12.5, unit: '%' })).toBe('−12,5 pp');
    expect(formatTrend({ trend: 36, unit: 'h' })).toBe('+36 h');
    expect(formatTrend({ trend: 1, unit: 'count' })).toBe('+1');
  });
  it('uses english number format when locale is en', () => {
    setLocale('en');
    expect(formatKpiValue({ value: 75.5, unit: '%' }).value).toBe('75.5');
  });
  it('renders label, value, unit, trend and status icon', () => {
    renderWithProviders(<KpiTile kpi={{ code: 'PREDICTED_DOWNTIME_H', value: 36, unit: 'h', trend: 36, status: 'critical', definitionKey: 'x' }} />);
    expect(screen.getByText('Przewidywany przestój')).toBeInTheDocument();
    expect(screen.getByTestId('kpi-value')).toHaveTextContent('36');
    expect(screen.getByTestId('kpi-trend')).toHaveTextContent('+36 h');
    expect(screen.getByLabelText('Krytyczne')).toBeInTheDocument();
  });
});
