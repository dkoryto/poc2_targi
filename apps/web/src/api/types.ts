// Hand-written types mirroring docs/api/endpoints.md (replace with generated.ts via `pnpm gen:api` once API is up).

export type Role =
  | 'SupplierUser'
  | 'InboundCoordinator'
  | 'ProductionPlanner'
  | 'QualityInspector'
  | 'OperationsDirector'
  | 'Auditor'
  | 'Administrator'
  | 'DemoPresenter';

export const ALL_ROLES: Role[] = [
  'DemoPresenter',
  'OperationsDirector',
  'ProductionPlanner',
  'InboundCoordinator',
  'QualityInspector',
  'SupplierUser',
  'Auditor',
  'Administrator',
];

export interface Paged<T> {
  items: T[];
  total: number;
}

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  errors?: Record<string, string[]>;
  traceId?: string;
  missing?: MissingItem[];
}

export interface MissingItem {
  code: string;
  labelKey?: string;
  params?: Record<string, unknown>;
}

// Identity
export interface UserContext {
  id: string;
  username: string;
  displayName: string;
  role: Role;
  supplierId?: string | null;
  supplierName?: string | null;
  siteId: string;
  locale: string;
  demoMode: boolean;
}
export interface LoginResponse {
  accessToken: string;
  expiresAt: string;
  user: UserContext;
}
export interface DemoAccount {
  username: string;
  role: Role;
  supplierCode?: string | null;
  description: string;
}

// Dashboard
export type KpiCode =
  | 'MATERIAL_READINESS'
  | 'OTIF'
  | 'HIGH_RISK_DELIVERIES'
  | 'PREDICTED_DOWNTIME_H'
  | 'ORDER_ON_TIME'
  | 'PASSPORT_COMPLETENESS';
export type KpiUnit = '%' | 'h' | 'count';
export type KpiStatus = 'ok' | 'warn' | 'critical';
export interface Kpi {
  code: KpiCode;
  value: number;
  unit: KpiUnit;
  trend: number;
  status: KpiStatus;
  definitionKey: string;
}
export interface KpiResponse {
  asOf: string;
  items: Kpi[];
}

export type RiskCategory = 'Low' | 'Medium' | 'High' | 'Critical';

export interface MapSite {
  code: string;
  name: string;
  lat: number;
  lon: number;
}
export interface MapSupplier {
  code: string;
  name: string;
  country: string;
  city: string;
  lat: number;
  lon: number;
  riskScore: number;
  riskCategory: RiskCategory;
}
export type ShipmentStatus =
  | 'Planned'
  | 'InTransit'
  | 'Delayed'
  | 'Delivered'
  | 'OnHold'
  | 'Confirmed'
  | 'InProduction'
  | 'QualityControl'
  | 'ReadyToShip'
  | 'Shipped';
export interface MapShipment {
  code: string;
  poCode: string;
  supplierCode: string;
  partCode: string;
  quantity: number;
  eta: string;
  requiredDate: string;
  status: string;
  riskScore: number;
  riskCategory: RiskCategory;
  progress: number;
  lat: number;
  lon: number;
  route: [number, number][];
}
export interface MapData {
  site: MapSite;
  suppliers: MapSupplier[];
  shipments: MapShipment[];
}

export interface HeatmapCell {
  row: string;
  col: string;
  score: number;
  count: number;
}
export interface RiskHeatmap {
  rows: string[];
  cols: string[];
  cells: HeatmapCell[];
}

export interface QualityStatus {
  passports: {
    draft: number;
    pendingReview: number;
    approved: number;
    generated: number;
    invalidated: number;
  };
  documents: {
    pending: number;
    verifying: number;
    accepted: number;
    rejected: number;
    requiresCompletion: number;
  };
  openNonConformances: number;
  lotsBlocked: number;
  readyForAcceptance: number;
}

