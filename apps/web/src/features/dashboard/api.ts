import { useQuery } from '@tanstack/react-query';
import { api } from '@/api/client';
import { keys } from '@/api/keys';
import type { GanttData, KpiResponse, MapData, QualityStatus, RiskHeatmap } from '@/api/types';

export function useKpis() {
  return useQuery({ queryKey: keys.dashboard.kpis, queryFn: () => api.get<KpiResponse>('/dashboard/kpis'), refetchInterval: 30_000 });
}
export function useMapData() {
  return useQuery({ queryKey: keys.dashboard.map, queryFn: () => api.get<MapData>('/dashboard/map'), refetchInterval: 30_000 });
}
export function useHeatmap() {
  return useQuery({ queryKey: keys.dashboard.heatmap, queryFn: () => api.get<RiskHeatmap>('/dashboard/risk-heatmap'), refetchInterval: 30_000 });
}
export function useQualityStatus() {
  return useQuery({ queryKey: keys.dashboard.quality, queryFn: () => api.get<QualityStatus>('/dashboard/quality-status'), refetchInterval: 30_000 });
}
export function usePlan() {
  return useQuery({ queryKey: keys.dashboard.plan, queryFn: () => api.get<GanttData>('/dashboard/plan'), refetchInterval: 30_000 });
}
