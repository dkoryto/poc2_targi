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
  siteCode?: string;
  /** Plants this user may act on; SupplierUser sees only the ones it supplies. */
  availableSites?: string[];
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

// Sites (multi-site — see docs/architecture/multi-site.md)
export interface Site {
  code: string;
  name: string;
  city: string;
  country: string;
  lat: number;
  lon: number;
  timeZone: string;
  /** i18n key suffix describing the plant profile, e.g. `ASSEMBLY_INTEGRATION`. */
  profileKey?: string | null;
  /** Preset key of this plant's headline scenario; that tile is highlighted on /planning. */
  featuredScenarioKey?: string | null;
  isDefault?: boolean;
  /** Optional cheap status hint for the switcher; absent unless the API supplies it. */
  highRiskDeliveries?: number | null;
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
  /** Plant the step is told against; the presenter panel offers a switch when it differs. */
  siteCode?: string | null;
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

// ---------------------------------------------------------------------------
// Wave 2: planning / traceability / passports / audit / admin
// ---------------------------------------------------------------------------

export type ScenarioChange =
  | { type: 'DELAY_INBOUND'; poLineId: string; days: number; poCode?: string; partCode?: string }
  | { type: 'BLOCK_LOT'; lotNumber: string }
  | { type: 'PRIORITY'; orderCode: string; priority: number }
  | { type: 'CAPACITY'; workCenterCode: string; factor: number }
  | { type: 'DELAY_ORDER'; orderCode: string; days: number };
export type ScenarioChangeType = ScenarioChange['type'];
export const SCENARIO_CHANGE_TYPES: ScenarioChangeType[] = ['DELAY_INBOUND', 'BLOCK_LOT', 'PRIORITY', 'CAPACITY', 'DELAY_ORDER'];

export type ScenarioStatus = 'Draft' | 'Running' | 'Completed' | 'Failed' | 'Approved' | 'Rejected' | 'Saved';

export interface ScenarioPreset {
  key: string;
  titleKey: string;
  changes: ScenarioChange[];
  /** Exactly one preset per plant is the headline scenario. */
  featured?: boolean;
  siteCode?: string;
}
export interface Explanation {
  reasonCode: string;
  orderCode: string;
  params: Record<string, unknown>;
}
export interface Consequence {
  kind: 'info' | 'warn' | 'critical';
  textKey?: string | null;
  text?: string | null;
  params?: Record<string, unknown>;
}
export interface PlanningScenario {
  id: string;
  name: string;
  status: ScenarioStatus;
  createdAt: string;
  createdBy: string;
  changes: ScenarioChange[];
  solver?: string | null;
  elapsedMs?: number | null;
  before?: GanttData | null;
  after?: GanttData | null;
  kpiBefore?: PlanKpi | null;
  kpiAfter?: PlanKpi | null;
  explanations?: Explanation[];
  consequences?: Consequence[];
  approvedAt?: string | null;
  approvedBy?: string | null;
  baselineVersion?: number | null;
  errorMessage?: string | null;
  /** Operations differing from the approved baseline. `kpiAfter.movedOperations` counts what re-planning moved vs "before". */
  changesVsBaseline?: number | null;
}
export interface PlanningScenarioSummary {
  id: string;
  name: string;
  status: ScenarioStatus;
  createdAt: string;
  createdBy: string;
  solver?: string | null;
  changeCount: number;
  kpiAfter?: PlanKpi | null;
}
export interface PlanningBaseline {
  id: string;
  version: number;
  approvedAt: string;
  approvedBy: string;
  gantt: GanttData;
  kpi: PlanKpi;
}
export interface MovedOperation {
  operationCode: string;
  orderCode: string;
  workCenterCode: string;
  before: { start: string; end: string };
  after: { start: string; end: string };
  shiftDays: number;
}
export interface ScenarioCompare {
  movedOperations: MovedOperation[];
  kpiDelta: Partial<PlanKpi>;
}
export interface CreateScenarioRequest {
  name: string;
  changes: ScenarioChange[];
}

// Traceability
export type TraceKind = 'Serial' | 'Lot' | 'Heat' | 'PurchaseOrder' | 'Document' | 'Order' | 'Supplier' | 'Shipment' | 'Operation' | 'Inspection' | 'Passport' | 'Consumption';
export interface TraceSearchHit {
  kind: TraceKind;
  code: string;
  label: string;
  siteCode?: string | null;
}
export interface TraceNode {
  kind: TraceKind | string;
  code: string;
  label: string;
  status?: string | null;
  children: TraceNode[];
  meta?: Record<string, unknown> | null;
}
export interface TraceComponent {
  partCode: string;
  partName?: string | null;
  lotNumber: string;
  heatNumber?: string | null;
  supplierCode: string;
  supplierName?: string | null;
  country?: string | null;
  certSha256?: string | null;
  documentId?: string | null;
}
export interface SerialTrace {
  serial: string;
  productCode: string;
  productName: string;
  orderCode: string;
  bomVersion: string;
  status: string;
  genealogy: TraceNode;
  components?: TraceComponent[];
  passportStatus?: PassportStatus | null;
}
export type LotStatus = 'AwaitingInspection' | 'Accepted' | 'ConditionallyReleased' | 'Blocked' | 'Recalled';
export const LOT_STATUSES: LotStatus[] = ['AwaitingInspection', 'Accepted', 'ConditionallyReleased', 'Blocked', 'Recalled'];
export type InspectionResult = 'Passed' | 'Failed' | 'Conditional';
export interface Inspection {
  id: string;
  result: InspectionResult;
  notes?: string | null;
  inspectedAt: string;
  inspector?: string | null;
}
export interface NonConformance {
  id: string;
  code: string;
  title: string;
  status: string;
  raisedAt: string;
}
export interface LotSummary {
  lotNumber: string;
  heatNumber?: string | null;
  partCode: string;
  partName?: string | null;
  supplierCode: string;
  supplierName?: string | null;
  quantity: number;
  unit: string;
  status: LotStatus;
  receivedOn: string;
}
export interface Lot extends LotSummary {
  poLineId?: string | null;
  poCode?: string | null;
  producedOn?: string | null;
  expiresOn?: string | null;
  documents: DocumentSummary[];
  inspections: Inspection[];
  consumedBy: { orderCode: string; serials: string[] }[];
  reservedBy: string[];
  nonConformances?: NonConformance[];
  rowVersion?: string;
}
export interface LotForward {
  lot: LotSummary;
  orders: { orderCode: string; status: string; relation: 'Consumed' | 'Reserved' }[];
  serials: { serial: string; orderCode: string; productCode: string }[];
  passports: { serial: string; status: PassportStatus }[];
}
export interface BlockLotRequest {
  reason: string;
  ncrTitle: string;
}
export interface BlockLotResponse {
  lot: Lot;
  affected: { orders: string[]; serials: string[]; passports: string[] };
}
export interface InspectionRequest {
  result: InspectionResult;
  notes?: string;
  inspectedAt: string;
}
export interface AuditEvent {
  id: string;
  occurredAt: string;
  user: string;
  action: string;
  entity: string;
  entityCode: string;
  before?: unknown;
  after?: unknown;
  correlationId: string;
  source: string;
}

// Passports
export type PassportStatus = 'Draft' | 'PendingReview' | 'Approved' | 'Generated' | 'Invalidated';
export const PASSPORT_STATUSES: PassportStatus[] = ['Draft', 'PendingReview', 'Approved', 'Generated', 'Invalidated'];
export interface PassportRequirement {
  code: string;
  satisfied: boolean;
  evidence?: string | null;
}
export interface PassportSummary {
  serial: string;
  productCode: string;
  productName?: string | null;
  orderCode: string;
  status: PassportStatus;
  templateCode: string;
  complete: boolean;
  missingCount?: number;
  updatedAt?: string | null;
  latestVersion?: number | null;
}
export interface PassportComponent {
  partCode: string;
  partName?: string | null;
  lotNumber: string;
  supplierCode: string;
  supplierName?: string | null;
  country?: string | null;
  certSha256?: string | null;
}
export interface PassportDeviation {
  id: string;
  code?: string | null;
  title: string;
  status: string;
  approvedBy?: string | null;
  approvedAt?: string | null;
}
export interface PassportVersion {
  version: number;
  generatedAt: string;
  generatedBy: string;
  sha256: string;
  fileSize: number;
  status: 'Current' | 'Superseded' | 'Invalidated';
}
export interface Passport {
  serial: string;
  productCode: string;
  productName?: string | null;
  orderCode: string;
  bomVersion?: string | null;
  status: PassportStatus;
  templateCode: string;
  completeness: { complete: boolean; missing: MissingItem[]; requirements: PassportRequirement[] };
  components: PassportComponent[];
  inspections: Inspection[];
  deviations: PassportDeviation[];
  versions: PassportVersion[];
  approvedBy?: string | null;
  approvedAt?: string | null;
  invalidatedAt?: string | null;
  invalidationReason?: string | null;
}
export interface GeneratePassportResponse {
  version: number;
  sha256: string;
  downloadUrl: string;
}

// Admin
export interface AdminSettings {
  riskWeights: { code: string; weight: number }[];
  objectiveWeights: { code: string; value: number }[];
  thresholds: { code: string; value: number; unit?: string | null }[];
}
