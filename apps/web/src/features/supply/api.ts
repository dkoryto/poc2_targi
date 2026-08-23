import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, requestRaw } from '@/api/client';
import { keys } from '@/api/keys';
import type { DocumentSummary, EtaChangeRequest, EtaChangeResponse, ImpactResponse, Paged, PoLinePatch, PurchaseOrderDetail, PurchaseOrderLine, PurchaseOrderSummary, Shipment, ShipmentAdviceRequest, Supplier } from '@/api/types';

export interface PoFilters {
  status?: string;
  supplierCode?: string;
  riskCategory?: string;
  siteCode?: string;
  dueFrom?: string;
  dueTo?: string;
  q?: string;
}

export function useSuppliers() {
  return useQuery({ queryKey: keys.suppliers, queryFn: () => api.get<Paged<Supplier>>('/suppliers') });
}
export function usePurchaseOrders(filters: PoFilters) {
  return useQuery({ queryKey: keys.purchaseOrders.list(filters), queryFn: () => api.get<Paged<PurchaseOrderSummary>>('/purchase-orders', { ...filters }) });
}
export function usePurchaseOrder(code: string | undefined) {
  return useQuery({ queryKey: keys.purchaseOrders.detail(code ?? ''), queryFn: () => api.get<PurchaseOrderDetail>(`/purchase-orders/${code}`), enabled: !!code });
}
export function useLineImpact(code: string, lineId: string | null) {
  return useQuery({ queryKey: keys.purchaseOrders.impact(code, lineId ?? ''), queryFn: () => api.get<ImpactResponse>(`/purchase-orders/${code}/lines/${lineId}/impact`), enabled: !!lineId });
}
export function usePatchLine(code: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ lineId, patch, rowVersion }: { lineId: string; patch: PoLinePatch; rowVersion: string }) => {
      const res = await requestRaw<PurchaseOrderLine>(`/purchase-orders/${code}/lines/${lineId}`, { method: 'PATCH', body: patch, ifMatch: rowVersion });
      return res.data;
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: keys.purchaseOrders.all });
      void qc.invalidateQueries({ queryKey: keys.dashboard.all });
    },
  });
}
export function useChangeEta(code: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ lineId, body }: { lineId: string; body: EtaChangeRequest }) => api.post<EtaChangeResponse>(`/purchase-orders/${code}/lines/${lineId}/eta`, body),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: keys.purchaseOrders.all });
      void qc.invalidateQueries({ queryKey: keys.dashboard.all });
      void qc.invalidateQueries({ queryKey: keys.shipments.all });
    },
  });
}
export function useUploadDocument(code: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (fd: FormData) => api.upload<DocumentSummary>('/documents', fd),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: keys.purchaseOrders.detail(code) });
      void qc.invalidateQueries({ queryKey: keys.dashboard.all });
    },
  });
}
export function useCreateAdvice() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: ShipmentAdviceRequest) => api.post<Shipment>('/shipments', body),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: keys.shipments.all });
      void qc.invalidateQueries({ queryKey: keys.purchaseOrders.all });
      void qc.invalidateQueries({ queryKey: keys.dashboard.all });
    },
  });
}
