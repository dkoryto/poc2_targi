import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/api/client';
import { keys } from '@/api/keys';
import type { AuditEvent, BlockLotRequest, BlockLotResponse, Inspection, InspectionRequest, Lot, LotForward, LotSummary, Paged, SerialTrace, TraceSearchHit } from '@/api/types';

export interface LotFilters {
  partCode?: string;
  status?: string;
  q?: string;
}
export interface AuditFilters {
  entity?: string;
  code?: string;
  user?: string;
  from?: string;
  to?: string;
  page?: number;
}

export function useTraceSearch(q: string) {
  return useQuery({ queryKey: keys.trace.search(q), queryFn: () => api.get<TraceSearchHit[]>('/trace/search', { q }), enabled: q.trim().length >= 2, staleTime: 30_000 });
}
export function useSerial(serial: string | undefined) {
  return useQuery({ queryKey: keys.trace.serial(serial ?? ''), queryFn: () => api.get<SerialTrace>(`/trace/serials/${encodeURIComponent(serial ?? '')}`), enabled: !!serial });
}
export function useLots(filters: LotFilters, enabled = true) {
  return useQuery({ queryKey: keys.lotList(filters), queryFn: () => api.get<Paged<LotSummary>>('/lots', { ...filters }), enabled });
}
export function useLot(lotNumber: string | undefined) {
  return useQuery({ queryKey: keys.lot(lotNumber ?? ''), queryFn: () => api.get<Lot>(`/lots/${encodeURIComponent(lotNumber ?? '')}`), enabled: !!lotNumber });
}
export function useLotForward(lotNumber: string | undefined) {
  return useQuery({ queryKey: keys.lotForward(lotNumber ?? ''), queryFn: () => api.get<LotForward>(`/trace/lots/${encodeURIComponent(lotNumber ?? '')}/forward`), enabled: !!lotNumber });
}
export function useBlockLot(lotNumber: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: BlockLotRequest) => api.post<BlockLotResponse>(`/lots/${encodeURIComponent(lotNumber)}/block`, body),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: keys.lots });
      void qc.invalidateQueries({ queryKey: keys.passports });
      void qc.invalidateQueries({ queryKey: keys.dashboard.all });
      void qc.invalidateQueries({ queryKey: keys.planning.all });
    },
  });
}
export function useAddInspection(lotNumber: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: InspectionRequest) => api.post<Inspection>(`/lots/${encodeURIComponent(lotNumber)}/inspections`, body),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: keys.lots });
      void qc.invalidateQueries({ queryKey: keys.passports });
      void qc.invalidateQueries({ queryKey: keys.dashboard.quality });
    },
  });
}
export function useTraceAudit(filters: AuditFilters, enabled = true) {
  return useQuery({ queryKey: keys.trace.audit(filters), queryFn: () => api.get<Paged<AuditEvent>>('/trace/audit', { ...filters }), enabled });
}
