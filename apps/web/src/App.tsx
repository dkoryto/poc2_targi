import { BrowserRouter, Navigate, Route, Routes } from 'react-router';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ToastProvider } from '@/components/ui';
import { ThemeProvider } from '@/theme/theme';
import { AuthProvider } from '@/features/auth/auth';
import { RequireAuth } from '@/features/auth/RequireRole';
import { LoginPage } from '@/features/auth/LoginPage';
import { AppShell } from '@/components/layout/AppShell';
import { DashboardPage } from '@/features/dashboard/DashboardPage';
import { SupplyListPage } from '@/features/supply/SupplyListPage';
import { PurchaseOrderPage } from '@/features/supply/PurchaseOrderPage';
import { InboundPage } from '@/features/inbound/InboundPage';
import { NotificationsPage } from '@/features/notifications/NotificationsPage';
import { PlanningPage } from '@/features/planning/PlanningPage';
import { ScenarioDetailPage } from '@/features/planning/ScenarioDetailPage';
import { TracePage } from '@/features/trace/TracePage';
import { SerialPage } from '@/features/trace/SerialPage';
import { LotPage } from '@/features/trace/LotPage';
import { LotsPage } from '@/features/trace/LotsPage';
import { PassportsPage } from '@/features/passports/PassportsPage';
import { PassportPage } from '@/features/passports/PassportPage';
import { AuditPage } from '@/features/audit/AuditPage';
import { AdminPage } from '@/features/admin/AdminPage';
import { SummaryPage } from '@/features/demo/SummaryPage';
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
          <Route path="/planning" element={<PlanningPage />} />
          <Route path="/planning/scenarios/:id" element={<ScenarioDetailPage />} />
          <Route path="/trace" element={<TracePage />} />
          <Route path="/trace/serials/:serial" element={<SerialPage />} />
          <Route path="/trace/lots" element={<LotsPage />} />
          <Route path="/trace/lots/:lot" element={<LotPage />} />
          <Route path="/passports" element={<PassportsPage />} />
          <Route path="/passports/:serial" element={<PassportPage />} />
          <Route path="/audit" element={<AuditPage />} />
          <Route path="/admin" element={<AdminPage />} />
          <Route path="/demo/summary" element={<SummaryPage />} />
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
      <ThemeProvider>
      <ToastProvider>
        <AuthProvider>
          <BrowserRouter>
            <AppRoutes />
          </BrowserRouter>
        </AuthProvider>
      </ToastProvider>
      </ThemeProvider>
    </QueryClientProvider>
  );
}
