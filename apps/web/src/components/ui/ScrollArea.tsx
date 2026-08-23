import { useCallback, useEffect, useRef, useState, type CSSProperties, type ReactNode } from 'react';
import s from './ui.module.css';

export interface ScrollAreaProps {
  children: ReactNode;
  /** Accessible label for the scrollable region (required for keyboard users). */
  label: string;
  className?: string;
  style?: CSSProperties;
  /** `'x'` (default) scrolls horizontally only; `'both'` also scrolls vertically. */
  axis?: 'x' | 'both';
  'data-testid'?: string;
}

/**
 * Horizontally scrollable container with edge shadows, so wide content (Gantt, heatmap,
 * tables) scrolls inside itself and never widens the page — rule 1 of the responsive
 * contract. Focusable so it can be scrolled with the keyboard.
 */
export function ScrollArea({ children, label, className, style, axis = 'x', ...rest }: ScrollAreaProps) {
  const ref = useRef<HTMLDivElement>(null);
  const [edges, setEdges] = useState({ start: false, end: false });

  const measure = useCallback(() => {
    const el = ref.current;
    if (!el) return;
    const max = el.scrollWidth - el.clientWidth;
    setEdges({ start: el.scrollLeft > 1, end: max > 1 && el.scrollLeft < max - 1 });
  }, []);

  useEffect(() => {
    measure();
    const el = ref.current;
    if (!el || typeof ResizeObserver === 'undefined') return;
    const ro = new ResizeObserver(measure);
    ro.observe(el);
    for (const child of Array.from(el.children)) ro.observe(child);
    return () => ro.disconnect();
  }, [measure, children]);

  return (
    <div
      ref={ref}
      className={[s.scrollArea, axis === 'both' && s.scrollAreaBoth, edges.start && s.scrollAreaStart, edges.end && s.scrollAreaEnd, className].filter(Boolean).join(' ')}
      style={style}
      onScroll={measure}
      role="region"
      aria-label={label}
      // A scrollable region must be reachable by keyboard (WCAG 2.1.1); the element is
      // deliberately non-interactive otherwise.
      // eslint-disable-next-line jsx-a11y/no-noninteractive-tabindex
      tabIndex={0}
      data-testid={rest['data-testid']}
    >
      {children}
    </div>
  );
}
