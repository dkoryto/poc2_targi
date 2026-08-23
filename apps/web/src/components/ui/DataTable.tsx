import { useMemo, useState, type ReactNode } from 'react';
import { ChevronDown, ChevronUp, ChevronsUpDown } from 'lucide-react';
import s from './ui.module.css';
import { EmptyState, ErrorState, Skeleton } from './States';

export interface Column<T> {
  key: string;
  header: ReactNode;
  render: (row: T) => ReactNode;
  sortValue?: (row: T) => string | number | null | undefined;
  align?: 'left' | 'right';
  width?: string | number;
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
  ...rest
}: DataTableProps<T>) {
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
