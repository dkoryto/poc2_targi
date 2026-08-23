import { screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { AdminPage, normaliseWeights, normaliseThresholds } from './AdminPage';
import { renderWithProviders } from '@/test/utils';

describe('Admin', () => {
  it('renders service cards, settings tables and demo status', async () => {
    renderWithProviders(<AdminPage />, { route: '/admin', auth: true });
    await waitFor(() => expect(screen.getByTestId('service-planning-engine')).toHaveTextContent('Działa'));
    expect(screen.getByTestId('service-local-ai')).toHaveTextContent('Wyłączona');
    await waitFor(() => expect(screen.getByTestId('settings-tables')).toHaveTextContent('Odchylenie ETA'));
    expect(screen.getByTestId('settings-tables')).toHaveTextContent('0,35');
  });

  it('normalises weights from the array shape', () => {
    expect(
      normaliseWeights([{ code: 'ETA_DEVIATION', weight: 0.35 }, { code: 'CRITICALITY', weight: 0.15 }], 'weight'),
    ).toEqual([
      { code: 'ETA_DEVIATION', value: 0.35 },
      { code: 'CRITICALITY', value: 0.15 },
    ]);
  });

  it('normalises weights from the camelCase object shape and drops aggregates', () => {
    // The API has also returned this shape; it used to crash the page with `.map is not a function`.
    expect(
      normaliseWeights(
        { etaDeviation: 0.35, criticality: 0.15, noAlternative: 0.1, docCompleteness: 0.15, supplierReliability: 0.1, coverage: 0.1, logisticsEvents: 0.05, sum: 1 },
        'weight',
      ),
    ).toEqual([
      { code: 'ETA_DEVIATION', value: 0.35 },
      { code: 'CRITICALITY', value: 0.15 },
      { code: 'NO_ALTERNATIVE', value: 0.1 },
      { code: 'DOC_COMPLETENESS', value: 0.15 },
      { code: 'SUPPLIER_RELIABILITY', value: 0.1 },
      { code: 'COVERAGE', value: 0.1 },
      { code: 'LOGISTICS_EVENTS', value: 0.05 },
    ]);
  });

  it('normalises objective weights and flat threshold scalars', () => {
    expect(normaliseWeights({ latenessPerDayPerPriority: 10, downtimePerHour: 20 }, 'value')).toEqual([
      { code: 'LATENESS_PER_DAY_PER_PRIORITY', value: 10 },
      { code: 'DOWNTIME_PER_HOUR', value: 20 },
    ]);
    expect(normaliseThresholds({ riskNotifyThreshold: 50, solverTimeLimitMs: 2500, horizonWeeks: 12 })).toEqual([
      { code: 'RISK_NOTIFY_THRESHOLD', value: 50, unit: null },
      { code: 'SOLVER_TIME_LIMIT_MS', value: 2500, unit: 'ms' },
      { code: 'HORIZON_WEEKS', value: 12, unit: null },
    ]);
  });

  it('returns an empty list for missing or malformed payloads instead of throwing', () => {
    expect(normaliseWeights(undefined, 'weight')).toEqual([]);
    expect(normaliseWeights(null, 'weight')).toEqual([]);
    expect(normaliseThresholds(undefined)).toEqual([]);
  });
});
