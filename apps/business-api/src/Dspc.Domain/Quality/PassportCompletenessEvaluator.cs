using Dspc.Domain.Common;

namespace Dspc.Domain.Quality;

/// <summary>One key component consumed into the serial, flattened for the completeness rules and the PDF.</summary>
public sealed record PassportComponentFacts(
    string PartCode,
    string? PartName,
    string LotNumber,
    string? HeatNumber,
    string SupplierCode,
    string? SupplierName,
    string? Country,
    MaterialLotStatus LotStatus,
    string? CertificateSha256,
    string? CertificateNumber,
    bool LotInspectionPassed);

public sealed record PassportInspectionFacts(string Code, InspectionResult Result, DateTime InspectedAt, string InspectedBy, string? Notes);

public sealed record PassportDeviationFacts(string? Code, string Title, string? ApprovedBy, DateTime? ApprovedAt);

/// <summary>Everything the completeness rules need, read from the domain model — no EF, no DTOs.</summary>
public sealed record PassportFacts(
    string SerialNumber,
    string? ProductCode,
    string? ProductName,
    string? OrderCode,
    string? BomVersion,
    PassportStatus Status,
    string? ApprovedBy,
    DateTime? ApprovedAt,
    IReadOnlyList<PassportComponentFacts> Components,
    IReadOnlyList<PassportInspectionFacts> Inspections,
    IReadOnlyList<PassportDeviationFacts> Deviations,
    bool DeviationsRegisterReviewed = true);

public sealed record RequirementResult(string Code, bool Satisfied, string? Evidence, bool Mandatory);

public sealed record MissingRequirement(string Code, string LabelKey, IReadOnlyDictionary<string, object> Params);

public sealed record CompletenessResult(bool Complete, IReadOnlyList<RequirementResult> Requirements, IReadOnlyList<MissingRequirement> Missing);

/// <summary>
/// Rules of the demonstrator passport template <c>DQP-01</c>. Pure and deterministic: the same facts always produce the
/// same verdict. A passport may only be generated when every mandatory requirement is satisfied — see
/// <c>docs/adr/0006-passport-generation-and-invalidation.md</c>. The mapping of these rows onto a specific contract or
/// standard is out of scope for the demonstrator.
/// </summary>
public static class PassportCompletenessEvaluator
{
    public const string TemplateCode = "DQP-01";

    public static class Requirements
    {
        public const string ProductData = "PRODUCT_DATA";
        public const string OrderRef = "ORDER_REF";
        public const string BomVersion = "BOM_VERSION";
        public const string KeyComponentLots = "KEY_COMPONENT_LOTS";
        public const string SupplierOrigin = "SUPPLIER_ORIGIN";
        public const string QcStatus = "QC_STATUS";
        public const string CertificatesWithHash = "CERTIFICATES_WITH_HASH";
        public const string InspectionResults = "INSPECTION_RESULTS";
        public const string Deviations = "DEVIATIONS";
        public const string Approval = "APPROVAL";
    }

    /// <summary>Requirement codes in template order, with their mandatory flag.</summary>
    public static readonly IReadOnlyList<(string Code, bool Mandatory)> Template =
    [
        (Requirements.ProductData, true),
        (Requirements.OrderRef, true),
        (Requirements.BomVersion, true),
        (Requirements.KeyComponentLots, true),
        (Requirements.SupplierOrigin, true),
        (Requirements.QcStatus, true),
        (Requirements.CertificatesWithHash, true),
        (Requirements.InspectionResults, true),
        (Requirements.Deviations, false),
        (Requirements.Approval, true)
    ];

    public static CompletenessResult Evaluate(PassportFacts f, IReadOnlyList<(string Code, bool Mandatory)>? template = null)
    {
        template ??= Template;
        var results = new List<RequirementResult>();
        var missing = new List<MissingRequirement>();

        foreach (var (code, mandatory) in template)
        {
            var (satisfied, evidence, parameters) = Check(code, f);
            results.Add(new RequirementResult(code, satisfied, evidence, mandatory));
            if (!satisfied && mandatory) missing.Add(new MissingRequirement(code, $"passports.missing.{code}", parameters));
        }

        return new CompletenessResult(missing.Count == 0, results, missing);
    }

