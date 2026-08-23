using System.Security.Cryptography;
using System.Text.Json;
using Dspc.Application.Abstractions;
using Dspc.Application.Common;
using Dspc.Application.Modules.Quality;
using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Dspc.Domain.Events;
using Dspc.Domain.Quality;
using Microsoft.EntityFrameworkCore;

namespace Dspc.Application.Modules.Passports;

/// <summary>Renders the passport snapshot into a PDF. Implemented in Infrastructure (QuestPDF + QRCoder, offline).</summary>
public interface IPassportPdfGenerator
{
    byte[] Render(PassportRenderModel model);

    /// <summary>PNG QR code for a relative in-app link — nothing is fetched from the network.</summary>
    byte[] RenderQr(string payload);
}

public sealed record PassportRenderComponent(string PartCode, string? PartName, string LotNumber, string? HeatNumber, string SupplierCode, string? SupplierName, string? Country, string? CertificateNumber, string? CertSha256, string LotStatus);

public sealed record PassportRenderInspection(string Code, string Result, DateTime InspectedAt, string InspectedBy, string? Notes);

public sealed record PassportRenderDeviation(string? Code, string Title, string? ApprovedBy, DateTime? ApprovedAt);

public sealed record PassportRenderModel(
    string Serial, string ProductCode, string ProductName, string OrderCode, string BomVersion, string TemplateCode,
    int Version, DateTime GeneratedAt, string GeneratedBy, string? ApprovedBy, DateTime? ApprovedAt,
    IReadOnlyList<PassportRenderComponent> Components, IReadOnlyList<PassportRenderInspection> Inspections,
    IReadOnlyList<PassportRenderDeviation> Deviations, IReadOnlyList<(string Code, bool Satisfied, string? Evidence)> Requirements,
    string QrPayload, string SiteName);

