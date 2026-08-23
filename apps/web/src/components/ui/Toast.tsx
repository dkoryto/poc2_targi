import { createContext, useCallback, useContext, useMemo, useRef, useState, type ReactNode } from 'react';
import { CheckCircle2, AlertTriangle, OctagonAlert, Info, X } from 'lucide-react';
import s from './ui.module.css';

export type ToastTone = 'ok' | 'warn' | 'critical' | 'info';
export interface ToastItem {
  id: number;
  tone: ToastTone;
  title: string;
  message?: string;
}
interface ToastApi {
  push: (t: Omit<ToastItem, 'id'>) => void;
  ok: (title: string, message?: string) => void;
  warn: (title: string, message?: string) => void;
  critical: (title: string, message?: string) => void;
  info: (title: string, message?: string) => void;
}
const Ctx = createContext<ToastApi | null>(null);

export function ToastProvider({ children }: { children: ReactNode }) {
  const [items, setItems] = useState<ToastItem[]>([]);
  const seq = useRef(0);
  const push = useCallback((t: Omit<ToastItem, 'id'>) => {
    const id = ++seq.current;
    setItems((prev) => [...prev.slice(-4), { ...t, id }]);
    window.setTimeout(() => setItems((prev) => prev.filter((i) => i.id !== id)), t.tone === 'critical' ? 8000 : 5000);
  }, []);
  const api = useMemo<ToastApi>(
    () => ({
      push,
      ok: (title, message) => push({ tone: 'ok', title, message }),
      warn: (title, message) => push({ tone: 'warn', title, message }),
      critical: (title, message) => push({ tone: 'critical', title, message }),
      info: (title, message) => push({ tone: 'info', title, message }),
    }),
    [push],
  );
  const icon = { ok: <CheckCircle2 size={16} color="var(--ok)" />, warn: <AlertTriangle size={16} color="var(--warn)" />, critical: <OctagonAlert size={16} color="var(--crit)" />, info: <Info size={16} color="var(--info)" /> };
  return (
    <Ctx.Provider value={api}>
      {children}
      <div className={s.toasts} aria-live="polite" aria-atomic="false">
        {items.map((it) => (
          <div key={it.id} className={[s.toast, s[`toast${it.tone[0]!.toUpperCase()}${it.tone.slice(1)}`]].join(' ')} role="status" data-testid="toast">
            {icon[it.tone]}
            <div style={{ flex: 1 }}>
              <div style={{ fontWeight: 600 }}>{it.title}</div>
              {it.message && <div className="muted">{it.message}</div>}
            </div>
            <button type="button" className={s.iconBtn} style={{ width: 22, height: 22 }} aria-label="close" onClick={() => setItems((prev) => prev.filter((i) => i.id !== it.id))}>
              <X size={14} />
            </button>
          </div>
        ))}
      </div>
    </Ctx.Provider>
  );
}

export function useToast(): ToastApi {
  const ctx = useContext(Ctx);
  if (!ctx) throw new Error('ToastProvider missing');
  return ctx;
}
