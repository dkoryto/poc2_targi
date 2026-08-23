import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/api/client';
import { keys } from '@/api/keys';
import { useSiteCode, useSiteParam, useSiteReady } from '@/features/sites/sites';
import type { Notification, Paged } from '@/api/types';

export function useNotifications(enabled = true, unreadOnly = false) {
  const site = useSiteCode();
  const params = useSiteParam();
  const ready = useSiteReady();
  return useQuery({
    queryKey: [...keys.notifications, { unreadOnly, site }],
    queryFn: () => api.get<Paged<Notification>>('/notifications', { unreadOnly: unreadOnly || undefined, ...params }),
    enabled: enabled && ready,
    refetchInterval: 30_000,
  });
}
export function useMarkRead() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.post<void>(`/notifications/${id}/read`),
    onSuccess: () => void qc.invalidateQueries({ queryKey: keys.notifications }),
  });
}
