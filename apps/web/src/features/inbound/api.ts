import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/api/client';
import { keys } from '@/api/keys';
import { useSiteCode, useSiteParam, useSiteReady } from '@/features/sites/sites';
import type { LogisticsEvent, LogisticsEventRequest, Paged, Shipment, ShipmentEvent } from '@/api/types';

export function useShipments() {
  const params = useSiteParam();
  const ready = useSiteReady();
  return useQuery({
    queryKey: keys.shipments.list(useSiteCode()),
    queryFn: () => api.get<Paged<Shipment>>('/shipments', params),
    enabled: ready,
    refetchInterval: 30_000,
  });
}
export function useShipment(code: string | undefined) {
  return useQuery({ queryKey: keys.shipments.detail(code ?? ''), queryFn: () => api.get<Shipment>(`/shipments/${code}`), enabled: !!code });
}
export function useAddShipmentEvent(code: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: { type: string; occurredAt: string; note?: string }) => api.post<ShipmentEvent>(`/shipments/${code}/events`, body),
    onSuccess: () => void qc.invalidateQueries({ queryKey: keys.shipments.all }),
  });
}
export function useLogisticsEvents() {
  const params = useSiteParam();
  const ready = useSiteReady();
  return useQuery({
    queryKey: keys.logisticsEventList(useSiteCode()),
    queryFn: () => api.get<Paged<LogisticsEvent>>('/logistics-events', params),
    enabled: ready,
    refetchInterval: 30_000,
  });
}
export function useRaiseLogisticsEvent() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: LogisticsEventRequest) => api.post<LogisticsEvent>('/logistics-events', body),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: keys.logisticsEvents });
      void qc.invalidateQueries({ queryKey: keys.shipments.all });
      void qc.invalidateQueries({ queryKey: keys.dashboard.all });
      void qc.invalidateQueries({ queryKey: keys.purchaseOrders.all });
    },
  });
}