// Gantt
export type OrderStatus = 'Planned' | 'Released' | 'InProgress' | 'Completed' | 'OnHold';
export interface GanttWorkCenter {
  code: string;
  name: string;
  lineCode: string;
}
export interface GanttOrder {
  code: string;
  productCode: string;
  productName: string;
  priority: number;
  dueDate: string;
  status: OrderStatus | string;
  materialComplete: boolean;
  riskFlag: 'none' | 'warn' | 'critical';
}
export interface GanttOperation {
  orderCode: string;
  code: string;
  sequence: number;
  workCenterCode: string;
  start: string;
  end: string;
  frozen: boolean;
  status: string;
  materialWait: boolean;
  changed?: boolean;
  shiftDays?: number;
}
export interface GanttConflict {
  operationCode: string;
  reasonCode: string;
  params: Record<string, unknown>;
}
export interface GanttData {
  horizonStart: string;
  horizonEnd: string;
  workCenters: GanttWorkCenter[];
  orders: GanttOrder[];
  operations: GanttOperation[];
  dependencies: { from: string; to: string }[];
  conflicts: GanttConflict[];
}

export interface PlanKpi {
  downtimeHours: number;
  lateOrders: number;
  totalLatenessDays: number;
  movedOperations: number;
  ordersWithShortage: number;
  onTimeRate: number;
}

// Suppliers / inbound
export interface Supplier {
  code: string;
  name: string;
  country: string;
  city: string;
  lat: number;
  lon: number;
  otif: number;
  qualityScore: number;
  riskScore: number;
  openOrders: number;
  activeShipments: number;
}

export type PoLineStatus =
  | 'Confirmed'
  | 'InProduction'
  | 'QualityControl'
  | 'ReadyToShip'
  | 'Shipped'
  | 'Delivered'
  | 'OnHold';
export const PO_LINE_STATUSES: PoLineStatus[] = [
  'Confirmed',
  'InProduction',
  'QualityControl',
  'ReadyToShip',
  'Shipped',
  'Delivered',
  'OnHold',
];

export type RiskFactorCode =
  | 'ETA_DEVIATION'
  | 'CRITICALITY'
  | 'NO_ALTERNATIVE'
  | 'DOC_COMPLETENESS'
  | 'SUPPLIER_RELIABILITY'
  | 'COVERAGE'
  | 'LOGISTICS_EVENTS';
export interface RiskFactor {
  code: RiskFactorCode | string;
  raw: number;
  weight: number;
  contribution: number;
}
export interface EndangeredOrder {
  orderCode: string;
  requiredOn: string;
  shortage: number;
}
export interface RiskSummary {
  score: number;
  category: RiskCategory;
  factors: RiskFactor[];
  endangeredOrders: EndangeredOrder[];
}

export type DocumentType =
  | 'MATERIAL_CERT'
  | 'INSPECTION_REPORT'
  | 'DECLARATION_OF_CONFORMITY'
  | 'TRANSPORT_DOC';
export const DOCUMENT_TYPES: DocumentType[] = [
  'MATERIAL_CERT',
  'INSPECTION_REPORT',
  'DECLARATION_OF_CONFORMITY',
  'TRANSPORT_DOC',
];
export type DocumentStatus = 'Pending' | 'Verifying' | 'Accepted' | 'Rejected' | 'RequiresCompletion';
export interface DocumentSummary {
  id: string;
  type: DocumentType;
  fileName: string;
  sizeBytes: number;
  sha256: string;
  status: DocumentStatus;
  uploadedAt: string;
  uploadedBy: string;
  lotNumber?: string | null;
  documentNumber?: string | null;
  aiSuggestion?: unknown;
}

export interface ChangeEntry {
  id: string;
  occurredAt: string;
  user: string;
  action: string;
  field?: string | null;
  before?: string | null;
  after?: string | null;
  comment?: string | null;
}

export interface PurchaseOrderLine {
  id: string;
  lineNo: number;
  partCode: string;
  partName: string;
  quantity: number;
  unit: string;
  requiredDate: string;
  eta: string;
  progressPercent: number;
  status: PoLineStatus;
  lotNumber?: string | null;
  heatNumber?: string | null;
  producedOn?: string | null;
  expiresOn?: string | null;
  risk: RiskSummary;
  documents: DocumentSummary[];
  shipmentCode?: string | null;
  rowVersion: string;
}
export interface PurchaseOrderSummary {
  code: string;
  supplierCode: string;
  supplierName: string;
  status: string;
  orderedAt: string;
  requiredDate: string;
  eta: string;
  lineCount: number;
  riskScore: number;
  riskCategory: RiskCategory;
  progressPercent: number;
  siteCode: string;
}
export interface PurchaseOrderDetail {
  code: string;
  supplier: Supplier;
  status: string;
  orderedAt: string;
  siteCode?: string;
  lines: PurchaseOrderLine[];
  history: ChangeEntry[];
}
export interface PoLinePatch {
  status?: PoLineStatus;
  progressPercent?: number;
  lotNumber?: string;
  heatNumber?: string;
  producedOn?: string;
  expiresOn?: string;
  quantity?: number;
  eta?: string;
  comment?: string;
}
export type EtaReason =
  | 'PRODUCTION_DELAY'
  | 'LOGISTICS'
  | 'QUALITY'
  | 'CAPACITY'
  | 'MATERIAL_SHORTAGE'
  | 'OTHER';
