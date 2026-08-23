import { render, type RenderOptions } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router';
import type { ReactElement, ReactNode } from 'react';
import { ToastProvider } from '@/components/ui';
import { AuthProvider } from '@/features/auth/auth';

export function makeQueryClient() {
  return new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } } });
}

export function renderWithProviders(ui: ReactElement, { route = '/', auth = false, ...opts }: RenderOptions & { route?: string; auth?: boolean } = {}) {
  const qc = makeQueryClient();
  const Wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={qc}>
      <ToastProvider>
        <MemoryRouter initialEntries={[route]}>{auth ? <AuthProvider>{children}</AuthProvider> : children}</MemoryRouter>
      </ToastProvider>
    </QueryClientProvider>
  );
  return { qc, ...render(ui, { wrapper: Wrapper, ...opts }) };
}
