import { useEffect, useRef, type ReactNode } from 'react';
import { X, AlertTriangle } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import s from './ui.module.css';
import { Button, IconButton } from './Button';

export interface DialogProps {
  open: boolean;
  onClose: () => void;
  title: ReactNode;
  children: ReactNode;
  footer?: ReactNode;
  size?: 'md' | 'lg';
}

export function Dialog({ open, onClose, title, children, footer, size = 'md' }: DialogProps) {
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!open) return;
    const prev = document.activeElement as HTMLElement | null;
    const el = ref.current;
    const focusable = el?.querySelector<HTMLElement>('input, select, textarea, button:not([data-close])');
    (focusable ?? el)?.focus();
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
      if (e.key === 'Tab' && el) {
        const nodes = Array.from(el.querySelectorAll<HTMLElement>('a, button, input, select, textarea, [tabindex]:not([tabindex="-1"])')).filter((n) => !n.hasAttribute('disabled'));
        if (nodes.length === 0) return;
        const first = nodes[0]!;
        const last = nodes[nodes.length - 1]!;
        if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last.focus(); }
        else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus(); }
      }
    };
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('keydown', onKey);
      prev?.focus();
    };
  }, [open, onClose]);
  if (!open) return null;
  return (
    <div className={s.backdrop} onMouseDown={(e) => { if (e.target === e.currentTarget) onClose(); }} role="presentation">
      <div ref={ref} role="dialog" aria-modal="true" aria-labelledby="dlg-title" className={[s.dialog, size === 'lg' && s.dialogLg].filter(Boolean).join(' ')} tabIndex={-1}>
        <div className={s.dialogHeader}>
          <h2 id="dlg-title" style={{ flex: 1 }}>{title}</h2>
          <IconButton label="close" onClick={onClose} data-close>
            <X size={16} />
          </IconButton>
        </div>
        <div className={s.dialogBody}>{children}</div>
        {footer && <div className={s.dialogFooter}>{footer}</div>}
      </div>
    </div>
  );
}

export function ConfirmDialog({
  open,
  onClose,
  onConfirm,
  title,
  children,
  impact,
  confirmLabel,
  danger,
  loading,
}: {
  open: boolean;
  onClose: () => void;
  onConfirm: () => void;
  title: ReactNode;
  children?: ReactNode;
  impact?: ReactNode;
  confirmLabel?: string;
  danger?: boolean;
  loading?: boolean;
}) {
  const { t } = useTranslation();
  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={title}
      footer={
        <>
          <Button variant="ghost" onClick={onClose} disabled={loading}>
            {t('common.cancel')}
          </Button>
          <Button variant={danger ? 'danger' : 'primary'} onClick={onConfirm} loading={loading} data-testid="confirm-button">
            {confirmLabel ?? t('common.confirm')}
          </Button>
        </>
      }
    >
      {children}
      {impact && (
        <div className={s.impact} role="note">
          <AlertTriangle size={16} aria-hidden style={{ flexShrink: 0 }} />
          <div>{impact}</div>
        </div>
      )}
    </Dialog>
  );
}
