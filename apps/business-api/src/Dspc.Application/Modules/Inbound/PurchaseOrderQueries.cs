using Dspc.Application.Abstractions;
using Dspc.Application.Common;
using Dspc.Application.Modules.Risk;
using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dspc.Application.Modules.Inbound;

public sealed record PurchaseOrderFilter(string? Status, string? SupplierCode, string? RiskCategory, string? SiteCode, DateOnly? DueFrom, DateOnly? DueTo, string? Q, string? PartCode);

public sealed class PurchaseOrderQueries(IAppDbContext db, ISupplierScope scope)
{
    public async Task<ListResult<PurchaseOrderSummaryDto>> ListAsync(PurchaseOrderFilter f, CancellationToken ct)
    {
        var q = scope.Apply(db.PurchaseOrders.AsNoTracking()).Include(p => p.Supplier).Include(p => p.Lines).ThenInclude(l => l.Part).AsQueryable();
        if (!string.IsNullOrWhiteSpace(f.SupplierCode)) q = q.Where(p => p.Supplier!.Code == f.SupplierCode);
        if (!string.IsNullOrWhiteSpace(f.Status) && Enum.TryParse<PurchaseOrderLineStatus>(f.Status, true, out var ls)) q = q.Where(p => p.Lines.Any(l => l.Status == ls));
        if (!string.IsNullOrWhiteSpace(f.RiskCategory) && Enum.TryParse<RiskCategory>(f.RiskCategory, true, out var rc)) q = q.Where(p => p.Lines.Any(l => l.RiskCategory == rc));
        if (f.DueFrom is { } df) q = q.Where(p => p.Lines.Any(l => l.RequiredDate >= df));
        if (f.DueTo is { } dt) q = q.Where(p => p.Lines.Any(l => l.RequiredDate <= dt));
        if (!string.IsNullOrWhiteSpace(f.PartCode)) q = q.Where(p => p.Lines.Any(l => l.Part!.Code == f.PartCode));
        if (!string.IsNullOrWhiteSpace(f.Q))
        {
            var term = f.Q.Trim().ToLower();
            q = q.Where(p => p.Code.ToLower().Contains(term) || p.Supplier!.Name.ToLower().Contains(term) || p.Lines.Any(l => l.Part!.Code.ToLower().Contains(term) || l.Part.NamePl.ToLower().Contains(term) || (l.LotNumber != null && l.LotNumber.ToLower().Contains(term))));
        }
        var sites = await db.Sites.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Code, ct);
        var list = await q.OrderByDescending(p => p.Lines.Max(l => (int?)l.RiskScore) ?? 0).ThenBy(p => p.Code).ToListAsync(ct);
        var items = list.Select(p =>
        {
            var max = p.Lines.OrderByDescending(l => l.RiskScore).FirstOrDefault();
            return new PurchaseOrderSummaryDto(p.Code, p.Supplier!.Code, p.Supplier.Name, p.Status.ToString(), p.OrderedOn,
                p.Lines.Count == 0 ? null : p.Lines.Min(l => l.RequiredDate), p.Lines.Count == 0 ? null : p.Lines.Max(l => l.Eta),
                p.Lines.Count, p.Lines.Count(l => l.Status != PurchaseOrderLineStatus.Delivered),
                max?.RiskScore ?? 0, (max?.RiskCategory ?? RiskCategory.Low).ToString(),
                p.Lines.Count == 0 ? 0 : (int)Math.Round(p.Lines.Average(l => l.ProgressPercent)), sites.GetValueOrDefault(p.SiteId, ""));
        }).ToList();
        if (!string.IsNullOrWhiteSpace(f.SiteCode)) items = items.Where(i => i.SiteCode == f.SiteCode).ToList();
        return ListResult.Of(items);
    }

    public async Task<PurchaseOrderDetailDto> GetAsync(string code, CancellationToken ct)
    {
        var po = await scope.Apply(db.PurchaseOrders.AsNoTracking()).Include(p => p.Supplier)
            .Include(p => p.Lines).ThenInclude(l => l.Part)
            .Include(p => p.Lines).ThenInclude(l => l.Documents)
            .Include(p => p.Lines).ThenInclude(l => l.Shipment)
            .Include(p => p.Lines).ThenInclude(l => l.History)
            .FirstOrDefaultAsync(p => p.Code == code, ct) ?? throw new NotFoundException("PurchaseOrder", code);
        var site = await db.Sites.AsNoTracking().FirstAsync(s => s.Id == po.SiteId, ct);
        var lineIds = po.Lines.Select(l => l.Id).ToList();
        var latest = await db.RiskAssessments.AsNoTracking().Where(r => lineIds.Contains(r.PurchaseOrderLineId))
            .GroupBy(r => r.PurchaseOrderLineId).Select(g => g.OrderByDescending(r => r.AssessedAt).First()).ToListAsync(ct);
        var byLine = latest.ToDictionary(r => r.PurchaseOrderLineId);
        var lines = po.Lines.OrderBy(l => l.LineNo).Select(l => ToLineDto(l, byLine.GetValueOrDefault(l.Id))).ToList();
        var history = po.Lines.SelectMany(l => l.History.Select(h => new ChangeEntry(h.Id, h.CreatedAt, h.ChangedBy, $"L{l.LineNo}.{h.Field}", h.Field, h.OldValue, h.NewValue, h.Comment)))
            .OrderByDescending(h => h.OccurredAt).ToList();
        var sup = po.Supplier!;
        var openOrders = await db.PurchaseOrders.AsNoTracking().CountAsync(p => p.SupplierId == sup.Id && (p.Status == PurchaseOrderStatus.Open || p.Status == PurchaseOrderStatus.PartiallyDelivered), ct);
        var activeShipments = await db.Shipments.AsNoTracking().CountAsync(s => s.SupplierId == sup.Id && s.Status != ShipmentStatus.Received && s.Status != ShipmentStatus.Cancelled, ct);
        var supRisk = await db.PurchaseOrderLines.AsNoTracking().Where(l => l.PurchaseOrder!.SupplierId == sup.Id && l.Status != PurchaseOrderLineStatus.Delivered).Select(l => (int?)l.RiskScore).MaxAsync(ct) ?? 0;
        var supplierRef = new SupplierRefDto(sup.Code, sup.Name, sup.Country, sup.City, sup.Latitude, sup.Longitude, sup.OtifPercent, sup.QualityScore, supRisk, openOrders, activeShipments);
        return new PurchaseOrderDetailDto(po.Code, supplierRef, po.Status.ToString(), po.OrderedOn, po.Notes, site.Code, lines, history, po.RowVersion.ToString());
    }

    public static PurchaseOrderLineDto ToLineDto(PurchaseOrderLine l, RiskAssessment? assessment, RiskSummaryDto? risk = null)
    {
        var part = l.Part!;
        var required = Json.Deserialize<List<string>>(part.RequiredDocumentTypesJson) ?? new();
        risk ??= assessment is not null ? RiskAssessmentService.FromAssessment(assessment) : new RiskSummaryDto(l.RiskScore, l.RiskCategory.ToString(), [], [], [], l.UpdatedAt);
        return new PurchaseOrderLineDto(l.Id, l.LineNo, part.Code, part.NamePl, part.NameEn, part.Category.ToString(), l.Quantity, l.DeliveredQuantity, part.Unit,
            l.RequiredDate, l.Eta, l.OriginalEta, l.ProgressPercent, l.Status.ToString(), l.SupplierConfirmed,
            l.LotNumber, l.HeatNumber, l.ProducedOn, l.ExpiresOn, l.DeliveredOn, risk,
            l.Documents.OrderBy(d => d.Type).Select(ToDocumentSummary).ToList(), required, l.Shipment?.Code, l.RowVersion.ToString(), l.LastComment);
    }

    public static DocumentSummary ToDocumentSummary(QualityDocument d) => new(d.Id, d.Type.ToString(), d.DocumentNumber, d.FileName, d.SizeBytes, d.Sha256, d.Status.ToString(),
        d.UploadedAt, d.UploadedBy, d.LotNumber, d.HeatNumber, d.IssuedOn, d.VerifiedBy, d.VerifiedAt, d.VerificationComment,
        d.AiSuggestionJson is null ? null : Json.Deserialize<object>(d.AiSuggestionJson));
}
