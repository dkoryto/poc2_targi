import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/api/client';
import { keys } from '@/api/keys';
import type { Notification, Paged } from '@/api/types';

export function useNotifications(enabled = true, unreadOnly = false) {
  return useQuery({
    queryKey: [...keys.notifications, { unreadOnly }],
    queryFn: () => api.get<Paged<Notification>>('/notifications', { unreadOnly: unreadOnly || undefined }),
    enabled,
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
