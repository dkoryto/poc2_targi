import { AlertCircle } from 'lucide-react';
import { forwardRef, useId, type InputHTMLAttributes, type ReactNode, type SelectHTMLAttributes, type TextareaHTMLAttributes } from 'react';
import s from './ui.module.css';

export function FormField({ label, error, hint, children, required, full, id }: { label: ReactNode; error?: string; hint?: string; children: (id: string) => ReactNode; required?: boolean; full?: boolean; id?: string }) {
  const auto = useId();
  const fid = id ?? auto;
  return (
    <div className={[s.field, full && s.full].filter(Boolean).join(' ')}>
      <label htmlFor={fid} className={s.label}>
        {label}
        {required && <span aria-hidden> *</span>}
      </label>
      {children(fid)}
      {error ? <span className={s.fieldError} role="alert">{error}</span> : hint ? <span className={s.fieldHint}>{hint}</span> : null}
    </div>
  );
}

/**
 * A failure that belongs to the form rather than to one input: a required field left empty, or a
 * rejection the API did not attribute to a named field. Rendered inside the dialog or panel so the
 * user sees why nothing happened, instead of a button that appears dead.
 */
export function FormAlert({ message }: { message?: string | null }) {
  if (!message) return null;
  return (
    <div className={s.formAlert} role="alert" data-testid="form-alert">
      <AlertCircle size={14} aria-hidden />
      <span>{message}</span>
    </div>
  );
}

export const Input = forwardRef<HTMLInputElement, InputHTMLAttributes<HTMLInputElement> & { invalid?: boolean }>(function Input({ invalid, className, ...rest }, ref) {
  return <input ref={ref} className={[s.input, invalid && s.inputError, className].filter(Boolean).join(' ')} aria-invalid={invalid || undefined} {...rest} />;
});

export const Select = forwardRef<HTMLSelectElement, SelectHTMLAttributes<HTMLSelectElement> & { invalid?: boolean }>(function Select({ invalid, className, children, ...rest }, ref) {
  return (
    <select ref={ref} className={[s.select, invalid && s.inputError, className].filter(Boolean).join(' ')} aria-invalid={invalid || undefined} {...rest}>
      {children}
    </select>
  );
});

export const Textarea = forwardRef<HTMLTextAreaElement, TextareaHTMLAttributes<HTMLTextAreaElement> & { invalid?: boolean }>(function Textarea({ invalid, className, ...rest }, ref) {
  return <textarea ref={ref} className={[s.textarea, invalid && s.inputError, className].filter(Boolean).join(' ')} aria-invalid={invalid || undefined} {...rest} />;
});

export const DateInput = forwardRef<HTMLInputElement, InputHTMLAttributes<HTMLInputElement> & { invalid?: boolean }>(function DateInput(props, ref) {
  return <Input ref={ref} type="date" {...props} />;
});

export const FileInput = forwardRef<HTMLInputElement, InputHTMLAttributes<HTMLInputElement> & { invalid?: boolean }>(function FileInput({ invalid, className, ...rest }, ref) {
  return <input ref={ref} type="file" className={[s.fileInput, className].filter(Boolean).join(' ')} aria-invalid={invalid || undefined} {...rest} />;
});

export function FormGrid({ children }: { children: ReactNode }) {
  return <div className={s.formGrid}>{children}</div>;
}
