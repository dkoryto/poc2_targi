namespace Dspc.Domain.Common;

public enum Role
{
    SupplierUser,
    InboundCoordinator,
    ProductionPlanner,
    QualityInspector,
    OperationsDirector,
    Auditor,
    Administrator,
    DemoPresenter
}

public enum PurchaseOrderStatus { Open, PartiallyDelivered, Delivered, Closed, Cancelled }

public enum PurchaseOrderLineStatus { Confirmed, InProduction, QualityControl, ReadyToShip, Shipped, Delivered, OnHold }

public enum ShipmentStatus { Advised, Departed, InTransit, AtBorder, Arrived, Received, Cancelled }

public enum ShipmentEventType { Advised, Departed, BorderCrossed, Delayed, EtaUpdated, Arrived, Received, Note }

public enum LogisticsEventType { BORDER_DELAY, PORT_DISRUPTION, WEATHER, QUALITY_ISSUE, PARTIAL_DELIVERY, NO_CONFIRMATION }

public enum EventSeverity { LOW, MEDIUM, HIGH }

public enum PartCategory { Mechanika, Elektronika, Materialy, Optyka, Zasilanie }

public enum MaterialLotStatus { AwaitingInspection, Accepted, ConditionallyReleased, Blocked, Recalled }

public enum DocumentType { MATERIAL_CERT, INSPECTION_REPORT, DECLARATION_OF_CONFORMITY, TRANSPORT_DOC }

public enum DocumentStatus { Pending, Verifying, Accepted, Rejected, RequiresCompletion, Missing }

public enum InspectionResult { Passed, Failed, Conditional }

public enum NonConformanceStatus { Open, UnderReview, Closed }

public enum ProductionOrderStatus { Planned, Released, InProgress, Completed, OnHold, Cancelled }

public enum OperationStatus { Planned, Ready, InProgress, Completed }

public enum SerialStatus { Planned, InProduction, Completed, Shipped, Quarantined }

public enum PassportStatus { Draft, PendingReview, Approved, Generated, Invalidated }

public enum PassportVersionStatus { Current, Superseded, Invalidated }

public enum PlanningBaselineStatus { Active, Superseded }

public enum PlanningScenarioStatus { Draft, Running, Completed, Failed, Approved, Rejected, Saved }

public enum ScenarioChangeType { DELAY_INBOUND, BLOCK_LOT, PRIORITY, CAPACITY, DELAY_ORDER }

public enum RiskCategory { Low, Medium, High, Critical }

public enum NotificationSeverity { Info, Warning, Critical }

public enum AuditSource { Api, System, Seed, Demo }

public enum TraceLinkKind
{
    SupplierToPurchaseOrder, PurchaseOrderToLine, LineToShipment, LineToLot, LotToReceipt, LotToInspection,
    LotToConsumption, ConsumptionToOrder, OrderToOperation, OrderToSerial, SerialToPassport, LotToDocument
}
