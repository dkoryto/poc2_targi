import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router';
import { RotateCcw, ArrowRight, ArrowLeft } from 'lucide-react';
import s from './layout.module.css';
import { Drawer, Button, LoadingState, ErrorState } from '@/components/ui';
import { useDemoScript } from '@/features/demo/api';
import type { DemoScriptStep } from '@/api/types';

export const FALLBACK_STEPS: DemoScriptStep[] = [
  { step: 1, titleKey: 'demo.script.1.title', descriptionKey: 'demo.script.1.desc', route: '/' },
  { step: 2, titleKey: 'demo.script.2.title', descriptionKey: 'demo.script.2.desc', route: '/supply/orders/PO-2026-0007' },
  { step: 3, titleKey: 'demo.script.3.title', descriptionKey: 'demo.script.3.desc', route: '/' },
  { step: 4, titleKey: 'demo.script.4.title', descriptionKey: 'demo.script.4.desc', route: '/planning' },
  { step: 5, titleKey: 'demo.script.5.title', descriptionKey: 'demo.script.5.desc', route: '/planning' },
  { step: 6, titleKey: 'demo.script.6.title', descriptionKey: 'demo.script.6.desc', route: '/trace?q=PMV-2026-0007' },
  { step: 7, titleKey: 'demo.script.7.title', descriptionKey: 'demo.script.7.desc', route: '/passports/PMV-2026-0007' },
  { step: 8, titleKey: 'demo.script.8.title', descriptionKey: 'demo.script.8.desc', route: '/' },
  { step: 9, titleKey: 'demo.script.9.title', descriptionKey: 'demo.script.9.desc', route: '/trace?q=HTS-22-2608' },
];

export function PresenterPanel({ open, onClose, onReset }: { open: boolean; onClose: () => void; onReset: () => void }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const script = useDemoScript(open);
  const [current, setCurrent] = useState(0);
  const steps = script.data && script.data.length > 0 ? script.data : FALLBACK_STEPS;
  const total = steps.length;
  const go = (idx: number) => {
    const i = Math.max(0, Math.min(total - 1, idx));
    setCurrent(i);
    const st = steps[i];
    if (st) navigate(st.route);
  };
  return (
    <Drawer
      open={open}
      onClose={onClose}
      title={t('demo.presenter')}
      actions={
        <Button size="sm" variant="ghost" icon={<RotateCcw size={13} />} onClick={onReset} data-testid="presenter-reset">
          {t('topbar.resetDemo')}
        </Button>
      }
    >
      <div className={s.presenterBody} data-testid="presenter-panel">
        <p className="muted" style={{ fontSize: 'var(--fs-xs)' }}>
          {t('demo.presenterHint')}
        </p>
        {script.isLoading && <LoadingState rows={3} />}
        {script.isError && !script.data && <ErrorState error={script.error} onRetry={() => script.refetch()} />}
        <div className="row" style={{ justifyContent: 'space-between' }}>
          <span className="muted">{t('demo.step', { n: current + 1, total })}</span>
          <span className="row">
            <Button size="sm" icon={<ArrowLeft size={13} />} onClick={() => go(current - 1)} disabled={current === 0} data-testid="presenter-prev">
              {t('common.previous')}
            </Button>
            <Button size="sm" variant="primary" onClick={() => go(current + 1)} disabled={current >= total - 1} data-testid="presenter-next">
              {t('common.next')} <ArrowRight size={13} />
            </Button>
          </span>
        </div>
        {steps.map((st, i) => (
          <button key={st.step} type="button" className={s.step} aria-current={i === current ? 'step' : undefined} onClick={() => go(i)} data-testid={`presenter-step-${st.step}`}>
            <span className={s.stepNo}>{st.step}</span>
            <span>
              <div className={s.stepTitle}>{t(st.titleKey, { defaultValue: st.titleKey })}</div>
              <div className={s.stepDesc}>{t(st.descriptionKey, { defaultValue: st.descriptionKey })}</div>
              <div className="muted" style={{ fontSize: 11, marginTop: 4 }}>
                {t('demo.goToStep')}: <code>{st.route}</code>
              </div>
            </span>
          </button>
        ))}
      </div>
    </Drawer>
  );
}
