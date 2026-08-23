using Dspc.Application.Abstractions;
using Dspc.Application.Modules.Quality;
using Dspc.Domain.Common;
using Dspc.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace Dspc.Application.Modules.Passports;

/// <summary>
/// A quality block on a lot invalidates every passport built from it. Invoked inside the blocking transaction so the
/// UI never shows a stale "Generated" passport for a blocked lot; the outbox handler afterwards only adds notifications
/// and risk re-scoring. Idempotent — passports already invalidated are left alone. Previous PDF versions are retained
/// and marked <c>Invalidated</c>, never deleted.
/// </summary>
public sealed class PassportInvalidationService(IAppDbContext db, TraceabilityIndex trace, IEventPublisher events, IDemoClock clock, ICurrentUser user)
{
    public async Task<IReadOnlyList<string>> InvalidateForLotAsync(string lotNumber, string reason, CancellationToken ct)
    {
        var impact = await trace.ForwardAsync(lotNumber, ct);
        if (impact.Serials.Count == 0) return [];
        return await InvalidateForSerialsAsync(impact.Serials, $"Partia {lotNumber} zablokowana jakościowo: {reason}", ct);
    }

    public async Task<IReadOnlyList<string>> InvalidateForSerialsAsync(IReadOnlyList<string> serials, string reason, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var passports = await db.Passports
            .Include(p => p.ProductSerial)
            .Include(p => p.Versions)
            .Where(p => serials.Contains(p.ProductSerial!.SerialNumber) && p.Status != PassportStatus.Invalidated)
            .ToListAsync(ct);

        var invalidated = new List<string>();
        foreach (var p in passports)
        {
            p.Status = PassportStatus.Invalidated;
            p.InvalidationReason = reason;
            p.InvalidatedAt = now;
            p.UpdatedAt = now;
            foreach (var v in p.Versions.Where(v => v.Status == PassportVersionStatus.Current))
                v.Status = PassportVersionStatus.Invalidated;
            invalidated.Add(p.ProductSerial!.SerialNumber);
            events.Publish(new PassportInvalidated(now, user.CorrelationId, p.ProductSerial!.SerialNumber, reason));
        }
        return invalidated;
    }
}