export const ETA_REASONS: EtaReason[] = [
  'PRODUCTION_DELAY',
  'LOGISTICS',
  'QUALITY',
  'CAPACITY',
  'MATERIAL_SHORTAGE',
  'OTHER',
];
export interface EtaChangeRequest {
  eta: string;
  reason: EtaReason;
  comment?: string;
}
export interface EtaChangeResponse {
  line: PurchaseOrderLine;
  risk: RiskSummary;
  endangeredOrders: EndangeredOrder[];
}
export interface ImpactResponse {
  risk: RiskSummary;
  endangeredOrders: EndangeredOrder[];
  predictedDowntimeHours: number;
}

export interface ShipmentEvent {
  id: string;
  type: string;
  occurredAt: string;
  note?: string | null;
  user?: string | null;
}
export interface Shipment {
  code: string;
  poCode: string;
  supplierCode: string;
  supplierName: string;
  carrier?: string | null;
  vehicle?: string | null;
  plannedDeparture?: string | null;
  eta: string;
  requiredDate?: string | null;
  status: string;
  riskScore: number;
  riskCategory: RiskCategory;
  progress: number;
  lines: { lineId: string; partCode: string; quantity: number }[];
  events: ShipmentEvent[];
}
export interface ShipmentAdviceRequest {
  poCode: string;
  lineIds: string[];
  carrier: string;
  vehicle: string;
  plannedDeparture: string;
  eta: string;
}

export type LogisticsEventType =
  | 'BORDER_DELAY'
  | 'PORT_DISRUPTION'
  | 'WEATHER'
  | 'QUALITY_ISSUE'
  | 'PARTIAL_DELIVERY'
  | 'NO_CONFIRMATION';
export const LOGISTICS_EVENT_TYPES: LogisticsEventType[] = [
  'BORDER_DELAY',
  'PORT_DISRUPTION',
  'WEATHER',
  'QUALITY_ISSUE',
  'PARTIAL_DELIVERY',
  'NO_CONFIRMATION',
];
export type Severity = 'LOW' | 'MEDIUM' | 'HIGH';
export interface LogisticsEvent {
  id: string;
  type: LogisticsEventType;
  severity: Severity;
  supplierCode?: string | null;
  shipmentCode?: string | null;
  description: string;
  raisedAt: string;
  active: boolean;
}
export interface LogisticsEventRequest {
  type: LogisticsEventType;
  severity: Severity;
  supplierCode?: string;
  shipmentCode?: string;
  description: string;
}

// Notifications / demo / admin
export interface Notification {
  id: string;
  createdAt: string;
  title: string;
  message: string;
  severity: 'info' | 'warn' | 'critical';
  read: boolean;
  route?: string | null;
  eventName?: string | null;
}

export interface DemoStatus {
  demoMode: boolean;
  seedVersion: string;
  seededAt: string;
  lastResetMs?: number | null;
}
export interface DemoResetResult {
  durationMs: number;
  seedVersion: string;
  counts: Record<string, number>;
}
export interface DemoScriptStep {
  step: number;
  titleKey: string;
  descriptionKey: string;
  route: string;
  action?: string | null;
}

export interface ServiceStatus {
  name: string;
  status: 'up' | 'down' | 'disabled';
  latencyMs?: number | null;
}
export interface AdminStatus {
  services: ServiceStatus[];
  recentErrors: { at: string; operation: string; message: string }[];
}

export interface DomainEvent {
  name: string;
  occurredAt: string;
  correlationId: string;
  payload: Record<string, unknown>;
}
