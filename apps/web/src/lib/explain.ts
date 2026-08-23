import type { TFunction } from 'i18next';
import { fmtDate, fmtNumber } from './format';

const DATE_KEYS = new Set(['availableOn', 'dueDate', 'date', 'start', 'end', 'eta', 'requiredOn']);

/** Formats solver explanation params for i18n interpolation (dates, percentages, lists). */
export function formatExplainParams(params: Record<string, unknown>): Record<string, string | number> {
  const out: Record<string, string | number> = {};
  for (const [k, v] of Object.entries(params)) {
    if (v == null) {
      out[k] = '—';
    } else if (Array.isArray(v)) {
      out[k] = v.map(String).join(', ');
    } else if (typeof v === 'string' && DATE_KEYS.has(k) && /^\d{4}-\d{2}-\d{2}/.test(v)) {
      out[k] = fmtDate(v);
    } else if (typeof v === 'number') {
      if (k === 'materialCompleteness' || k === 'factor') out[k] = `${fmtNumber(v * 100, 0)} %`;
      else out[k] = fmtNumber(v, Number.isInteger(v) ? 0 : 1);
    } else {
      out[k] = String(v);
    }
  }
  return out;
}

export function explainText(t: TFunction, reasonCode: string, params: Record<string, unknown>): string {
  return t(`explain.${reasonCode}`, { ...formatExplainParams(params), defaultValue: reasonCode });
}
