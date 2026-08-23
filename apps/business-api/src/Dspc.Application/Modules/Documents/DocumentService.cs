using System.Security.Cryptography;
using Dspc.Application.Abstractions;
using Dspc.Application.Common;
using Dspc.Application.Modules.Inbound;
using Dspc.Application.Modules.Risk;
using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Dspc.Domain.Events;
using FluentValidation;
using ValidationException = Dspc.Application.Common.ValidationException;
using Microsoft.EntityFrameworkCore;

namespace Dspc.Application.Modules.Documents;

public sealed record UploadDocumentRequest(string Type, Guid? PoLineId, string? LotNumber, string? HeatNumber, string DocumentNumber, DateOnly? IssuedOn);
public sealed record VerifyDocumentRequest(string Status, string? Comment);
public sealed record DocumentDownload(Stream Content, string ContentType, string FileName);

public sealed class UploadDocumentRequestValidator : AbstractValidator<UploadDocumentRequest>
{
    public UploadDocumentRequestValidator()
    {
        RuleFor(x => x.Type).Must(t => Enum.TryParse<DocumentType>(t, true, out _)).WithMessage("Unknown document type.");
        RuleFor(x => x.DocumentNumber).NotEmpty().MaximumLength(64).Matches("^[A-Za-z0-9\\-_/. ]+$");
        RuleFor(x => x.LotNumber).MaximumLength(64).Matches("^[A-Za-z0-9\\-_/]+$").When(x => !string.IsNullOrEmpty(x.LotNumber));
        RuleFor(x => x).Must(x => x.PoLineId.HasValue || !string.IsNullOrEmpty(x.LotNumber)).WithMessage("Document must reference a purchase order line or a lot.");
    }
}

public sealed class VerifyDocumentRequestValidator : AbstractValidator<VerifyDocumentRequest>
{
    public VerifyDocumentRequestValidator()
    {
        RuleFor(x => x.Status).Must(s => s is "Accepted" or "Rejected" or "RequiresCompletion" or "Verifying").WithMessage("Status must be Accepted, Rejected, RequiresCompletion or Verifying.");
        RuleFor(x => x.Comment).MaximumLength(1000);
    }
}

