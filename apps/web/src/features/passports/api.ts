import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/api/client';
import { keys } from '@/api/keys';
import type { GeneratePassportResponse, Paged, Passport, PassportSummary } from '@/api/types';

export interface PassportFilters {
  status?: string;
  q?: string;
}
export function usePassports(filters: PassportFilters) {
  return useQuery({ queryKey: keys.passportList(filters), queryFn: () => api.get<Paged<PassportSummary>>('/passports', { ...filters }) });
}
export function usePassport(serial: string | undefined) {
  return useQuery({ queryKey: keys.passport(serial ?? ''), queryFn: () => api.get<Passport>(`/passports/${encodeURIComponent(serial ?? '')}`), enabled: !!serial });
}
function invalidate(qc: ReturnType<typeof useQueryClient>) {
  void qc.invalidateQueries({ queryKey: keys.passports });
  void qc.invalidateQueries({ queryKey: keys.dashboard.quality });
  void qc.invalidateQueries({ queryKey: keys.dashboard.kpis });
}
export function useApprovePassport(serial: string) {
  const qc = useQueryClient();
  return useMutation({ mutationFn: () => api.post<Passport>(`/passports/${encodeURIComponent(serial)}/approve`), onSuccess: () => invalidate(qc) });
}
export function useGeneratePassport(serial: string) {
  const qc = useQueryClient();
  return useMutation({ mutationFn: () => api.post<GeneratePassportResponse>(`/passports/${encodeURIComponent(serial)}/generate`), onSuccess: () => invalidate(qc) });
}
