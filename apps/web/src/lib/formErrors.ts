import { useCallback, useState } from 'react';
import { ApiError } from '@/api/client';

/**
 * Shared error state for hand-built forms and dialogs.
 *
 * Two failures used to be invisible to the user: a required field left empty (the dialog simply
 * did nothing) and a 400 returned by the API (swallowed into a generic toast). Both now land on
 * the field they belong to, falling back to a form-level message when the server does not name
 * one. Forms driven by react-hook-form keep their own resolver for the client-side half and use
 * `fromApi` only for the server's answer.
 */
export interface FormErrors {
  /** Field name (camelCase, matching the API's Problem Details keys) → message. */
  fields: Record<string, string>;
  /** Message that belongs to the form as a whole rather than one field. */
  formError: string | null;
  clear: () => void;
  /**
   * Client-side required check. Pass the field values keyed by name; entries that are empty get
   * the supplied message. Returns true when everything is filled in.
   */
  requireFields: (values: Record<string, string | null | undefined>, message: string) => boolean;
  /** Map an ApiError's Problem Details onto the form; anything unattributable becomes formError. */
  fromApi: (error: unknown, fallback: string) => void;
  setFormError: (message: string | null) => void;
}

export function useFormErrors(): FormErrors {
  const [fields, setFields] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<string | null>(null);

  const clear = useCallback(() => {
    setFields({});
    setFormError(null);
  }, []);

  const requireFields = useCallback((values: Record<string, string | null | undefined>, message: string) => {
    const missing: Record<string, string> = {};
    for (const [name, value] of Object.entries(values)) {
      if (!value || !String(value).trim()) missing[name] = message;
    }
    setFields(missing);
    setFormError(Object.keys(missing).length > 0 ? message : null);
    return Object.keys(missing).length === 0;
  }, []);

  const fromApi = useCallback((error: unknown, fallback: string) => {
    const problem = error instanceof ApiError ? error.problem : null;
    const entries = Object.entries(problem?.errors ?? {});
    if (entries.length > 0) {
      const next: Record<string, string> = {};
      const unattributed: string[] = [];
      for (const [key, messages] of entries) {
        const text = (messages ?? []).join(' ');
        // FluentValidation object-level rules report an empty property name; those belong to the
        // form, not to any single input.
        if (!key || key === '_' || key === '$') unattributed.push(text);
        else next[key] = text;
      }
      setFields(next);
      setFormError(unattributed.length > 0 ? unattributed.join(' ') : Object.keys(next).length === 0 ? fallback : null);
      return;
    }
    setFields({});
    setFormError(problem?.detail ?? problem?.title ?? (error instanceof Error ? error.message : null) ?? fallback);
  }, []);

  return { fields, formError, clear, requireFields, fromApi, setFormError };
}
