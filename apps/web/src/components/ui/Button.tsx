import { forwardRef, type ButtonHTMLAttributes, type ReactNode } from 'react';
import s from './ui.module.css';

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: 'default' | 'primary' | 'danger' | 'ghost';
  size?: 'sm' | 'md' | 'lg';
  icon?: ReactNode;
  loading?: boolean;
}

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(function Button(
  { variant = 'default', size = 'md', icon, loading, children, className, disabled, ...rest },
  ref,
) {
  const cls = [
    s.btn,
    variant === 'primary' && s.btnPrimary,
    variant === 'danger' && s.btnDanger,
    variant === 'ghost' && s.btnGhost,
    size === 'sm' && s.btnSm,
    size === 'lg' && s.btnLg,
    className,
  ]
    .filter(Boolean)
    .join(' ');
  return (
    <button ref={ref} className={cls} disabled={disabled || loading} aria-busy={loading} {...rest}>
      {icon}
      {children}
    </button>
  );
});

export interface IconButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  label: string;
}
export function IconButton({ label, children, className, ...rest }: IconButtonProps) {
  return (
    <button className={[s.iconBtn, className].filter(Boolean).join(' ')} aria-label={label} title={label} {...rest}>
      {children}
    </button>
  );
}
