import type { ReactNode } from 'react';
import s from './ui.module.css';

export interface TabItem {
  key: string;
  label: ReactNode;
}
export function Tabs({ items, value, onChange }: { items: TabItem[]; value: string; onChange: (k: string) => void }) {
  return (
    <div role="tablist" className={s.tabs}>
      {items.map((it) => (
        <button
          key={it.key}
          role="tab"
          type="button"
          className={s.tab}
          aria-selected={value === it.key}
          onClick={() => onChange(it.key)}
          onKeyDown={(e) => {
            const idx = items.findIndex((i) => i.key === it.key);
            if (e.key === 'ArrowRight') onChange(items[(idx + 1) % items.length]!.key);
            if (e.key === 'ArrowLeft') onChange(items[(idx - 1 + items.length) % items.length]!.key);
          }}
        >
          {it.label}
        </button>
      ))}
    </div>
  );
}
