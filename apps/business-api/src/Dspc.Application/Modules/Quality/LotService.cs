using Dspc.Application.Abstractions;
using Dspc.Application.Common;
using Dspc.Application.Modules.Inbound;
using Dspc.Application.Modules.Risk;
using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Dspc.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace Dspc.Application.Modules.Quality;

/// <summary>
/// Material lots: read model, inspections and the quality block. Blocking is the demo's second scenario — it raises
/// <see cref="MaterialLotBlocked"/>, which the outbox delivers to <see cref="MaterialLotBlockedHandler"/> to invalidate
/// the passports built from the lot, flag reservations and re-score inbound risk for the part.
/// </summary>
public sealed class LotService(
    IAppDbContext db, ISupplierScope scope, ICurrentUser user, IDemoClock clock,
    IEventPublisher events, IAuditWriter audit, TraceabilityIndex trace,
    Passports.PassportInvalidationService passportInvalidation)
{
    public async Task<ListResult<LotSummaryDto>> ListAsync(Guid siteId, string? partCode, string? status, string? q, CancellationToken ct)
    {
        var query = scope.Apply(db.MaterialLots.AsNoTracking()).Where(l => l.SiteId == siteId).Include(l => l.Part).Include(l => l.Supplier).AsQueryable();
        if (!string.IsNullOrWhiteSpace(partCode)) query = query.Where(l => l.Part!.Code == partCode);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<MaterialLotStatus>(status, true, out var s)) query = query.Where(l => l.Status == s);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(l => l.LotNumber.ToLower().Contains(term) || (l.HeatNumber != null && l.HeatNumber.ToLower().Contains(term)) || l.Part!.Code.ToLower().Contains(term));
        }
        var list = await query.OrderBy(l => l.Part!.Code).ThenBy(l => l.LotNumber).Take(500).ToListAsync(ct);
        return ListResult.Of(list.Select(ToSummary).ToList());
    }

    public async Task<LotDto> GetAsync(string lotNumber, CancellationToken ct)
    {
        var lot = await LoadAsync(lotNumber, tracking: false, ct);
        return await ToDetailAsync(lot, ct);
    }

    public async Task<ListResult<NonConformanceDto>> NonConformancesAsync(CancellationToken ct)
    {
        var list = await db.NonConformances.AsNoTracking().Include(n => n.MaterialLot).OrderByDescending(n => n.RaisedAt).Take(200).ToListAsync(ct);
        return ListResult.Of(list.Select(ToNcrDto).ToList());
    }

    public async Task<InspectionDto> AddInspectionAsync(string lotNumber, AddInspectionRequest req, CancellationToken ct)
    {
        var lot = await LoadAsync(lotNumber, tracking: true, ct);
        var result = Enum.Parse<InspectionResult>(req.Result, true);
        var now = clock.UtcNow;
        var inspection = new QualityInspection
        {
            Id = Guid.NewGuid(), Code = $"QI-{now:yyyy}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}", MaterialLotId = lot.Id,
            Result = result, InspectedBy = user.Username, InspectedAt = req.InspectedAt ?? now, Notes = req.Notes, CreatedAt = now, UpdatedAt = now
        };
        db.QualityInspections.Add(inspection);

        var before = new { Status = lot.Status.ToString() };
        lot.Status = result switch
        {
            InspectionResult.Passed => MaterialLotStatus.Accepted,
            InspectionResult.Conditional => MaterialLotStatus.ConditionallyReleased,
            _ => lot.Status
        };
        lot.UpdatedAt = now;
        audit.Write("Lot.Inspect", "MaterialLot", lot.LotNumber, lot.Id, before, new { Result = result.ToString(), Status = lot.Status.ToString(), req.Notes });
        await db.SaveChangesAsync(ct);

        if (result == InspectionResult.Failed)
            await BlockInternalAsync(lot, $"Inspekcja {inspection.Code}: wynik negatywny. {req.Notes}".Trim(), $"Negatywny wynik inspekcji partii {lot.LotNumber}", ct);

        return new InspectionDto(inspection.Id, inspection.Result.ToString(), inspection.Notes, inspection.InspectedAt, inspection.InspectedBy, inspection.Code);
    }

    public async Task<BlockLotResponse> BlockAsync(string lotNumber, BlockLotRequest req, CancellationToken ct)
    {
        var lot = await LoadAsync(lotNumber, tracking: true, ct);
        if (lot.Status is MaterialLotStatus.Blocked or MaterialLotStatus.Recalled)
            throw new ConflictException($"Lot '{lotNumber}' is already {lot.Status}.");
        var (affected, ncr) = await BlockInternalAsync(lot, req.Reason, req.NcrTitle, ct);
        var dto = await ToDetailAsync(await LoadAsync(lotNumber, tracking: false, ct), ct);
        return new BlockLotResponse(dto, affected, ToNcrDto(ncr));
    }

    private async Task<(AffectedRecordsDto Affected, NonConformance Ncr)> BlockInternalAsync(MaterialLot lot, string reason, string ncrTitle, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var impact = await trace.ForwardAsync(lot.LotNumber, ct);
        var before = new { Status = lot.Status.ToString() };

        lot.Status = MaterialLotStatus.Blocked;
        lot.BlockReason = reason;
        lot.BlockedAt = now;
        lot.UpdatedAt = now;

        var ncr = new NonConformance
        {
            Id = Guid.NewGuid(), Code = $"NCR-{now:yyyy}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}", Title = ncrTitle, Description = reason,
            Status = NonConformanceStatus.Open, MaterialLotId = lot.Id, SupplierId = lot.SupplierId, RaisedBy = user.Username, RaisedAt = now, CreatedAt = now, UpdatedAt = now
        };
        db.NonConformances.Add(ncr);

        // reservations against the lot become unusable material
        var reservations = await db.Reservations.Include(r => r.ProductionOrder).Where(r => r.MaterialLotId == lot.Id).ToListAsync(ct);
        foreach (var r in reservations) r.IsBlocked = true;

        // denormalised balance follows the lot
        var balance = await db.InventoryBalances.FirstOrDefaultAsync(b => b.PartId == lot.PartId, ct);
        if (balance is not null)
        {
            balance.OnHand = Math.Max(0, balance.OnHand - lot.RemainingQuantity);
            balance.Blocked += lot.RemainingQuantity;
            balance.Reserved = Math.Max(0, balance.Reserved - reservations.Sum(r => r.Quantity));
            balance.UpdatedAt = now;
        }

        // passports built from the lot lose validity immediately — same transaction, no window where the UI shows a
        // "Generated" passport for blocked material. Notifications and risk re-scoring follow via the outbox handler.
        var invalidatedPassports = await passportInvalidation.InvalidateForSerialsAsync(impact.Serials, $"Partia {lot.LotNumber} zablokowana jakościowo: {reason}", ct);

        var partCode = lot.Part?.Code ?? await db.Parts.Where(p => p.Id == lot.PartId).Select(p => p.Code).FirstAsync(ct);
        var affected = new AffectedRecordsDto(impact.OrderCodes, impact.Serials, invalidatedPassports.Count > 0 ? invalidatedPassports : impact.PassportSerials);
        events.Publish(new MaterialLotBlocked(now, user.CorrelationId, lot.LotNumber, partCode, reason, [.. affected.Orders], [.. affected.Serials]));
        audit.Write("Lot.Block", "MaterialLot", lot.LotNumber, lot.Id, before,
            new { Status = lot.Status.ToString(), Reason = reason, Ncr = ncr.Code, AffectedOrders = affected.Orders, AffectedSerials = affected.Serials, AffectedPassports = affected.Passports });
        await db.SaveChangesAsync(ct);
        return (affected, ncr);
    }

    private async Task<MaterialLot> LoadAsync(string lotNumber, bool tracking, CancellationToken ct)
    {
        var q = scope.Apply(tracking ? db.MaterialLots : db.MaterialLots.AsNoTracking()).Include(l => l.Part).Include(l => l.Supplier);
        return await q.FirstOrDefaultAsync(l => l.LotNumber == lotNumber, ct) ?? throw new NotFoundException("MaterialLot", lotNumber);
    }

    private async Task<LotDto> ToDetailAsync(MaterialLot lot, CancellationToken ct)
    {
        var documents = await db.QualityDocuments.AsNoTracking().Where(d => d.MaterialLotId == lot.Id || d.LotNumber == lot.LotNumber).OrderBy(d => d.Type).ToListAsync(ct);
        var inspections = await db.QualityInspections.AsNoTracking().Where(i => i.MaterialLotId == lot.Id).OrderByDescending(i => i.InspectedAt).ToListAsync(ct);
        var consumptions = await db.MaterialConsumptions.AsNoTracking().Include(c => c.ProductionOrder).Include(c => c.ProductSerial)
            .Where(c => c.MaterialLotId == lot.Id).ToListAsync(ct);
        var reserved = await db.Reservations.AsNoTracking().Include(r => r.ProductionOrder).Where(r => r.MaterialLotId == lot.Id).Select(r => r.ProductionOrder!.Code).Distinct().OrderBy(c => c).ToListAsync(ct);
        var ncrs = await db.NonConformances.AsNoTracking().Where(n => n.MaterialLotId == lot.Id).OrderByDescending(n => n.RaisedAt).ToListAsync(ct);
        var poCode = lot.PurchaseOrderLineId is { } lid
            ? await db.PurchaseOrderLines.AsNoTracking().Where(l => l.Id == lid).Select(l => l.PurchaseOrder!.Code).FirstOrDefaultAsync(ct)
            : null;

        var consumedBy = consumptions.GroupBy(c => c.ProductionOrder!.Code)
            .OrderBy(g => g.Key)
            .Select(g => new LotConsumptionDto(g.Key, g.Where(c => c.ProductSerial is not null).Select(c => c.ProductSerial!.SerialNumber).Distinct().Order().ToList(), g.Sum(c => c.Quantity)))
            .ToList();

        var site = await db.Sites.AsNoTracking().Where(x => x.Id == lot.SiteId)
            .Select(x => new { x.Code, x.Name }).FirstOrDefaultAsync(ct);

        return new LotDto(
            lot.LotNumber, lot.HeatNumber, lot.Part?.Code ?? "", lot.Part?.NamePl, lot.Supplier?.Code ?? "", lot.Supplier?.Name,
            lot.Quantity, lot.RemainingQuantity, lot.Unit, lot.Status.ToString(), lot.ReceivedOn, lot.CountryOfOrigin,
            lot.PurchaseOrderLineId, poCode, lot.ProducedOn, lot.ExpiresOn, lot.BlockReason, lot.BlockedAt,
            documents.Select(PurchaseOrderQueries.ToDocumentSummary).ToList(),
            inspections.Select(i => new InspectionDto(i.Id, i.Result.ToString(), i.Notes, i.InspectedAt, i.InspectedBy, i.Code)).ToList(),
            consumedBy, reserved, ncrs.Select(ToNcrDto).ToList(), lot.RowVersion.ToString(),
            site?.Code ?? "", site?.Name ?? "");
    }

    public static LotSummaryDto ToSummary(MaterialLot l) => new(
        l.LotNumber, l.HeatNumber, l.Part?.Code ?? "", l.Part?.NamePl, l.Supplier?.Code ?? "", l.Supplier?.Name,
        l.Quantity, l.RemainingQuantity, l.Unit, l.Status.ToString(), l.ReceivedOn, l.CountryOfOrigin);

    public static NonConformanceDto ToNcrDto(NonConformance n) =>
        new(n.Id, n.Code, n.Title, n.Status.ToString(), n.RaisedAt, n.Description, n.MaterialLot?.LotNumber);
}
