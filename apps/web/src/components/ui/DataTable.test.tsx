import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { DataTable } from './DataTable';
import { ApiError } from '@/api/client';
import { renderWithProviders } from '@/test/utils';

const cols = [{ key: 'a', header: 'A', render: (r: { a: string }) => r.a }];

describe('DataTable states', () => {
  it('shows skeleton rows while loading', () => {
    renderWithProviders(<DataTable columns={cols} rows={undefined} rowKey={(r) => r.a} loading />);
    expect(screen.getAllByTestId('table-skeleton').length).toBeGreaterThan(0);
  });
  it('shows problem-details aware error with retry', () => {
    renderWithProviders(<DataTable columns={cols} rows={undefined} rowKey={(r) => r.a} error={new ApiError(500, { title: 'Boom', detail: 'db down', traceId: 'abc' })} onRetry={() => {}} />);
    expect(screen.getByTestId('error-state')).toHaveTextContent('db down');
    expect(screen.getByTestId('error-state')).toHaveTextContent('abc');
    expect(screen.getByRole('button', { name: 'Spróbuj ponownie' })).toBeInTheDocument();
  });
  it('shows empty state', () => {
    renderWithProviders(<DataTable columns={cols} rows={[]} rowKey={(r) => r.a} />);
    expect(screen.getByTestId('empty-state')).toBeInTheDocument();
  });
  it('renders rows', () => {
    renderWithProviders(<DataTable columns={cols} rows={[{ a: 'x1' }, { a: 'x2' }]} rowKey={(r) => r.a} />);
    expect(screen.getByText('x2')).toBeInTheDocument();
  });
});
