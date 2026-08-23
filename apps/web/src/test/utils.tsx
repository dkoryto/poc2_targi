import { render, type RenderOptions } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router';
import type { ReactElement, ReactNode } from 'react';
import { ToastProvider } from '@/components/ui';
import { AuthProvider } from '@/features/auth/auth';
import { SiteProvider } from '@/features/sites/sites';

export function makeQueryClient() {
  return new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } } });
}

/**
 * `site: false` renders without the plant context — only for components that never touch it.
 * Everything routed under AppShell needs it, so it is on by default.
 */
export function renderWithProviders(
  ui: ReactElement,
  { route = '/', auth = false, site = true, ...opts }: RenderOptions & { route?: string; auth?: boolean; site?: boolean } = {},
) {
  const qc = makeQueryClient();
  const Wrapper = ({ children }: { children: ReactNode }) => {
    const inner = site ? <SiteProvider>{children}</SiteProvider> : children;
    return (
      <QueryClientProvider client={qc}>
        <ToastProvider>
          <MemoryRouter initialEntries={[route]}>{auth ? <AuthProvider>{inner}</AuthProvider> : inner}</MemoryRouter>
        </ToastProvider>
      </QueryClientProvider>
    );
  };
  return { qc, ...render(ui, { wrapper: Wrapper, ...opts }) };
}
