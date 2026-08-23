import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/api/client';
import { keys } from '@/api/keys';
import type { LogisticsEvent, LogisticsEventRequest, Paged, Shipment, ShipmentEvent } from '@/api/types';

export function useShipments() {
  return useQuery({ queryKey: keys.shipments.all, queryFn: () => api.get<Paged<Shipment>>('/shipments'), refetchInterval: 30_000 });
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
  return useQuery({ queryKey: keys.logisticsEvents, queryFn: () => api.get<Paged<LogisticsEvent>>('/logistics-events'), refetchInterval: 30_000 });
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
