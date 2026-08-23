import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ChevronRight, ChevronDown, Box, Factory, Package, Truck, FileText, Building2, ClipboardCheck, Cog, FileBadge, Hash, Layers } from 'lucide-react';
import s from './trace.module.css';
import type { TraceNode } from '@/api/types';
import { StatusChip, type Tone } from '@/components/ui';

const ICONS: Record<string, typeof Box> = {
  Serial: Box,
  Order: Factory,
  Operation: Cog,
  Consumption: Layers,
  Lot: Package,
  Heat: Hash,
  PurchaseOrder: FileText,
  Shipment: Truck,
  Supplier: Building2,
  Document: FileText,
  Inspection: ClipboardCheck,
  Passport: FileBadge,
};

const STATUS_TONE: Record<string, Tone> = {
  Accepted: 'ok',
  Passed: 'ok',
  Completed: 'ok',
  Generated: 'ok',
  Approved: 'ok',
  Delivered: 'ok',
  ConditionallyReleased: 'warn',
  Conditional: 'warn',
  RequiresCompletion: 'warn',
  Pending: 'neutral',
  PendingReview: 'info',
  Draft: 'neutral',
  Blocked: 'critical',
  Recalled: 'critical',
  Failed: 'critical',
  Rejected: 'critical',
  Invalidated: 'critical',
};

export function nodeStatusTone(status?: string | null): Tone {
  return (status && STATUS_TONE[status]) || 'neutral';
}
export function nodeStatusLabel(t: (k: string, o?: Record<string, unknown>) => string, kind: string, status: string): string {
  const ns = kind === 'Lot' || kind === 'Heat' ? 'lot' : kind === 'Passport' ? 'passport' : kind === 'Document' ? 'doc' : kind === 'Order' ? 'order' : kind === 'Inspection' ? 'inspection' : kind === 'Shipment' ? 'shipment' : kind === 'PurchaseOrder' ? 'po' : 'generic';
  return t(`status.${ns}.${status}`, { defaultValue: status });
}

function Node({ node, depth, selected, onSelect, defaultOpen }: { node: TraceNode; depth: number; selected: string | null; onSelect: (n: TraceNode) => void; defaultOpen: number }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(depth < defaultOpen);
  const Icon = ICONS[node.kind] ?? Box;
  const hasChildren = node.children && node.children.length > 0;
  const key = `${node.kind}:${node.code}`;
  return (
    <li>
      <div className="row" style={{ gap: 4, flexWrap: 'nowrap' }}>
        {hasChildren ? (
          <button type="button" className={s.toggle} aria-expanded={open} aria-label={open ? t('trace.collapse') : t('trace.expand')} onClick={() => setOpen((o) => !o)} data-testid={`trace-toggle-${node.code}`}>
            {open ? <ChevronDown size={12} /> : <ChevronRight size={12} />}
          </button>
        ) : (
          <span className={s.toggleSpacer} />
        )}
        <button type="button" className={[s.node, selected === key && s.nodeSelected].filter(Boolean).join(' ')} onClick={() => onSelect(node)} aria-pressed={selected === key} data-testid={`trace-node-${node.code}`}>
          <Icon size={14} aria-hidden style={{ flexShrink: 0, color: 'var(--fg-2)' }} />
          <span className={s.nodeKind}>{t(`trace.kind.${node.kind}`, { defaultValue: node.kind })}</span>
          <span className={s.nodeCode}>{node.code}</span>
          <span className={s.nodeLabel}>{node.label}</span>
          {node.status && <StatusChip tone={nodeStatusTone(node.status)} label={nodeStatusLabel(t, node.kind, node.status)} small />}
        </button>
      </div>
      {hasChildren && open && (
        <ul>
          {node.children.map((c, i) => (
            <Node key={`${c.kind}:${c.code}:${i}`} node={c} depth={depth + 1} selected={selected} onSelect={onSelect} defaultOpen={defaultOpen} />
          ))}
        </ul>
      )}
    </li>
  );
}

export function GenealogyTree({ root, selected, onSelect, defaultOpen = 5 }: { root: TraceNode; selected: TraceNode | null; onSelect: (n: TraceNode) => void; defaultOpen?: number }) {
  const { t } = useTranslation();
  return (
    <ul className={s.tree} role="tree" aria-label={t('trace.genealogy')} data-testid="genealogy-tree">
      <Node node={root} depth={0} selected={selected ? `${selected.kind}:${selected.code}` : null} onSelect={onSelect} defaultOpen={defaultOpen} />
    </ul>
  );
}
