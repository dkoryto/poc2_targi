using Dspc.Application.Modules.Quality;

namespace Dspc.Application.Modules.Passports;

public sealed record PassportRequirementDto(string Code, bool Satisfied, string? Evidence, bool Mandatory);

public sealed record MissingItemDto(string Code, string LabelKey, IReadOnlyDictionary<string, object> Params);

public sealed record PassportCompletenessDto(bool Complete, IReadOnlyList<MissingItemDto> Missing, IReadOnlyList<PassportRequirementDto> Requirements);

public sealed record PassportComponentDto(string PartCode, string? PartName, string LotNumber, string SupplierCode, string? SupplierName, string? Country, string? CertSha256, string LotStatus);

public sealed record PassportDeviationDto(string Id, string? Code, string Title, string Status, string? ApprovedBy, DateTime? ApprovedAt);

public sealed record PassportVersionDto(int Version, DateTime GeneratedAt, string GeneratedBy, string Sha256, long FileSize, string Status);

public sealed record PassportSummaryDto(
    string Serial, string ProductCode, string? ProductName, string OrderCode, string Status, string TemplateCode,
    bool Complete, int MissingCount, DateTime? UpdatedAt, int? LatestVersion,
    string SiteCode, string SiteName);

public sealed record PassportDto(
    string Serial, string ProductCode, string? ProductName, string OrderCode, string? BomVersion, string Status, string TemplateCode,
    PassportCompletenessDto Completeness, IReadOnlyList<PassportComponentDto> Components, IReadOnlyList<InspectionDto> Inspections,
    IReadOnlyList<PassportDeviationDto> Deviations, IReadOnlyList<PassportVersionDto> Versions,
    string? ApprovedBy, DateTime? ApprovedAt, DateTime? InvalidatedAt, string? InvalidationReason,
    // The passport QR links straight here, so the record must name its own plant: the reader may
    // arrive with a different plant selected, or none.
    string SiteCode = "", string SiteName = "", bool IsDemo = true);

public sealed record GeneratePassportResponse(int Version, string Sha256, string DownloadUrl, long FileSize, DateTime GeneratedAt);

public sealed record PassportPdfDownload(Stream Content, string FileName, string ContentType = "application/pdf");