/// <summary>Deterministic document path: validate → scan → hash → store → metadata → re-score the line. No AI on the critical path.</summary>
public sealed class DocumentService(IAppDbContext db, ISupplierScope scope, ICurrentUser user, IDemoClock clock, IEventPublisher events, IAuditWriter audit, IDocumentStorage storage, IFileScanner scanner, RiskAssessmentService risk)
{
    public const long MaxBytes = 10 * 1024 * 1024;
    private static readonly Dictionary<string, string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase) { [".pdf"] = "application/pdf", [".png"] = "image/png", [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg" };

    public async Task<DocumentSummary> UploadAsync(UploadDocumentRequest req, string fileName, long length, Stream content, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        var safeName = SanitizeFileName(fileName);
        var ext = Path.GetExtension(safeName);
        if (!AllowedExtensions.TryGetValue(ext, out var contentType)) errors["file"] = ["Allowed file types: pdf, png, jpg."];
        if (length <= 0 || length > MaxBytes) errors["file"] = [$"File size must be between 1 byte and {MaxBytes / 1024 / 1024} MB."];
        if (errors.Count > 0) throw new ValidationException(errors);

        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();
        if (!MagicMatches(bytes, ext)) throw new ValidationException(new Dictionary<string, string[]> { ["file"] = ["File content does not match its extension."] });
        var (clean, reason) = await scanner.ScanAsync(bytes, safeName, ct);
        if (!clean) throw new UnprocessableException($"File rejected by scanner: {reason}");

        PurchaseOrderLine? line = null;
        MaterialLot? lot = null;
        Supplier supplier;
        if (req.PoLineId is { } lid)
        {
            line = await scope.Apply(db.PurchaseOrderLines).Include(l => l.Part).Include(l => l.PurchaseOrder).ThenInclude(p => p!.Supplier).FirstOrDefaultAsync(l => l.Id == lid, ct)
                ?? throw new NotFoundException("PurchaseOrderLine", lid.ToString());
            supplier = line.PurchaseOrder!.Supplier!;
        }
        else
        {
            lot = await scope.Apply(db.MaterialLots).Include(l => l.Supplier).FirstOrDefaultAsync(l => l.LotNumber == req.LotNumber, ct) ?? throw new NotFoundException("MaterialLot", req.LotNumber ?? "");
            supplier = lot.Supplier!;
        }
        if (req.LotNumber is not null && line is not null && line.LotNumber is not null && !string.Equals(line.LotNumber, req.LotNumber, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException(new Dictionary<string, string[]> { ["lotNumber"] = [$"Lot number '{req.LotNumber}' does not match the line's lot '{line.LotNumber}'."] });

        var now = clock.UtcNow;
        var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var id = Guid.NewGuid();
        var key = $"documents/{supplier.Code}/{id:N}{ext.ToLowerInvariant()}";
        await storage.PutAsync(key, new MemoryStream(bytes), contentType!, ct);

        var type = Enum.Parse<DocumentType>(req.Type, true);
        var existing = line is not null
            ? await db.QualityDocuments.Where(d => d.PurchaseOrderLineId == line.Id && d.Type == type).OrderByDescending(d => d.Version).FirstOrDefaultAsync(ct)
            : await db.QualityDocuments.Where(d => d.MaterialLotId == lot!.Id && d.Type == type).OrderByDescending(d => d.Version).FirstOrDefaultAsync(ct);
        var doc = new QualityDocument
        {
            Id = id, Type = type, Status = DocumentStatus.Pending, DocumentNumber = req.DocumentNumber.Trim(), FileName = safeName, ContentType = contentType!, SizeBytes = bytes.Length, Sha256 = sha, StorageKey = key,
            IssuedOn = req.IssuedOn, PurchaseOrderLineId = line?.Id, MaterialLotId = lot?.Id ?? (line?.LotNumber is { } ln ? await db.MaterialLots.Where(l => l.LotNumber == ln).Select(l => (Guid?)l.Id).FirstOrDefaultAsync(ct) : null),
            SupplierId = supplier.Id, LotNumber = req.LotNumber ?? line?.LotNumber ?? lot?.LotNumber, HeatNumber = req.HeatNumber ?? line?.HeatNumber ?? lot?.HeatNumber,
            UploadedBy = user.Username, UploadedAt = now, Version = (existing?.Version ?? 0) + 1, CreatedAt = now, UpdatedAt = now
        };
        if (existing is not null && existing.Status is DocumentStatus.Missing or DocumentStatus.RequiresCompletion or DocumentStatus.Rejected)
        {
            // a re-upload supersedes the previous problem document
            db.QualityDocuments.Remove(existing);
        }
        db.QualityDocuments.Add(doc);
        events.Publish(new QualityDocumentUploaded(now, user.CorrelationId, doc.Id, doc.Type.ToString(), line?.PurchaseOrder?.Code, line?.Id, doc.LotNumber, supplier.Code));
        audit.Write("Document.Upload", "QualityDocument", doc.DocumentNumber, doc.Id, null, new { doc.Type, doc.FileName, doc.SizeBytes, doc.Sha256, PoLineId = line?.Id, doc.LotNumber });
        await db.SaveChangesAsync(ct);
        if (line is not null) { await risk.AssessAndPersistAsync(line, "DocumentUploaded", null, ct); await db.SaveChangesAsync(ct); }
        return PurchaseOrderQueries.ToDocumentSummary(doc);
    }

    public async Task<ListResult<DocumentSummary>> ListAsync(Guid? poLineId, string? lotNumber, CancellationToken ct)
    {
        var q = scope.Apply(db.QualityDocuments.AsNoTracking());
        if (poLineId is { } id) q = q.Where(d => d.PurchaseOrderLineId == id);
        if (!string.IsNullOrWhiteSpace(lotNumber)) q = q.Where(d => d.LotNumber == lotNumber || d.MaterialLot!.LotNumber == lotNumber);
        var list = await q.OrderByDescending(d => d.UploadedAt).Take(500).ToListAsync(ct);
        return ListResult.Of(list.Select(PurchaseOrderQueries.ToDocumentSummary).ToList());
    }

    public async Task<DocumentDownload?> DownloadAsync(Guid id, CancellationToken ct)
    {
        var doc = await scope.Apply(db.QualityDocuments.AsNoTracking()).FirstOrDefaultAsync(d => d.Id == id, ct) ?? throw new NotFoundException("QualityDocument", id.ToString());
        if (string.IsNullOrEmpty(doc.StorageKey)) return null;
        var stream = await storage.GetAsync(doc.StorageKey, ct);
        audit.Write("Document.Download", "QualityDocument", doc.DocumentNumber, doc.Id, null, new { doc.FileName });
        await db.SaveChangesAsync(ct);
        return stream is null ? null : new DocumentDownload(stream, doc.ContentType, doc.FileName);
    }

    public async Task<DocumentSummary> VerifyAsync(Guid id, VerifyDocumentRequest req, CancellationToken ct)
    {
        var doc = await db.QualityDocuments.Include(d => d.PurchaseOrderLine).ThenInclude(l => l!.Part).Include(d => d.PurchaseOrderLine).ThenInclude(l => l!.PurchaseOrder).ThenInclude(p => p!.Supplier)
            .FirstOrDefaultAsync(d => d.Id == id, ct) ?? throw new NotFoundException("QualityDocument", id.ToString());
        var before = new { Status = doc.Status.ToString(), doc.VerificationComment };
        doc.Status = Enum.Parse<DocumentStatus>(req.Status, true);
        doc.VerifiedBy = user.Username; doc.VerifiedAt = clock.UtcNow; doc.VerificationComment = req.Comment; doc.UpdatedAt = clock.UtcNow;
        events.Publish(new QualityDocumentVerified(clock.UtcNow, user.CorrelationId, doc.Id, doc.Status.ToString(), doc.PurchaseOrderLineId, doc.LotNumber));
        audit.Write("Document.Verify", "QualityDocument", doc.DocumentNumber, doc.Id, before, new { Status = doc.Status.ToString(), doc.VerificationComment });
        if (doc.Status == DocumentStatus.Rejected)
        {
            var n = new Notification { Id = Guid.NewGuid(), TargetRole = doc.PurchaseOrderLine is null ? Role.QualityInspector : Role.InboundCoordinator, Severity = NotificationSeverity.Warning, TitleKey = "notifications.documentRejected.title", MessageKey = "notifications.documentRejected.message",
                ParamsJson = Json.Serialize(new { documentNumber = doc.DocumentNumber, comment = req.Comment ?? "", eventName = EventNames.QualityDocumentVerified }), Route = doc.PurchaseOrderLine?.PurchaseOrder is { } po ? $"/supply/orders/{po.Code}" : $"/trace/lots/{doc.LotNumber}", CreatedAt = clock.UtcNow, UpdatedAt = clock.UtcNow };
            db.Notifications.Add(n);
        }
        await db.SaveChangesAsync(ct);
        if (doc.PurchaseOrderLine is { } line) { await risk.AssessAndPersistAsync(line, "DocumentVerified", null, ct); await db.SaveChangesAsync(ct); }
        return PurchaseOrderQueries.ToDocumentSummary(doc);
    }

    public static string SanitizeFileName(string name)
    {
        var n = Path.GetFileName(name.Replace('\\', '/')).Trim();
        var invalid = Path.GetInvalidFileNameChars().Concat(['\0', ':', '*', '?', '"', '<', '>', '|']).ToHashSet();
        var sb = new System.Text.StringBuilder();
        foreach (var c in n) sb.Append(invalid.Contains(c) || char.IsControl(c) ? '_' : c);
        var result = sb.ToString();
        if (result.Length > 120) result = result[^120..];
        return string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(result)) ? "document" + Path.GetExtension(result) : result;
    }

    private static bool MagicMatches(byte[] b, string ext) => ext.ToLowerInvariant() switch
    {
        ".pdf" => b.Length > 4 && b[0] == 0x25 && b[1] == 0x50 && b[2] == 0x44 && b[3] == 0x46,
        ".png" => b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47,
        ".jpg" or ".jpeg" => b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF,
        _ => false
    };
}
