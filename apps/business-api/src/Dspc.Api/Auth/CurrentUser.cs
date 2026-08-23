using System.Security.Claims;
using Dspc.Application.Abstractions;
using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Dspc.Infrastructure.Identity;
using Dspc.Api.Middleware;

namespace Dspc.Api.Auth;

/// <summary>Current principal from the JWT; outside an HTTP request (hosted services, seeder) it is the system user.</summary>
public sealed class HttpCurrentUser : ICurrentUser
{
    private readonly HttpContext? _ctx;
    public HttpCurrentUser(IHttpContextAccessor accessor) { _ctx = accessor.HttpContext; }

    private ClaimsPrincipal? P => _ctx?.User;
    public bool IsAuthenticated => P?.Identity?.IsAuthenticated == true;
    public Guid? Id => Guid.TryParse(P?.FindFirst("sub")?.Value, out var g) ? g : null;
    public string Username => IsAuthenticated ? P?.FindFirst("unique_name")?.Value ?? P?.Identity?.Name ?? "unknown" : "system";
    public Role? Role => Enum.TryParse<Role>(P?.FindFirst(DspcClaims.Role)?.Value, out var r) ? r : null;
    public Guid? SupplierId => Guid.TryParse(P?.FindFirst(DspcClaims.SupplierId)?.Value, out var g) ? g : null;
    public string? SupplierCode => P?.FindFirst(DspcClaims.SupplierCode)?.Value;
    public Guid? SiteId => Guid.TryParse(P?.FindFirst(DspcClaims.SiteId)?.Value, out var g) ? g : null;
    public string CorrelationId => _ctx is null ? "system" : CorrelationIdMiddleware.Get(_ctx);
    public string? IpAddress => _ctx?.Connection.RemoteIpAddress?.ToString();
}

/// <summary>Supplier users only see their own organisation; everyone else is unrestricted. Applied inside every inbound/document query.</summary>
public sealed class SupplierScope(ICurrentUser user) : ISupplierScope
{
    public bool IsRestricted => user.Role == Domain.Common.Role.SupplierUser;
    public Guid? SupplierId => IsRestricted ? user.SupplierId ?? Guid.Empty : null;

    public IQueryable<PurchaseOrder> Apply(IQueryable<PurchaseOrder> q) => IsRestricted ? q.Where(p => p.SupplierId == SupplierId) : q;
    public IQueryable<PurchaseOrderLine> Apply(IQueryable<PurchaseOrderLine> q) => IsRestricted ? q.Where(l => l.PurchaseOrder!.SupplierId == SupplierId) : q;
    public IQueryable<Shipment> Apply(IQueryable<Shipment> q) => IsRestricted ? q.Where(s => s.SupplierId == SupplierId) : q;
    public IQueryable<QualityDocument> Apply(IQueryable<QualityDocument> q) => IsRestricted ? q.Where(d => d.SupplierId == SupplierId) : q;
    public IQueryable<MaterialLot> Apply(IQueryable<MaterialLot> q) => IsRestricted ? q.Where(l => l.SupplierId == SupplierId) : q;
    public IQueryable<Supplier> Apply(IQueryable<Supplier> q) => IsRestricted ? q.Where(s => s.Id == SupplierId) : q;
}

public static class Policies
{
    public const string Authenticated = "Authenticated";
    public const string Dashboard = "Dashboard";          // everyone except suppliers
    public const string SupplyRead = "SupplyRead";        // all roles (suppliers scoped)
    public const string SupplyWrite = "SupplyWrite";      // supplier, inbound coordinator, admin, presenter
    public const string Inbound = "Inbound";              // inbound coordinator, planner, director, admin, presenter, auditor (read)
    public const string InboundWrite = "InboundWrite";    // inbound coordinator, planner, admin, presenter
    public const string Planner = "Planner";
    public const string PlanApprove = "PlanApprove";
    public const string Quality = "Quality";
    public const string Trace = "Trace";
    public const string Audit = "Audit";
    public const string Admin = "Admin";
    public const string AdminWrite = "AdminWrite";
    public const string DemoControl = "DemoControl";

    public static void Configure(Microsoft.AspNetCore.Authorization.AuthorizationOptions o)
    {
        string[] R(params Role[] roles) => roles.Select(r => r.ToString()).ToArray();
        o.AddPolicy(Authenticated, p => p.RequireAuthenticatedUser());
        o.AddPolicy(Dashboard, p => p.RequireRole(R(Role.OperationsDirector, Role.ProductionPlanner, Role.InboundCoordinator, Role.QualityInspector, Role.Auditor, Role.Administrator, Role.DemoPresenter)));
        o.AddPolicy(SupplyRead, p => p.RequireAuthenticatedUser());
        o.AddPolicy(SupplyWrite, p => p.RequireRole(R(Role.SupplierUser, Role.InboundCoordinator, Role.Administrator, Role.DemoPresenter)));
        o.AddPolicy(Inbound, p => p.RequireRole(R(Role.InboundCoordinator, Role.ProductionPlanner, Role.OperationsDirector, Role.Administrator, Role.DemoPresenter, Role.Auditor)));
        o.AddPolicy(InboundWrite, p => p.RequireRole(R(Role.InboundCoordinator, Role.ProductionPlanner, Role.Administrator, Role.DemoPresenter)));
        o.AddPolicy(Planner, p => p.RequireRole(R(Role.ProductionPlanner, Role.OperationsDirector, Role.Administrator, Role.DemoPresenter, Role.Auditor, Role.InboundCoordinator, Role.QualityInspector)));
        o.AddPolicy(PlanApprove, p => p.RequireRole(R(Role.ProductionPlanner, Role.DemoPresenter)));
        o.AddPolicy(Quality, p => p.RequireRole(R(Role.QualityInspector, Role.Administrator, Role.DemoPresenter)));
        o.AddPolicy(Trace, p => p.RequireRole(R(Role.QualityInspector, Role.ProductionPlanner, Role.OperationsDirector, Role.Auditor, Role.Administrator, Role.DemoPresenter, Role.InboundCoordinator)));
        o.AddPolicy(Audit, p => p.RequireRole(R(Role.Auditor, Role.Administrator, Role.OperationsDirector, Role.DemoPresenter)));
        o.AddPolicy(Admin, p => p.RequireRole(R(Role.Administrator, Role.DemoPresenter)));
        o.AddPolicy(AdminWrite, p => p.RequireRole(R(Role.Administrator)));
        o.AddPolicy(DemoControl, p => p.RequireRole(R(Role.DemoPresenter, Role.Administrator)));
    }
}
