import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/api/client';
import { keys } from '@/api/keys';
import type { AdminStatus, DemoAccount, DemoResetResult, DemoScriptStep, DemoStatus } from '@/api/types';

export function useDemoStatus() {
  return useQuery({ queryKey: keys.demoStatus, queryFn: () => api.get<DemoStatus>('/demo/status'), staleTime: 60_000, retry: false });
}
export function useDemoScript(enabled: boolean) {
  return useQuery({ queryKey: keys.demoScript, queryFn: () => api.get<DemoScriptStep[]>('/demo/script'), enabled, staleTime: Infinity });
}
export function useDemoAccounts(enabled: boolean) {
  return useQuery({ queryKey: keys.demoAccounts, queryFn: () => api.get<DemoAccount[]>('/auth/demo-accounts'), enabled, staleTime: Infinity });
}
export function useResetDemo() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => api.post<DemoResetResult>('/demo/reset'),
    onSuccess: () => void qc.invalidateQueries(),
  });
}
export function useAdminStatus(enabled = true) {
  return useQuery({ queryKey: keys.admin.status, queryFn: () => api.get<AdminStatus>('/admin/status'), enabled, refetchInterval: 15_000 });
}
export function useHealth() {
  return useQuery({
    queryKey: keys.health,
    queryFn: async () => {
      const res = await fetch('/health/live', { cache: 'no-store' });
      return res.ok;
    },
    refetchInterval: 10_000,
    retry: false,
  });
}
