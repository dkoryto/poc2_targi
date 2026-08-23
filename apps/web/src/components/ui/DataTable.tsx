import { useMemo, useState, type ReactNode } from 'react';
import { ChevronDown, ChevronUp, ChevronsUpDown } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import s from './ui.module.css';
import { EmptyState, ErrorState, Skeleton } from './States';
import { useIsMobile } from './useBreakpoint';

export interface Column<T> {
  key: string;
  header: ReactNode;
  render: (row: T) => ReactNode;
  sortValue?: (row: T) => string | number | null | undefined;
  align?: 'left' | 'right';
  width?: string | number;
  /**
   * Card layout only. `title` becomes the card heading, `meta` sits next to it without a
   * label, `hidden` is dropped from the card. Everything else renders as a label/value row.
   */
  card?: 'title' | 'meta' | 'hidden';
}

export interface DataTableProps<T> {
  columns: Column<T>[];
  rows: T[] | undefined;
  rowKey: (row: T) => string;
  loading?: boolean;
  error?: unknown;
  onRetry?: () => void;
  onRowClick?: (row: T) => void;
  selectedKey?: string | null;
  emptyTitle?: string;
  emptyDetail?: string;
  maxHeight?: string | number;
  'data-testid'?: string;
  initialSort?: { key: string; dir: 'asc' | 'desc' };
  /**
   * `"cards"` renders a list of cards instead of rows below the `md` breakpoint, so the
   * columns that used to fall off the right edge stay readable on a phone. Defaults to
   * `"cards"`; pass `"scroll"` to keep the table and scroll it horizontally instead.
   */
  responsive?: 'cards' | 'scroll';
}

