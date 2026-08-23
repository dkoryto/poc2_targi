import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/api/client';
import { keys } from '@/api/keys';
import { useSiteCode, useSiteParam, useSiteReady } from '@/features/sites/sites';
import type { CreateScenarioRequest, Paged, PlanningBaseline, PlanningScenario, PlanningScenarioSummary, ScenarioCompare, ScenarioPreset } from '@/api/types';

export function useBaseline() {
  const params = useSiteParam();
  const ready = useSiteReady();
  return useQuery({
    queryKey: keys.planning.baseline(useSiteCode()),
    queryFn: () => api.get<PlanningBaseline>('/planning/baseline', params),
    enabled: ready,
  });
}
export function usePresets() {
  const params = useSiteParam();
  const ready = useSiteReady();
  return useQuery({
    queryKey: keys.planning.presets(useSiteCode()),
    queryFn: () => api.get<ScenarioPreset[]>('/planning/scenarios/presets', params),
    enabled: ready,
    staleTime: Infinity,
  });
}
export function useScenarios() {
  const params = useSiteParam();
  const ready = useSiteReady();
  return useQuery({
    queryKey: keys.planning.scenarios(useSiteCode()),
    queryFn: () => api.get<Paged<PlanningScenarioSummary>>('/planning/scenarios', params),
    enabled: ready,
  });
}
export function useScenario(id: string | undefined) {
  return useQuery({
    queryKey: keys.planning.scenario(id ?? ''),
    queryFn: () => api.get<PlanningScenario>(`/planning/scenarios/${id}`),
    enabled: !!id,
    refetchInterval: (q) => (q.state.data?.status === 'Running' ? 1000 : false),
    refetchIntervalInBackground: true,
  });
}
export function useCompare(id: string | undefined, enabled: boolean) {
  return useQuery({ queryKey: keys.planning.compare(id ?? ''), queryFn: () => api.get<ScenarioCompare>(`/planning/scenarios/${id}/compare`), enabled: !!id && enabled });
}
export function useCreateScenario() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateScenarioRequest) => api.post<PlanningScenario>('/planning/scenarios', body),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ['planning', 'scenarios'] }),
  });
}
export function useRunScenario() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.post<{ id: string; status: string }>(`/planning/scenarios/${id}/run`),
    onSuccess: (_r, id) => {
      void qc.invalidateQueries({ queryKey: keys.planning.scenario(id) });
      void qc.invalidateQueries({ queryKey: ['planning', 'scenarios'] });
    },
  });
}
function useScenarioAction(action: 'approve' | 'reject' | 'save') {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.post<PlanningScenario>(`/planning/scenarios/${id}/${action}`),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: keys.planning.all });
      if (action === 'approve') void qc.invalidateQueries({ queryKey: keys.dashboard.all });
    },
  });
}
export const useApproveScenario = () => useScenarioAction('approve');
export const useRejectScenario = () => useScenarioAction('reject');
export const useSaveScenario = () => useScenarioAction('save');
