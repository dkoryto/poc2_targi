import { useEffect, useRef, useState, type ReactNode } from 'react';
import { X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import s from './ui.module.css';
import { IconButton } from './Button';

const FOCUSABLE =
  'a[href]:not([hidden]), button:not([disabled]):not([hidden]), input:not([disabled]):not([hidden]), select:not([disabled]):not([hidden]), textarea:not([disabled]):not([hidden]), [tabindex]:not([tabindex="-1"]):not([hidden])';

export interface SheetProps {
  open: boolean;
  onClose: () => void;
  title: ReactNode;
  children: ReactNode;
  /** Extra controls in the header, before the close button. */
  actions?: ReactNode;
  /** Sticky footer (form actions). Reachable by thumb on mobile. */
  footer?: ReactNode;
  /** Desktop placement. On mobile every sheet slides up from the bottom, full width. */
  side?: 'right' | 'left';
  wide?: boolean;
  'data-testid'?: string;
}

/**
 * Side panel on desktop, full-height sheet sliding up from the bottom on mobile
 * (docs/architecture/responsive.md). Focus is trapped while open, Escape closes,
 * a downward swipe on the grab handle closes it on touch devices.
 */
export function Sheet({ open, onClose, title, children, actions, footer, side = 'right', wide, ...rest }: SheetProps) {
  const { t } = useTranslation();
  const ref = useRef<HTMLDivElement>(null);
  const [drag, setDrag] = useState(0);
  const touchStart = useRef<number | null>(null);

  useEffect(() => {
    if (!open) return;
    const prev = document.activeElement as HTMLElement | null;
    const el = ref.current;
    const first = el?.querySelector<HTMLElement>(FOCUSABLE);
    (first ?? el)?.focus();

    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        onClose();
        return;
      }
      if (e.key !== 'Tab' || !el) return;
      const nodes = Array.from(el.querySelectorAll<HTMLElement>(FOCUSABLE));
      if (nodes.length === 0) return;
      const firstNode = nodes[0]!;
      const lastNode = nodes[nodes.length - 1]!;
      if (e.shiftKey && document.activeElement === firstNode) {
        e.preventDefault();
        lastNode.focus();
      } else if (!e.shiftKey && document.activeElement === lastNode) {
        e.preventDefault();
        firstNode.focus();
      }
    };
    document.addEventListener('keydown', onKey);
    // Stop the page behind the sheet from scrolling under the user's finger.
    const prevOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    return () => {
      document.removeEventListener('keydown', onKey);
      document.body.style.overflow = prevOverflow;
      prev?.focus?.();
    };
  }, [open, onClose]);

  useEffect(() => {
    if (!open) setDrag(0);
  }, [open]);

  if (!open) return null;

  return (
    <>
      <div className={s.sheetBackdrop} onMouseDown={onClose} aria-hidden />
      <aside
        ref={ref}
        className={[s.sheet, side === 'left' && s.sheetLeft, wide && s.sheetWide].filter(Boolean).join(' ')}
        style={drag ? { transform: `translateY(${drag}px)` } : undefined}
        role="dialog"
        aria-modal="true"
        aria-label={typeof title === 'string' ? title : undefined}
        tabIndex={-1}
        data-testid={rest['data-testid']}
      >
        <div
          className={s.sheetGrab}
          aria-hidden
          onTouchStart={(e) => {
            touchStart.current = e.touches[0]?.clientY ?? null;
          }}
          onTouchMove={(e) => {
            if (touchStart.current == null) return;
            const dy = (e.touches[0]?.clientY ?? 0) - touchStart.current;
            if (dy > 0) setDrag(dy);
          }}
          onTouchEnd={() => {
            if (drag > 90) onClose();
            else setDrag(0);
            touchStart.current = null;
          }}
        />
        <div className={s.sheetHeader}>
          <h2 style={{ flex: 1, minWidth: 0 }}>{title}</h2>
          {actions}
          <IconButton label={t('common.close')} onClick={onClose} data-close>
            <X size={18} />
          </IconButton>
        </div>
        <div className={s.sheetBody}>{children}</div>
        {footer && <div className={s.sheetFooter}>{footer}</div>}
      </aside>
    </>
  );
}
