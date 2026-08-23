import { useEffect, type ReactNode } from 'react';
import { X } from 'lucide-react';
import s from './ui.module.css';
import { IconButton } from './Button';

export function Drawer({ open, onClose, title, children, wide, actions }: { open: boolean; onClose: () => void; title: ReactNode; children: ReactNode; wide?: boolean; actions?: ReactNode }) {
  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose(); };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [open, onClose]);
  if (!open) return null;
  return (
    <>
      <div className={s.drawerBackdrop} onMouseDown={onClose} aria-hidden />
      <aside className={[s.drawer, wide && s.drawerWide].filter(Boolean).join(' ')} role="dialog" aria-modal="true" aria-label={typeof title === 'string' ? title : undefined}>
        <div className={s.drawerHeader}>
          <h2 style={{ flex: 1 }}>{title}</h2>
          {actions}
          <IconButton label="close" onClick={onClose}>
            <X size={16} />
          </IconButton>
        </div>
        <div className={s.drawerBody}>{children}</div>
      </aside>
    </>
  );
}
