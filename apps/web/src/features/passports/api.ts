import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/api/client';
import { keys } from '@/api/keys';
import { useScopedSiteCode, useSiteReady } from '@/features/sites/sites';
import type { GeneratePassportResponse, Paged, Passport, PassportSummary } from '@/api/types';

export interface PassportFilters {
  status?: string;
  q?: string;
}
export function usePassports(filters: PassportFilters) {
  const site = useScopedSiteCode();
  const ready = useSiteReady();
  const scoped: PassportFilters & { siteCode?: string } = { ...filters };
  if (site) scoped.siteCode = site;
  return useQuery({
    queryKey: keys.passportList(scoped),
    queryFn: () => api.get<Paged<PassportSummary>>('/passports', { ...scoped }),
    enabled: ready,
  });
}
export function usePassport(serial: string | undefined) {
  return useQuery({ queryKey: keys.passport(serial ?? ''), queryFn: () => api.get<Passport>(`/passports/${encodeURIComponent(serial ?? '')}`), enabled: !!serial });
}
function invalidate(qc: ReturnType<typeof useQueryClient>) {
  void qc.invalidateQueries({ queryKey: keys.passports });
  void qc.invalidateQueries({ queryKey: ['dashboard', 'quality'] as const });
  void qc.invalidateQueries({ queryKey: ['dashboard', 'kpis'] as const });
}
export function useApprovePassport(serial: string) {
  const qc = useQueryClient();
  return useMutation({ mutationFn: () => api.post<Passport>(`/passports/${encodeURIComponent(serial)}/approve`), onSuccess: () => invalidate(qc) });
}
export function useGeneratePassport(serial: string) {
  const qc = useQueryClient();
  return useMutation({ mutationFn: () => api.post<GeneratePassportResponse>(`/passports/${encodeURIComponent(serial)}/generate`), onSuccess: () => invalidate(qc) });
}
