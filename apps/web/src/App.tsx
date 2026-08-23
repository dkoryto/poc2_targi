import { BrowserRouter, Navigate, Route, Routes } from 'react-router';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ToastProvider } from '@/components/ui';
import { AuthProvider } from '@/features/auth/auth';
import { RequireAuth } from '@/features/auth/RequireRole';
import { LoginPage } from '@/features/auth/LoginPage';
import { AppShell } from '@/components/layout/AppShell';
import { DashboardPage } from '@/features/dashboard/DashboardPage';
import { SupplyListPage } from '@/features/supply/SupplyListPage';
import { PurchaseOrderPage } from '@/features/supply/PurchaseOrderPage';
import { InboundPage } from '@/features/inbound/InboundPage';
import { NotificationsPage } from '@/features/notifications/NotificationsPage';
import { Placeholder } from '@/pages/Placeholder';
import { ApiError } from '@/api/client';

export function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        retry: (count, err) => !(err instanceof ApiError && err.status < 500) && count < 2,
        staleTime: 5_000,
        refetchOnWindowFocus: false,
      },
    },
  });
}

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<RequireAuth />}>
        <Route element={<AppShell />}>
          <Route index element={<DashboardPage />} />
          <Route path="/supply" element={<SupplyListPage />} />
          <Route path="/supply/orders/:code" element={<PurchaseOrderPage />} />
          <Route path="/inbound" element={<InboundPage />} />
          <Route path="/inbound/:code" element={<InboundPage />} />
          <Route path="/notifications" element={<NotificationsPage />} />
          <Route path="/planning/*" element={<Placeholder titleKey="nav.planning" />} />
          <Route path="/trace/*" element={<Placeholder titleKey="nav.trace" />} />
          <Route path="/passports/*" element={<Placeholder titleKey="nav.passports" />} />
          <Route path="/audit/*" element={<Placeholder titleKey="nav.audit" />} />
          <Route path="/admin/*" element={<Placeholder titleKey="nav.admin" />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Route>
      </Route>
    </Routes>
  );
}

export function App({ queryClient }: { queryClient?: QueryClient }) {
  const qc = queryClient ?? createQueryClient();
  return (
    <QueryClientProvider client={qc}>
      <ToastProvider>
        <AuthProvider>
          <BrowserRouter>
            <AppRoutes />
          </BrowserRouter>
        </AuthProvider>
      </ToastProvider>
    </QueryClientProvider>
  );
}
