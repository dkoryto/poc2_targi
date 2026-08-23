import { useState } from 'react';
import { Outlet, useNavigate } from 'react-router';
import s from './layout.module.css';
import { TopBar } from './TopBar';
import { Nav } from './Nav';
import { PresenterPanel } from './PresenterPanel';
import { DisclaimerBanner, ConfirmDialog, useToast } from '@/components/ui';
import { useLive } from '@/realtime/useLive';
import { useAuth } from '@/features/auth/auth';
import { useResetDemo } from '@/features/demo/api';
import { useTranslation } from 'react-i18next';

export function AppShell() {
  const { user } = useAuth();
  const live = useLive(!!user);
  const [presenter, setPresenter] = useState(false);
  const [resetOpen, setResetOpen] = useState(false);
  const reset = useResetDemo();
  const toast = useToast();
  const { t } = useTranslation();
  const navigate = useNavigate();
  return (
    <div className={s.shell}>
      <TopBar live={live} onOpenPresenter={() => setPresenter(true)} />
      <Nav />
      <main className={s.main} id="main">
        <Outlet />
      </main>
      <div className={s.footer}>
        <DisclaimerBanner />
      </div>
      <PresenterPanel open={presenter} onClose={() => setPresenter(false)} onReset={() => setResetOpen(true)} />
      <ConfirmDialog
        open={resetOpen}
        onClose={() => setResetOpen(false)}
        title={t('demo.resetTitle')}
        impact={t('demo.resetImpact')}
        danger
        confirmLabel={t('topbar.resetDemo')}
        loading={reset.isPending}
        onConfirm={() =>
          reset.mutate(undefined, {
            onSuccess: (r) => {
              setResetOpen(false);
              toast.ok(t('demo.resetDone', { ms: r.durationMs }));
              navigate('/');
            },
            onError: () => toast.critical(t('common.error')),
          })
        }
      />
    </div>
  );
}
