import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ErrorBoundary } from './ErrorBoundary';
import { ApiError } from '@/api/client';
import '@/i18n';

function Boom({ error }: { error: Error }): never {
  throw error;
}

afterEach(() => vi.restoreAllMocks());

describe('ErrorBoundary', () => {
  it('renders a localized card instead of blanking the page', () => {
    vi.spyOn(console, 'error').mockImplementation(() => {});
    render(
      <ErrorBoundary>
        <Boom error={new TypeError('x.map is not a function')} />
      </ErrorBoundary>,
    );
    expect(screen.getByTestId('error-boundary')).toBeInTheDocument();
    expect(screen.getByRole('alert')).toBeInTheDocument();
    expect(screen.getByText(/x\.map is not a function/)).toBeInTheDocument();
    expect(screen.getByTestId('error-retry')).toBeInTheDocument();
  });

  it('shows the correlation id from a Problem Details response', () => {
    vi.spyOn(console, 'error').mockImplementation(() => {});
    const err = new ApiError(500, { title: 'Boom', status: 500, traceId: 'corr-123' });
    render(
      <ErrorBoundary>
        <Boom error={err} />
      </ErrorBoundary>,
    );
    expect(screen.getByText('corr-123')).toBeInTheDocument();
  });

  it('recovers when the child stops throwing and retry is pressed', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => {});
    const user = userEvent.setup();
    let shouldThrow = true;
    function Child() {
      if (shouldThrow) throw new Error('nope');
      return <p>recovered</p>;
    }
    render(
      <ErrorBoundary>
        <Child />
      </ErrorBoundary>,
    );
    expect(screen.getByTestId('error-boundary')).toBeInTheDocument();
    shouldThrow = false;
    await user.click(screen.getByTestId('error-retry'));
    expect(await screen.findByText('recovered')).toBeInTheDocument();
  });

  it('clears the error when the reset key (route) changes', () => {
    vi.spyOn(console, 'error').mockImplementation(() => {});
    let shouldThrow = true;
    function Child() {
      if (shouldThrow) throw new Error('route error');
      return <p>other page</p>;
    }
    const { rerender } = render(
      <ErrorBoundary resetKey="/admin">
        <Child />
      </ErrorBoundary>,
    );
    expect(screen.getByTestId('error-boundary')).toBeInTheDocument();
    shouldThrow = false;
    rerender(
      <ErrorBoundary resetKey="/planning">
        <Child />
      </ErrorBoundary>,
    );
    expect(screen.getByText('other page')).toBeInTheDocument();
  });
});
