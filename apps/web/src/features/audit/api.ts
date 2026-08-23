import { useQuery } from '@tanstack/react-query';
import { api } from '@/api/client';
import { keys } from '@/api/keys';
import type { AuditEvent, Paged } from '@/api/types';

export interface AuditFilters {
  entity?: string;
  code?: string;
  user?: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}
export function useAudit(filters: AuditFilters) {
  return useQuery({ queryKey: keys.audit(filters), queryFn: () => api.get<Paged<AuditEvent>>('/audit', { ...filters }) });
}
