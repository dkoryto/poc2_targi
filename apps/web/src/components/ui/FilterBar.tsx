import { useState, type ReactNode } from 'react';
import { SlidersHorizontal, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import s from './ui.module.css';
import { Badge } from './Misc';
import { useIsMobile } from './useBreakpoint';

export interface FilterBarProps {
  children: ReactNode;
  /** How many filters are currently narrowing the result — shown on the mobile toggle. */
  activeCount?: number;
  onClear?: () => void;
  clearLabel?: string;
  'data-testid'?: string;
}

/**
 * Filter row. On desktop the filters sit inline as before; below `md` they collapse
 * behind a "Filtry" button carrying a count of the active ones, so the table gets the
 * full width (docs/architecture/responsive.md).
 */
export function FilterBar({ children, activeCount = 0, onClear, clearLabel, ...rest }: FilterBarProps) {
  const { t } = useTranslation();
  const isMobile = useIsMobile();
  const [open, setOpen] = useState(false);

  if (!isMobile) {
    return (
      <div className={s.filterBar} data-testid={rest['data-testid'] ?? 'filter-bar'}>
        {children}
        {onClear && (
          <button type="button" className={s.filterClear} onClick={onClear}>
            {clearLabel ?? t('common.clearFilters')}
          </button>
        )}
      </div>
    );
  }

  return (
    <div className={s.filterBarMobile} data-testid={rest['data-testid'] ?? 'filter-bar'}>
      <button
        type="button"
        className={s.filterToggle}
        aria-expanded={open}
        onClick={() => setOpen((o) => !o)}
        data-testid="filter-toggle"
      >
        {open ? <X size={15} aria-hidden /> : <SlidersHorizontal size={15} aria-hidden />}
        {t('common.filters')}
        {activeCount > 0 && <Badge count={activeCount} />}
      </button>
      {open && (
        <div className={s.filterPanel} data-testid="filter-panel">
          {children}
          {onClear && (
            <button type="button" className={s.filterClear} onClick={onClear}>
              {clearLabel ?? t('common.clearFilters')}
            </button>
          )}
        </div>
      )}
    </div>
  );
}