export function DataTable<T>({
  columns,
  rows,
  rowKey,
  loading,
  error,
  onRetry,
  onRowClick,
  selectedKey,
  emptyTitle,
  emptyDetail,
  maxHeight,
  initialSort,
  responsive = 'cards',
  ...rest
}: DataTableProps<T>) {
  const { t } = useTranslation();
  const isMobile = useIsMobile();
  const [sort, setSort] = useState<{ key: string; dir: 'asc' | 'desc' } | null>(initialSort ?? null);
  const sorted = useMemo(() => {
    if (!rows) return [];
    if (!sort) return rows;
    const col = columns.find((c) => c.key === sort.key);
    if (!col?.sortValue) return rows;
    const sv = col.sortValue;
    return [...rows].sort((a, b) => {
      const va = sv(a);
      const vb = sv(b);
      if (va == null && vb == null) return 0;
      if (va == null) return 1;
      if (vb == null) return -1;
      const cmp = typeof va === 'number' && typeof vb === 'number' ? va - vb : String(va).localeCompare(String(vb));
      return sort.dir === 'asc' ? cmp : -cmp;
    });
  }, [rows, sort, columns]);

  const toggleSort = (key: string) =>
    setSort((prev) => (prev?.key === key ? { key, dir: prev.dir === 'asc' ? 'desc' : 'asc' } : { key, dir: 'asc' }));

  if (isMobile && responsive === 'cards') {
    const sortable = columns.filter((c) => c.sortValue);
    const titleCol = columns.find((c) => c.card === 'title') ?? columns[0];
    const metaCols = columns.filter((c) => c.card === 'meta');
    const bodyCols = columns.filter((c) => c !== titleCol && c.card !== 'meta' && c.card !== 'hidden');
    return (
      <div className={s.cardList} data-testid={rest['data-testid']}>
        {sortable.length > 0 && (
          <label className={s.cardSort}>
            <span className="sr-only">{t('common.sortBy')}</span>
            <select
              value={sort ? `${sort.key}:${sort.dir}` : ''}
              onChange={(e) => {
                const v = e.target.value;
                if (!v) return setSort(null);
                const [key, dir] = v.split(':');
                setSort({ key: key!, dir: dir as 'asc' | 'desc' });
              }}
              aria-label={t('common.sortBy')}
              data-testid="card-sort"
            >
              <option value="">{t('common.sortBy')}</option>
              {sortable.map((c) => [
                <option key={`${c.key}:asc`} value={`${c.key}:asc`}>{`${typeof c.header === 'string' ? c.header : c.key} ↑`}</option>,
                <option key={`${c.key}:desc`} value={`${c.key}:desc`}>{`${typeof c.header === 'string' ? c.header : c.key} ↓`}</option>,
              ])}
            </select>
          </label>
        )}
        {loading &&
          Array.from({ length: 4 }).map((_, i) => (
            <div key={`sk-${i}`} data-testid="table-skeleton">
              <Skeleton height={72} />
            </div>
          ))}
        {!loading && error != null && <ErrorState error={error} onRetry={onRetry} />}
        {!loading && error == null && sorted.length === 0 && <EmptyState title={emptyTitle} detail={emptyDetail} />}
        {!loading &&
          error == null &&
          sorted.map((row) => {
            const k = rowKey(row);
            const interactive = Boolean(onRowClick);
            return (
              <div
                key={k}
                className={[s.rowCard, interactive && s.rowCardClickable, selectedKey === k && s.rowCardSelected].filter(Boolean).join(' ')}
                onClick={onRowClick ? () => onRowClick(row) : undefined}
                onKeyDown={
                  onRowClick
                    ? (e) => {
                        if (e.key === 'Enter' || e.key === ' ') {
                          e.preventDefault();
                          onRowClick(row);
                        }
                      }
                    : undefined
                }
                role={interactive ? 'button' : undefined}
                tabIndex={interactive ? 0 : undefined}
                data-testid="row-card"
              >
                <div className={s.rowCardHead}>
                  <span className={s.rowCardTitle}>{titleCol?.render(row)}</span>
                  {metaCols.map((c) => (
                    <span key={c.key} className={s.rowCardMeta}>
                      {c.render(row)}
                    </span>
                  ))}
                </div>
                <dl className={s.rowCardFields}>
                  {bodyCols.map((c) => (
                    <div key={c.key} className={s.rowCardField}>
                      <dt>{c.header}</dt>
                      <dd>{c.render(row)}</dd>
                    </div>
                  ))}
                </dl>
              </div>
            );
          })}
      </div>
    );
  }

  return (
    <div className={s.tableWrap} style={{ maxHeight }} data-testid={rest['data-testid']}>
      <table className={s.table}>
        <thead>
          <tr>
            {columns.map((c) => (
              <th key={c.key} style={{ width: c.width, textAlign: c.align }} aria-sort={sort?.key === c.key ? (sort.dir === 'asc' ? 'ascending' : 'descending') : undefined}>
                {c.sortValue ? (
                  <button type="button" onClick={() => toggleSort(c.key)}>
                    {c.header}
                    {sort?.key === c.key ? sort.dir === 'asc' ? <ChevronUp size={12} /> : <ChevronDown size={12} /> : <ChevronsUpDown size={12} opacity={0.5} />}
                  </button>
                ) : (
                  c.header
                )}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {loading &&
            Array.from({ length: 5 }).map((_, i) => (
              <tr key={`sk-${i}`} data-testid="table-skeleton">
                {columns.map((c) => (
                  <td key={c.key}>
                    <Skeleton height={14} />
                  </td>
                ))}
              </tr>
            ))}
          {!loading && error != null && (
            <tr>
              <td colSpan={columns.length}>
                <ErrorState error={error} onRetry={onRetry} />
              </td>
            </tr>
          )}
          {!loading && error == null && sorted.length === 0 && (
            <tr>
              <td colSpan={columns.length}>
                <EmptyState title={emptyTitle} detail={emptyDetail} />
              </td>
            </tr>
          )}
          {!loading &&
            error == null &&
            sorted.map((row) => {
              const k = rowKey(row);
              return (
                <tr
                  key={k}
                  className={[onRowClick && s.rowClickable, selectedKey === k && s.rowSelected].filter(Boolean).join(' ')}
                  onClick={onRowClick ? () => onRowClick(row) : undefined}
                  onKeyDown={onRowClick ? (e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); onRowClick(row); } } : undefined}
                  tabIndex={onRowClick ? 0 : undefined}
                  aria-selected={selectedKey === k || undefined}
                >
                  {columns.map((c) => (
                    <td key={c.key} className={c.align === 'right' ? s.num : undefined}>
                      {c.render(row)}
                    </td>
                  ))}
                </tr>
              );
            })}
        </tbody>
      </table>
    </div>
  );
}
