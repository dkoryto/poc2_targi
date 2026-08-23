import { useEffect, useRef, useState } from 'react';
import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from '@microsoft/signalr';
import { useQueryClient, type QueryKey } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { getToken } from '@/api/client';
import { keys } from '@/api/keys';
import type { DomainEvent } from '@/api/types';
import { useToast } from '@/components/ui';

export type LiveStatus = 'connected' | 'connecting' | 'disconnected';

const INVALIDATIONS: Record<string, QueryKey[]> = {
  ShipmentEtaChanged: [keys.dashboard.all, keys.purchaseOrders.all, keys.shipments.all],
  SupplierOrderStatusChanged: [keys.purchaseOrders.all, keys.shipments.all, keys.dashboard.all],
  DeliveryRiskChanged: [keys.dashboard.all, keys.purchaseOrders.all, keys.shipments.all, keys.suppliers],
  LogisticsRiskEventRaised: [keys.logisticsEvents, keys.dashboard.all, keys.shipments.all, keys.purchaseOrders.all],
  QualityDocumentUploaded: [keys.purchaseOrders.all, ['dashboard', 'quality'] as const, keys.lots],
  PlanningScenarioCompleted: [keys.planning.all],
  ProductionPlanApproved: [keys.planning.all, keys.dashboard.all],
  MaterialLotBlocked: [keys.lots, keys.passports, keys.dashboard.all, keys.planning.all, keys.quality],
  PassportInvalidated: [keys.passports, ['dashboard', 'quality'] as const],
  PassportGenerated: [keys.passports, ['dashboard', 'quality'] as const],
};

type Listener = (e: DomainEvent) => void;
const listeners = new Set<Listener>();
export function onDomainEvent(fn: Listener): () => void {
  listeners.add(fn);
  return () => listeners.delete(fn);
}

export function useLive(enabled: boolean): LiveStatus {
  const qc = useQueryClient();
  const toast = useToast();
  const { t } = useTranslation();
  const [status, setStatus] = useState<LiveStatus>('disconnected');
  const connRef = useRef<HubConnection | null>(null);

  useEffect(() => {
    if (!enabled || import.meta.env.VITE_USE_MOCKS === 'true') {
      setStatus('disconnected');
      return;
    }
    const conn = new HubConnectionBuilder()
      .withUrl('/hubs/live', { accessTokenFactory: () => getToken() ?? '' })
      .withAutomaticReconnect([0, 1000, 3000, 5000, 10000])
      .configureLogging(LogLevel.Warning)
      .build();
    connRef.current = conn;
    conn.onreconnecting(() => setStatus('connecting'));
    conn.onreconnected(() => {
      setStatus('connected');
      void qc.invalidateQueries();
    });
    conn.onclose(() => setStatus('disconnected'));
    conn.on('DomainEvent', (e: DomainEvent) => {
      for (const k of INVALIDATIONS[e.name] ?? []) void qc.invalidateQueries({ queryKey: k });
      void qc.invalidateQueries({ queryKey: keys.notifications });
      listeners.forEach((l) => l(e));
      const p = e.payload ?? {};
      const code = String(p.code ?? p.poLineCode ?? p.shipmentCode ?? p.poCode ?? '');
      switch (e.name) {
        case 'DeliveryRiskChanged': {
          const cat = String(p.category ?? p.newCategory ?? '');
          const msg = t('events.DeliveryRiskChanged', { code, category: t(`risk.${cat}`, { defaultValue: cat }) });
          if (cat === 'Critical' || cat === 'High') toast.critical(msg);
          else toast.warn(msg);
          break;
        }
        case 'MaterialLotBlocked':
          toast.critical(t('events.MaterialLotBlocked', { lotNumber: String(p.lotNumber ?? '') }));
          break;
        case 'PlanningScenarioCompleted':
          toast.info(t('events.PlanningScenarioCompleted'));
          break;
        case 'ProductionPlanApproved':
          toast.ok(t('events.ProductionPlanApproved'));
          break;
        case 'PassportInvalidated':
          toast.warn(t('events.PassportInvalidated', { serial: String(p.serial ?? '') }));
          break;
        case 'PassportGenerated':
          toast.ok(t('events.PassportGenerated', { serial: String(p.serial ?? '') }));
          break;
        default:
          break;
      }
    });
    setStatus('connecting');
    conn
      .start()
      .then(() => setStatus('connected'))
      .catch(() => setStatus('disconnected'));
    return () => {
      if (conn.state !== HubConnectionState.Disconnected) void conn.stop();
      connRef.current = null;
    };
  }, [enabled, qc, toast, t]);

  return status;
}
