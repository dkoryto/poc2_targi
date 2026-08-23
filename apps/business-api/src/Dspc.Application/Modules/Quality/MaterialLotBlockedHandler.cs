using Dspc.Application.Abstractions;
using Dspc.Application.Common;
using Dspc.Application.Modules.Risk;
using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Dspc.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dspc.Application.Modules.Quality;

/// <summary>
/// Consequences of a quality block, delivered through the outbox: every passport whose genealogy contains the lot is
/// invalidated (previous PDF versions are kept and marked, the reason is recorded), planners and quality are notified,
/// and inbound risk for the part is re-scored because free stock just dropped.
/// </summary>
public sealed class MaterialLotBlockedHandler(
    IAppDbContext db, TraceabilityIndex trace, RiskAssessmentService risk, Passports.PassportInvalidationService passportInvalidation,
    IDemoClock clock, ILogger<MaterialLotBlockedHandler> log) : IDomainEventHandler<MaterialLotBlocked>
{
    public async Task HandleAsync(MaterialLotBlocked e, CancellationToken ct)
    {
        var impact = await trace.ForwardAsync(e.LotNumber, ct);
        var now = clock.UtcNow;
        var reason = $"Partia {e.LotNumber} zablokowana jakościowo: {e.Reason}";

        // normally already done inside the blocking transaction; idempotent safety net for other block paths
        var invalidated = await passportInvalidation.InvalidateForSerialsAsync(impact.Serials, reason, ct);

        // orders still holding a reservation on the lot lose material completeness
        var lot = await db.MaterialLots.FirstOrDefaultAsync(l => l.LotNumber == e.LotNumber, ct);
        if (lot is not null)
        {
            foreach (var r in await db.Reservations.Include(r => r.ProductionOrder).Where(r => r.MaterialLotId == lot.Id && !r.IsBlocked).ToListAsync(ct))
                r.IsBlocked = true;
        }

        db.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(), TargetRole = Role.ProductionPlanner, Severity = NotificationSeverity.Critical,
            TitleKey = "notifications.lotBlocked.title", MessageKey = "notifications.lotBlocked.message",
            ParamsJson = Json.Serialize(new { lotNumber = e.LotNumber, partCode = e.PartCode, orders = string.Join(", ", impact.OrderCodes), passports = invalidated.Count, eventName = EventNames.MaterialLotBlocked }),
            Route = $"/trace/lots/{e.LotNumber}", CreatedAt = now, UpdatedAt = now
        });
        if (invalidated.Count > 0)
        {
            db.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(), TargetRole = Role.QualityInspector, Severity = NotificationSeverity.Critical,
                TitleKey = "notifications.passportInvalidated.title", MessageKey = "notifications.passportInvalidated.message",
                ParamsJson = Json.Serialize(new { serials = string.Join(", ", invalidated), lotNumber = e.LotNumber, eventName = EventNames.PassportInvalidated }),
                Route = $"/passports/{invalidated[0]}", CreatedAt = now, UpdatedAt = now
            });
        }

        await db.SaveChangesAsync(ct);
        await risk.RecalculateAffectedAsync(e.PartCode, "MaterialLotBlocked", ct);
        await db.SaveChangesAsync(ct);

        log.LogInformation("Lot {Lot} blocked: {Passports} passports invalidated, {Orders} orders affected", e.LotNumber, invalidated.Count, impact.OrderCodes.Count);
    }
}