/// <summary>
/// Digital Quality Passport: completeness against template <c>DQP-01</c>, quality-inspector approval, and versioned PDF
/// generation (QR + SHA-256). Generation is refused with 422 and the concrete missing list while anything mandatory is
/// absent. Blocking a component lot invalidates the passport (see <see cref="MaterialLotBlockedHandler"/>); earlier
/// versions are always kept.
/// </summary>
public sealed class PassportService(
    IAppDbContext db, ICurrentUser user, IDemoClock clock, IEventPublisher events, IAuditWriter audit,
    IDocumentStorage storage, IPassportPdfGenerator pdf)
{
    /// <summary>Roles allowed to see supplier identity and country of origin on a passport.</summary>
    private static readonly Role[] OriginVisibleTo =
        [Role.QualityInspector, Role.OperationsDirector, Role.Administrator, Role.Auditor, Role.DemoPresenter, Role.ProductionPlanner, Role.InboundCoordinator];

    public async Task<ListResult<PassportSummaryDto>> ListAsync(Guid siteId, string? status, string? q, CancellationToken ct)
    {
        var query = db.Passports.AsNoTracking()
            .Where(p => p.ProductSerial!.ProductionOrder!.SiteId == siteId)
            .Include(p => p.ProductSerial).ThenInclude(s => s!.Product)
            .Include(p => p.ProductSerial).ThenInclude(s => s!.ProductionOrder)
            .Include(p => p.Versions)
            .Include(p => p.Template)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<PassportStatus>(status, true, out var s)) query = query.Where(p => p.Status == s);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(p => p.ProductSerial!.SerialNumber.ToLower().Contains(term) || p.ProductSerial!.ProductionOrder!.Code.ToLower().Contains(term));
        }
        var list = await query.OrderBy(p => p.ProductSerial!.SerialNumber).Take(300).ToListAsync(ct);

        var result = new List<PassportSummaryDto>(list.Count);
        foreach (var p in list)
        {
            var facts = await FactsAsync(p, ct);
            var completeness = PassportCompletenessEvaluator.Evaluate(facts);
            result.Add(new PassportSummaryDto(
                p.ProductSerial!.SerialNumber, p.ProductSerial.Product?.Code ?? "", p.ProductSerial.Product?.NamePl,
                p.ProductSerial.ProductionOrder?.Code ?? "", p.Status.ToString(), p.Template?.Code ?? PassportCompletenessEvaluator.TemplateCode,
                completeness.Complete, completeness.Missing.Count, p.UpdatedAt,
                p.Versions.Count == 0 ? null : p.Versions.Max(v => v.Version)));
        }
        return ListResult.Of(result);
    }

    public async Task<PassportDto> GetAsync(string serial, CancellationToken ct)
    {
        var passport = await LoadAsync(serial, tracking: false, ct);
        return await ToDtoAsync(passport, ct);
    }

    public async Task<PassportDto> ApproveAsync(string serial, CancellationToken ct)
    {
        var passport = await LoadAsync(serial, tracking: true, ct);
        var facts = await FactsAsync(passport, ct);
        // approval itself is the missing piece at this point — judge the rest
        var completeness = PassportCompletenessEvaluator.Evaluate(facts with { ApprovedBy = user.Username, ApprovedAt = clock.UtcNow, Status = PassportStatus.PendingReview });
        if (!completeness.Complete)
            throw new UnprocessableException("Passport is incomplete and cannot be approved.",
                Payload(("missing", completeness.Missing.Select(ToMissingDto).ToList())));
        if (passport.Status == PassportStatus.Invalidated)
            throw new ConflictException("Invalidated passport must be re-evaluated before approval.");

        var before = new { Status = passport.Status.ToString(), passport.ApprovedBy };
        passport.Status = PassportStatus.Approved;
        passport.ApprovedBy = user.Username;
        passport.ApprovedAt = clock.UtcNow;
        passport.InvalidationReason = null;
        passport.InvalidatedAt = null;
        passport.UpdatedAt = clock.UtcNow;
        audit.Write("Passport.Approve", "Passport", serial, passport.Id, before, new { Status = passport.Status.ToString(), passport.ApprovedBy });
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(await LoadAsync(serial, tracking: false, ct), ct);
    }

    public async Task<GeneratePassportResponse> GenerateAsync(string serial, CancellationToken ct)
    {
        var passport = await LoadAsync(serial, tracking: true, ct);
        var facts = await FactsAsync(passport, ct);
        var completeness = PassportCompletenessEvaluator.Evaluate(facts);
        if (!completeness.Complete)
            throw new UnprocessableException("Passport is incomplete — resolve the missing items before generating the document.",
                Payload(("missing", completeness.Missing.Select(ToMissingDto).ToList()),
                        ("requirements", completeness.Requirements.Select(r => new { r.Code, r.Satisfied, r.Evidence }).ToList())));
        if (passport.Status is not (PassportStatus.Approved or PassportStatus.Generated))
            throw new UnprocessableException($"Passport must be approved by a quality inspector before generation (current status: {passport.Status}).",
                Payload(("missing", new[] { ToMissingDto(new MissingRequirement(PassportCompletenessEvaluator.Requirements.Approval, "passports.missing.APPROVAL", new Dictionary<string, object>())) })));

        var version = await GenerateVersionAsync(passport, facts, completeness, user.Username, ct);
        audit.Write("Passport.Generate", "Passport", serial, passport.Id, null, new { version.Version, version.Sha256, version.FileSize });
        events.Publish(new PassportGenerated(clock.UtcNow, user.CorrelationId, serial, version.Version, version.Sha256));
        await db.SaveChangesAsync(ct);
        return new GeneratePassportResponse(version.Version, version.Sha256, $"/api/v1/passports/{Uri.EscapeDataString(serial)}/versions/{version.Version}/pdf", version.FileSize, version.GeneratedAt);
    }

    /// <summary>Renders and stores a new version. Shared by the endpoint and the seed post-processor.</summary>
    public async Task<PassportVersion> GenerateVersionAsync(Passport passport, PassportFacts facts, CompletenessResult completeness, string generatedBy, CancellationToken ct)
    {
        var serial = passport.ProductSerial!.SerialNumber;
        var now = clock.UtcNow;
        var next = (await db.PassportVersions.Where(v => v.PassportId == passport.Id).MaxAsync(v => (int?)v.Version, ct) ?? 0) + 1;

        var model = new PassportRenderModel(
            serial, facts.ProductCode ?? "", facts.ProductName ?? "", facts.OrderCode ?? "", facts.BomVersion ?? "",
            passport.Template?.Code ?? PassportCompletenessEvaluator.TemplateCode, next, now, generatedBy, facts.ApprovedBy, facts.ApprovedAt,
            facts.Components.Select(c => new PassportRenderComponent(c.PartCode, c.PartName, c.LotNumber, c.HeatNumber, c.SupplierCode, c.SupplierName, c.Country, c.CertificateNumber, c.CertificateSha256, c.LotStatus.ToString())).ToList(),
            facts.Inspections.Select(i => new PassportRenderInspection(i.Code, i.Result.ToString(), i.InspectedAt, i.InspectedBy, i.Notes)).ToList(),
            facts.Deviations.Select(d => new PassportRenderDeviation(d.Code, d.Title, d.ApprovedBy, d.ApprovedAt)).ToList(),
            completeness.Requirements.Select(r => (r.Code, r.Satisfied, r.Evidence)).ToList(),
            $"/passports/{serial}", "Zakład Centralny (demo)");

        var bytes = pdf.Render(model);
        var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var key = $"passports/{serial}/v{next}.pdf";
        await storage.PutAsync(key, new MemoryStream(bytes), "application/pdf", ct);

        foreach (var v in await db.PassportVersions.Where(v => v.PassportId == passport.Id && v.Status == PassportVersionStatus.Current).ToListAsync(ct))
            v.Status = PassportVersionStatus.Superseded;

        var entity = new PassportVersion
        {
            Id = Guid.NewGuid(), PassportId = passport.Id, Version = next, Status = PassportVersionStatus.Current, StorageKey = key,
            Sha256 = sha, FileSize = bytes.LongLength, GeneratedBy = generatedBy, GeneratedAt = now,
            SnapshotJson = JsonSerializer.Serialize(model, Json.Options), CreatedAt = now, UpdatedAt = now
        };
        db.PassportVersions.Add(entity);
        passport.CurrentVersion = next;
        passport.Status = PassportStatus.Generated;
        passport.InvalidationReason = null;
        passport.InvalidatedAt = null;
        passport.UpdatedAt = now;
        return entity;
    }

    public async Task<PassportPdfDownload> DownloadAsync(string serial, int version, CancellationToken ct)
    {
        var passport = await LoadAsync(serial, tracking: false, ct);
        var v = passport.Versions.FirstOrDefault(x => x.Version == version) ?? throw new NotFoundException("PassportVersion", $"{serial}/v{version}");
        var stream = await storage.GetAsync(v.StorageKey, ct) ?? throw new NotFoundException("PassportVersion", $"{serial}/v{version}");
        return new PassportPdfDownload(stream, $"passport-{serial}-v{version}.pdf");
    }

    public async Task<byte[]> QrAsync(string serial, CancellationToken ct)
    {
        _ = await LoadAsync(serial, tracking: false, ct);
        return pdf.RenderQr($"/passports/{serial}");
    }

    public async Task<Passport> LoadAsync(string serial, bool tracking, CancellationToken ct)
    {
        var q = tracking ? db.Passports : db.Passports.AsNoTracking();
        return await q
            .Include(p => p.ProductSerial).ThenInclude(s => s!.Product)
            .Include(p => p.ProductSerial).ThenInclude(s => s!.ProductionOrder)
            .Include(p => p.Versions)
            .Include(p => p.Template)
            .FirstOrDefaultAsync(p => p.ProductSerial!.SerialNumber == serial, ct)
            ?? throw new NotFoundException("Passport", serial);
    }

    /// <summary>Collects the facts the completeness rules and the PDF need. Country/supplier are masked for roles without origin access.</summary>
    public async Task<PassportFacts> FactsAsync(Passport passport, CancellationToken ct)
    {
        var serial = passport.ProductSerial ?? throw new NotFoundException("ProductSerial", passport.ProductSerialId.ToString());
        var bom = await db.BomVersions.AsNoTracking().Where(b => b.Id == serial.BomVersionId).Select(b => b.Version).FirstOrDefaultAsync(ct);
        var keyParts = await db.BomItems.AsNoTracking().Where(i => i.BomVersionId == serial.BomVersionId && i.IsKeyComponent).Select(i => i.Part!.Code).ToListAsync(ct);

        var consumptions = await db.MaterialConsumptions.AsNoTracking()
            .Include(c => c.MaterialLot).ThenInclude(l => l!.Part)
            .Include(c => c.MaterialLot).ThenInclude(l => l!.Supplier)
            .Where(c => c.ProductSerialId == serial.Id)
            .ToListAsync(ct);

        var lotIds = consumptions.Select(c => c.MaterialLotId).Distinct().ToList();
        var docs = await db.QualityDocuments.AsNoTracking()
            .Where(d => d.MaterialLotId != null && lotIds.Contains(d.MaterialLotId!.Value))
            .ToListAsync(ct);
        var lotInspections = await db.QualityInspections.AsNoTracking()
            .Where(i => i.MaterialLotId != null && lotIds.Contains(i.MaterialLotId!.Value))
            .ToListAsync(ct);
        var serialInspections = await db.QualityInspections.AsNoTracking()
            .Where(i => i.ProductSerialId == serial.Id).OrderBy(i => i.InspectedAt).ToListAsync(ct);

        var showOrigin = user.Role is null || OriginVisibleTo.Contains(user.Role.Value);

        var components = consumptions
            .Where(c => c.MaterialLot is not null)
            .GroupBy(c => c.MaterialLot!.LotNumber)      // AsNoTracking does not resolve identities — group by code
            .Select(g =>
            {
                var lot = g.First().MaterialLot!;
                var cert = docs.Where(d => d.MaterialLotId == lot.Id && d.Status == DocumentStatus.Accepted
                                           && d.Type is DocumentType.MATERIAL_CERT or DocumentType.DECLARATION_OF_CONFORMITY)
                    .OrderBy(d => d.Type).FirstOrDefault();
                return new PassportComponentFacts(
                    lot.Part?.Code ?? "", lot.Part?.NamePl, lot.LotNumber, lot.HeatNumber,
                    showOrigin ? lot.Supplier?.Code ?? "" : "—", showOrigin ? lot.Supplier?.Name : null,
                    showOrigin ? lot.CountryOfOrigin : "—",
                    lot.Status, cert?.Sha256, cert?.DocumentNumber,
                    lotInspections.Any(i => i.MaterialLotId == lot.Id && i.Result != InspectionResult.Failed));
            })
            .OrderBy(c => keyParts.Contains(c.PartCode) ? 0 : 1).ThenBy(c => c.PartCode)
            .ToList();

        var deviations = ParseDeviations(passport.DeviationsJson);

        return new PassportFacts(
            serial.SerialNumber, serial.Product?.Code, serial.Product?.NamePl, serial.ProductionOrder?.Code, bom,
            passport.Status, passport.ApprovedBy, passport.ApprovedAt,
            components,
            serialInspections.Select(i => new PassportInspectionFacts(i.Code, i.Result, i.InspectedAt, i.InspectedBy, i.Notes)).ToList(),
            deviations);
    }

    public async Task<PassportDto> ToDtoAsync(Passport passport, CancellationToken ct)
    {
        var facts = await FactsAsync(passport, ct);
        var completeness = PassportCompletenessEvaluator.Evaluate(facts);
        var serialInspections = await db.QualityInspections.AsNoTracking().Where(i => i.ProductSerialId == passport.ProductSerialId).OrderByDescending(i => i.InspectedAt).ToListAsync(ct);

        return new PassportDto(
            facts.SerialNumber, facts.ProductCode ?? "", facts.ProductName, facts.OrderCode ?? "", facts.BomVersion,
            passport.Status.ToString(), passport.Template?.Code ?? PassportCompletenessEvaluator.TemplateCode,
            new PassportCompletenessDto(completeness.Complete, completeness.Missing.Select(ToMissingDto).ToList(),
                completeness.Requirements.Select(r => new PassportRequirementDto(r.Code, r.Satisfied, r.Evidence, r.Mandatory)).ToList()),
            facts.Components.Select(c => new PassportComponentDto(c.PartCode, c.PartName, c.LotNumber, c.SupplierCode, c.SupplierName, c.Country, c.CertificateSha256, c.LotStatus.ToString())).ToList(),
            serialInspections.Select(i => new InspectionDto(i.Id, i.Result.ToString(), i.Notes, i.InspectedAt, i.InspectedBy, i.Code)).ToList(),
            facts.Deviations.Select((d, idx) => new PassportDeviationDto($"{passport.Id:N}-{idx}", d.Code, d.Title, d.ApprovedBy is null ? "Open" : "Approved", d.ApprovedBy, d.ApprovedAt)).ToList(),
            passport.Versions.OrderByDescending(v => v.Version).Select(v => new PassportVersionDto(v.Version, v.GeneratedAt, v.GeneratedBy, v.Sha256, v.FileSize, v.Status.ToString())).ToList(),
            passport.ApprovedBy, passport.ApprovedAt, passport.InvalidatedAt, passport.InvalidationReason);
    }

    public static MissingItemDto ToMissingDto(MissingRequirement m) => new(m.Code, m.LabelKey, m.Params);

    /// <summary>Problem Details extensions: each entry becomes a top-level member of the 422 body.</summary>
    private static IReadOnlyDictionary<string, object?> Payload(params (string Key, object? Value)[] parts) =>
        parts.ToDictionary(p => p.Key, p => p.Value);

    public static IReadOnlyList<PassportDeviationFacts> ParseDeviations(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return [];
            return doc.RootElement.EnumerateArray().Select(e => new PassportDeviationFacts(
                e.TryGetProperty("code", out var c) ? c.GetString() : null,
                e.TryGetProperty("description", out var d) ? d.GetString() ?? "" : e.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "",
                e.TryGetProperty("approvedBy", out var a) ? a.GetString() : null,
                e.TryGetProperty("approvedAt", out var at) && at.ValueKind == JsonValueKind.String && DateTime.TryParse(at.GetString(), out var dt) ? dt : null)).ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
