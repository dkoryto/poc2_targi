import { useEffect, useId, useRef, useState, type ReactNode } from 'react';
import { MoreHorizontal } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import s from './ui.module.css';

export interface OverflowMenuProps {
  /**
   * Controls that do not fit on a narrow screen. They are rendered inside the popover,
   * each on its own row with its label — never dropped (contract rule 2).
   */
  children: ReactNode;
  label?: string;
  'data-testid'?: string;
}

/**
 * The "⋯" menu in the top bar. Everything that cannot fit at a given width moves in
 * here rather than disappearing.
 */
export function OverflowMenu({ children, label, ...rest }: OverflowMenuProps) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const wrapRef = useRef<HTMLDivElement>(null);
  const panelRef = useRef<HTMLDivElement>(null);
  const id = useId();

  useEffect(() => {
    if (!open) return;
    const onDoc = (e: MouseEvent) => {
      if (!wrapRef.current?.contains(e.target as Node)) setOpen(false);
    };
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setOpen(false);
    };
    // Choosing something inside closes the menu. Attached natively rather than as a JSX
    // onClick so the panel stays a non-interactive container for a11y purposes.
    const panel = panelRef.current;
    const onPick = (e: Event) => {
      if ((e.target as HTMLElement).closest('button, a, select, input')) setOpen(false);
    };
    panel?.addEventListener('click', onPick);
    document.addEventListener('mousedown', onDoc);
    document.addEventListener('keydown', onKey);
    return () => {
      panel?.removeEventListener('click', onPick);
      document.removeEventListener('mousedown', onDoc);
      document.removeEventListener('keydown', onKey);
    };
  }, [open]);

  return (
    <div className={s.overflowWrap} ref={wrapRef}>
      <button
        type="button"
        className={s.overflowBtn}
        aria-haspopup="true"
        aria-expanded={open}
        aria-controls={open ? id : undefined}
        aria-label={label ?? t('topbar.more')}
        title={label ?? t('topbar.more')}
        onClick={() => setOpen((o) => !o)}
        data-testid={rest['data-testid'] ?? 'overflow-menu'}
      >
        <MoreHorizontal size={18} aria-hidden />
      </button>
      {open && (
        <div className={s.overflowPanel} id={id} ref={panelRef} role="group" aria-label={label ?? t('topbar.more')}>
          {children}
        </div>
      )}
    </div>
  );
}

/** One labelled row inside {@link OverflowMenu}; wraps an existing control unchanged. */
export function OverflowItem({ label, children }: { label: ReactNode; children: ReactNode }) {
  return (
    <div className={s.overflowItem}>
      <span className={s.overflowLabel}>{label}</span>
      <div className={s.overflowControl}>{children}</div>
    </div>
  );
}
