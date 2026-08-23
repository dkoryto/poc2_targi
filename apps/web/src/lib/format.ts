import { formatInTimeZone } from 'date-fns-tz';
import { pl as plLocale, enGB } from 'date-fns/locale';
import { differenceInCalendarDays, parseISO } from 'date-fns';
import { currentLocale } from '@/i18n';

export const SITE_TZ = 'Europe/Warsaw';

function dfLocale() {
  return currentLocale() === 'en' ? enGB : plLocale;
}
function intlLocale() {
  return currentLocale() === 'en' ? 'en-GB' : 'pl-PL';
}

export function toDate(value: string | Date): Date {
  return typeof value === 'string' ? parseISO(value) : value;
}

export function fmtDate(value?: string | Date | null): string {
  if (!value) return '—';
  return formatInTimeZone(toDate(value), SITE_TZ, 'dd.MM.yyyy', { locale: dfLocale() });
}
export function fmtDateTime(value?: string | Date | null): string {
  if (!value) return '—';
  return formatInTimeZone(toDate(value), SITE_TZ, 'dd.MM.yyyy HH:mm', { locale: dfLocale() });
}
export function fmtTime(value: Date): string {
  return formatInTimeZone(value, SITE_TZ, 'HH:mm:ss');
}
export function fmtClock(value: Date): string {
  return formatInTimeZone(value, SITE_TZ, 'EEE dd.MM.yyyy HH:mm:ss', { locale: dfLocale() });
}
export function fmtNumber(value: number, digits = 0): string {
  return new Intl.NumberFormat(intlLocale(), {
    maximumFractionDigits: digits,
    minimumFractionDigits: digits,
  }).format(value);
}
export function fmtPercent(value: number, digits = 0): string {
  return `${fmtNumber(value, digits)} %`;
}
export function fmtHours(value: number): string {
  return `${fmtNumber(value, value % 1 === 0 ? 0 : 1)} h`;
}
export function fmtBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${fmtNumber(bytes / 1024, 1)} KB`;
  return `${fmtNumber(bytes / 1024 / 1024, 1)} MB`;
}
export function daysBetween(a: string | Date, b: string | Date): number {
  return differenceInCalendarDays(toDate(a), toDate(b));
}
export function fmtSigned(value: number, digits = 0): string {
  const s = fmtNumber(Math.abs(value), digits);
  if (value > 0) return `+${s}`;
  if (value < 0) return `−${s}`;
  return s;
}
export function dateInputValue(value?: string | Date | null): string {
  if (!value) return '';
  return formatInTimeZone(toDate(value), SITE_TZ, 'yyyy-MM-dd');
}
