import { useId, useState, type ReactNode } from 'react';
import s from './ui.module.css';

export function Tooltip({ content, children }: { content: ReactNode; children: ReactNode }) {
  const [open, setOpen] = useState(false);
  const id = useId();
  return (
    <span
      className={s.tooltipWrap}
      onMouseEnter={() => setOpen(true)}
      onMouseLeave={() => setOpen(false)}
      onFocus={() => setOpen(true)}
      onBlur={() => setOpen(false)}
      // eslint-disable-next-line jsx-a11y/no-noninteractive-tabindex -- focusable tooltip trigger (WCAG 1.4.13)
      tabIndex={0}
      aria-describedby={open ? id : undefined}
    >
      {children}
      {open && (
        <span role="tooltip" id={id} className={s.tooltip}>
          {content}
        </span>
      )}
    </span>
  );
}
