import { useTranslation } from 'react-i18next';

function flatten(v: unknown, prefix = '', out: Record<string, string> = {}): Record<string, string> {
  if (v === null || v === undefined) return out;
  if (typeof v !== 'object') {
    out[prefix || '$'] = String(v);
    return out;
  }
  if (Array.isArray(v)) {
    if (v.length === 0) out[prefix] = '[]';
    v.forEach((x, i) => flatten(x, `${prefix}[${i}]`, out));
    return out;
  }
  for (const [k, x] of Object.entries(v as Record<string, unknown>)) flatten(x, prefix ? `${prefix}.${k}` : k, out);
  return out;
}

export function diffKeys(before: unknown, after: unknown): { key: string; before?: string; after?: string; changed: boolean }[] {
  const b = flatten(before);
  const a = flatten(after);
  const keys = [...new Set([...Object.keys(b), ...Object.keys(a)])].sort();
  return keys.map((k) => ({ key: k, before: b[k], after: a[k], changed: b[k] !== a[k] }));
}

export function JsonDiff({ before, after }: { before: unknown; after: unknown }) {
  const { t } = useTranslation();
  const rows = diffKeys(before, after);
  if (rows.length === 0) return <span className="muted">{t('audit.noPayload')}</span>;
  return (
    <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12, fontFamily: 'var(--font-mono)' }} data-testid="json-diff">
      <thead>
        <tr style={{ color: 'var(--fg-3)', textAlign: 'left' }}>
          <th style={{ padding: '2px 6px' }}>{t('audit.field')}</th>
          <th style={{ padding: '2px 6px' }}>{t('risk.before')}</th>
          <th style={{ padding: '2px 6px' }}>{t('risk.after')}</th>
        </tr>
      </thead>
      <tbody>
        {rows.map((r) => (
          <tr key={r.key} style={{ background: r.changed ? 'var(--warn-bg)' : undefined }} data-changed={r.changed || undefined}>
            <td style={{ padding: '2px 6px', color: 'var(--fg-2)' }}>{r.key}</td>
            <td style={{ padding: '2px 6px', color: r.changed ? 'var(--crit)' : undefined, textDecoration: r.changed && r.before !== undefined ? 'line-through' : undefined }}>{r.before ?? '—'}</td>
            <td style={{ padding: '2px 6px', color: r.changed ? 'var(--ok)' : undefined, fontWeight: r.changed ? 600 : undefined }}>{r.after ?? '—'}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
