import type { ReactNode } from 'react';
import { Maximize2, Minimize2, Info } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import s from './ui.module.css';
import { IconButton } from './Button';
import { Tooltip } from './Tooltip';

export interface CardProps {
  title?: ReactNode;
  definition?: string;
  actions?: ReactNode;
  children: ReactNode;
  flush?: boolean;
  className?: string;
  focusable?: boolean;
  focused?: boolean;
  onToggleFocus?: () => void;
  'data-testid'?: string;
  style?: React.CSSProperties;
}

export function Card({
  title,
  definition,
  actions,
  children,
  flush,
  className,
  focusable,
  focused,
  onToggleFocus,
  style,
  ...rest
}: CardProps) {
  const { t } = useTranslation();
  return (
    <section className={[s.card, className].filter(Boolean).join(' ')} style={style} data-testid={rest['data-testid']}>
      {title !== undefined && (
        <header className={s.cardHeader}>
          <h2 className={s.cardTitle}>
            {title}
            {definition && (
              <Tooltip content={definition}>
                <Info size={14} aria-label={t('common.definition')} />
              </Tooltip>
            )}
          </h2>
          <div className={s.cardActions}>
            {actions}
            {focusable && (
              <IconButton label={focused ? t('common.exitFocus') : t('common.focus')} onClick={onToggleFocus}>
                {focused ? <Minimize2 size={16} /> : <Maximize2 size={16} />}
              </IconButton>
            )}
          </div>
        </header>
      )}
      <div className={[s.cardBody, flush && s.cardBodyFlush].filter(Boolean).join(' ')}>{children}</div>
    </section>
  );
}
