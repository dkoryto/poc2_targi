import { Component, type ErrorInfo, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { AlertOctagon, RotateCcw } from 'lucide-react';
import s from './ui.module.css';
import { Button } from './Button';
import { ApiError } from '@/api/client';

function ErrorCard({ error, onRetry }: { error: Error; onRetry: () => void }) {
  const { t } = useTranslation();
  const correlationId = error instanceof ApiError ? error.problem?.traceId : undefined;
  return (
    <div className="page" data-testid="error-boundary" role="alert">
      <div className={s.boundary}>
        <span className={s.boundaryIcon}>
          <AlertOctagon size={22} aria-hidden />
        </span>
        <h2>{t('error.boundaryTitle')}</h2>
        <p className="muted">{t('error.boundaryBody')}</p>
        <p className={s.boundaryDetail}>{error.message}</p>
        {correlationId && (
          <p className="muted" style={{ fontSize: 'var(--fs-xs)' }}>
            {t('error.correlationId')}: <span className="mono">{correlationId}</span>
          </p>
        )}
        <div className="row">
          <Button variant="primary" icon={<RotateCcw size={14} />} onClick={onRetry} data-testid="error-retry">
            {t('common.retry')}
          </Button>
          <Button variant="ghost" onClick={() => window.location.reload()} data-testid="error-reload">
            {t('error.reload')}
          </Button>
        </div>
      </div>
    </div>
  );
}

interface Props {
  children: ReactNode;
  /** Changing this value clears the error (e.g. the current route path). */
  resetKey?: string;
}
interface State {
  error: Error | null;
}

/**
 * Catches render/lifecycle errors below it so a single broken component
 * degrades to a localized card instead of a blank page.
 */
export class ErrorBoundary extends Component<Props, State> {
  state: State = { error: null };

  static getDerivedStateFromError(error: Error): State {
    return { error };
  }

  componentDidCatch(error: Error, info: ErrorInfo): void {
    console.error('[ErrorBoundary]', error, info.componentStack);
  }

  componentDidUpdate(prev: Props): void {
    if (this.state.error && prev.resetKey !== this.props.resetKey) this.setState({ error: null });
  }

  render(): ReactNode {
    if (this.state.error) return <ErrorCard error={this.state.error} onRetry={() => this.setState({ error: null })} />;
    return this.props.children;
  }
}