    private static (bool Satisfied, string? Evidence, IReadOnlyDictionary<string, object> Params) Check(string code, PassportFacts f)
    {
        var none = (IReadOnlyDictionary<string, object>)new Dictionary<string, object>();
        switch (code)
        {
            case Requirements.ProductData:
                return (!string.IsNullOrWhiteSpace(f.SerialNumber) && !string.IsNullOrWhiteSpace(f.ProductCode),
                    string.IsNullOrWhiteSpace(f.ProductCode) ? null : $"{f.ProductCode} · {f.SerialNumber}", none);

            case Requirements.OrderRef:
                return (!string.IsNullOrWhiteSpace(f.OrderCode), f.OrderCode, none);

            case Requirements.BomVersion:
                return (!string.IsNullOrWhiteSpace(f.BomVersion), f.BomVersion is null ? null : $"BOM {f.BomVersion}", none);

            case Requirements.KeyComponentLots:
                return (f.Components.Count > 0, f.Components.Count > 0 ? $"{f.Components.Count}" : null,
                    Dict(("componentCount", f.Components.Count)));

            case Requirements.SupplierOrigin:
            {
                var unknown = f.Components.Where(c => string.IsNullOrWhiteSpace(c.SupplierCode) || string.IsNullOrWhiteSpace(c.Country)).Select(c => c.LotNumber).ToList();
                return (f.Components.Count > 0 && unknown.Count == 0,
                    unknown.Count == 0 && f.Components.Count > 0 ? string.Join(", ", f.Components.Select(c => c.Country).Distinct().Order()) : null,
                    Dict(("lots", string.Join(", ", unknown))));
            }

            case Requirements.QcStatus:
            {
                var notReleased = f.Components.Where(c => c.LotStatus is not (MaterialLotStatus.Accepted or MaterialLotStatus.ConditionallyReleased)).ToList();
                return (f.Components.Count > 0 && notReleased.Count == 0,
                    f.Components.Count > 0 && notReleased.Count == 0 ? "Accepted" : null,
                    Dict(("lots", string.Join(", ", notReleased.Select(c => $"{c.LotNumber} ({c.LotStatus})")))));
            }

            case Requirements.CertificatesWithHash:
            {
                var withoutCert = f.Components.Where(c => string.IsNullOrWhiteSpace(c.CertificateSha256)).Select(c => c.LotNumber).ToList();
                return (f.Components.Count > 0 && withoutCert.Count == 0,
                    f.Components.Count > 0 && withoutCert.Count == 0 ? $"{f.Components.Count}" : null,
                    Dict(("lots", string.Join(", ", withoutCert))));
            }

            case Requirements.InspectionResults:
            {
                var passed = f.Inspections.Any(i => i.Result is InspectionResult.Passed or InspectionResult.Conditional);
                var failed = f.Inspections.Any(i => i.Result == InspectionResult.Failed);
                return (passed && !failed,
                    passed && !failed ? f.Inspections.First(i => i.Result != InspectionResult.Failed).Code : null,
                    Dict(("failed", failed)));
            }

            case Requirements.Deviations:
                // optional row: satisfied when the register has been reviewed (open deviations must carry an approval)
                return (f.Deviations.All(d => d.ApprovedBy is not null) && f.DeviationsRegisterReviewed,
                    f.Deviations.Count == 0 ? "0" : $"{f.Deviations.Count}",
                    Dict(("open", f.Deviations.Count(d => d.ApprovedBy is null))));

            case Requirements.Approval:
                return (!string.IsNullOrWhiteSpace(f.ApprovedBy) && f.ApprovedAt is not null && f.Status is not PassportStatus.Draft,
                    f.ApprovedBy, none);

            default:
                return (true, null, none);
        }
    }

    private static IReadOnlyDictionary<string, object> Dict(params (string Key, object Value)[] pairs) =>
        pairs.ToDictionary(p => p.Key, p => p.Value);
}
