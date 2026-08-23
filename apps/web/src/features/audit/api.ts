import { useQuery } from '@tanstack/react-query';
import { api } from '@/api/client';
import { keys } from '@/api/keys';
import { useScopedSiteCode, useSiteReady } from '@/features/sites/sites';
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
  const site = useScopedSiteCode();
  const ready = useSiteReady();
  const scoped: AuditFilters & { siteCode?: string } = { ...filters };
  if (site) scoped.siteCode = site;
  return useQuery({
    queryKey: keys.audit(scoped),
    queryFn: () => api.get<Paged<AuditEvent>>('/audit', { ...scoped }),
    enabled: ready,
  });
}
