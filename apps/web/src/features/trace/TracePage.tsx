import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate, useSearchParams, Link } from 'react-router';
import { Search } from 'lucide-react';
import s from './trace.module.css';
import { useTraceSearch } from './api';
import type { TraceSearchHit } from '@/api/types';
import { SiteChip } from '@/features/sites/SiteSwitch';
import { Card, EmptyState, ErrorState, Input, LoadingState } from '@/components/ui';

export const QUICK = ['PMV-2026-0007', 'HTS-22-2608', 'SCM-2026-0103', 'WO-2026-014'];

export function hitRoute(h: TraceSearchHit): string {
  switch (h.kind) {
    case 'Serial':
      return `/trace/serials/${encodeURIComponent(h.code)}`;
    case 'Lot':
    case 'Heat':
      return `/trace/lots/${encodeURIComponent(h.code)}`;
    case 'PurchaseOrder':
      return `/supply/orders/${encodeURIComponent(h.code)}`;
    case 'Passport':
      return `/passports/${encodeURIComponent(h.code)}`;
    case 'Order':
      return `/planning`;
    default:
      return `/trace?q=${encodeURIComponent(h.code)}`;
  }
}

function useDebounced<T>(value: T, ms: number): T {
  const [v, setV] = useState(value);
  useEffect(() => {
    const h = window.setTimeout(() => setV(value), ms);
    return () => window.clearTimeout(h);
  }, [value, ms]);
  return v;
}

export function TracePage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [params, setParams] = useSearchParams();
  const [q, setQ] = useState(params.get('q') ?? '');
  const dq = useDebounced(q, 250);
  const search = useTraceSearch(dq);

  useEffect(() => {
    const p = params.get('q') ?? '';
    if (p && p !== q) setQ(p);
    // eslint-disable-next-line react-hooks/exhaustive-deps -- sync from URL only
  }, [params]);

  const groups = useMemo(() => {
    const m = new Map<string, TraceSearchHit[]>();
    for (const h of search.data ?? []) m.set(h.kind, [...(m.get(h.kind) ?? []), h]);
    return [...m.entries()];
  }, [search.data]);

  const submit = (value: string) => {
    setQ(value);
    setParams(value ? { q: value } : {});
    // exact single-kind hit → go straight there
    const hits = search.data ?? [];
    const exact = hits.find((h) => h.code.toLowerCase() === value.toLowerCase());
    if (exact) navigate(hitRoute(exact));
  };

  return (
    <div className="page" data-testid="trace-page">
      <div className="page-header">
        <div>
          <h1>{t('trace.title')}</h1>
          <p>{t('trace.subtitle')}</p>
        </div>
        <Link to="/trace/lots">{t('trace.allLots')} →</Link>
      </div>
      <Card title={t('trace.search')} definition={t('trace.searchDef')}>
        <form className="stack" onSubmit={(e) => { e.preventDefault(); submit(q); }}>
          <div className={s.searchBox}>
            <Search size={16} aria-hidden />
            <Input value={q} onChange={(e) => { setQ(e.target.value); setParams(e.target.value ? { q: e.target.value } : {}); }} placeholder={t('trace.searchPlaceholder')} aria-label={t('trace.search')} data-testid="trace-search" />
          </div>
          <div className={s.chips} aria-label={t('trace.quick')}>
            {QUICK.map((c) => (
              <button key={c} type="button" className={s.chip} onClick={() => submit(c)} data-testid={`trace-quick-${c}`}>{c}</button>
            ))}
          </div>
        </form>
      </Card>
      {dq.trim().length >= 2 && (
        <Card title={t('trace.results')}>
          {search.isLoading && <LoadingState rows={3} />}
          {search.isError && <ErrorState error={search.error} onRetry={() => search.refetch()} />}
          {search.data && search.data.length === 0 && <EmptyState title={t('trace.noResults')} detail={t('trace.noResultsDetail')} />}
          {groups.map(([kind, hits]) => (
            <div key={kind} className={s.resultGroup}>
              <h3>{t(`trace.kind.${kind}`, { defaultValue: kind })} ({hits.length})</h3>
              {hits.map((h) => (
                <button key={`${h.kind}-${h.code}`} type="button" className={s.hit} onClick={() => navigate(hitRoute(h))} data-testid={`trace-hit-${h.code}`}>
                  <span className="mono">{h.code}</span>
                  <span className="muted">{h.label}</span>
                  <SiteChip code={h.siteCode} />
                </button>
              ))}
            </div>
          ))}
        </Card>
      )}
    </div>
  );
}
