import { useQuery } from '@tanstack/react-query';
import { api } from '@/api/client';
import { keys } from '@/api/keys';
import { useSiteCode, useSiteParam, useSiteReady } from '@/features/sites/sites';
import type { GanttData, KpiResponse, MapData, QualityStatus, RiskHeatmap } from '@/api/types';

/** Every dashboard panel is scoped to the active plant and refetches when it changes. */
function useSiteQuery<T>(key: readonly unknown[], path: string) {
  const params = useSiteParam();
  const ready = useSiteReady();
  return useQuery({
    queryKey: key,
    queryFn: () => api.get<T>(path, params),
    enabled: ready,
    refetchInterval: 30_000,
  });
}

export function useKpis() {
  return useSiteQuery<KpiResponse>(keys.dashboard.kpis(useSiteCode()), '/dashboard/kpis');
}
export function useMapData() {
  return useSiteQuery<MapData>(keys.dashboard.map(useSiteCode()), '/dashboard/map');
}
export function useHeatmap() {
  return useSiteQuery<RiskHeatmap>(keys.dashboard.heatmap(useSiteCode()), '/dashboard/risk-heatmap');
}
export function useQualityStatus() {
  return useSiteQuery<QualityStatus>(keys.dashboard.quality(useSiteCode()), '/dashboard/quality-status');
}
export function usePlan() {
  return useSiteQuery<GanttData>(keys.dashboard.plan(useSiteCode()), '/dashboard/plan');
}
