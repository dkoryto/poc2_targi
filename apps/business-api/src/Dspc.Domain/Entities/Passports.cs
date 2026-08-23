using Dspc.Domain.Common;

namespace Dspc.Domain.Entities;

public class Passport : VersionedEntity
{
    public Guid ProductSerialId { get; set; }
    public ProductSerial? ProductSerial { get; set; }
    public Guid PassportTemplateId { get; set; }
    public PassportTemplate? Template { get; set; }
    public PassportStatus Status { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? InvalidationReason { get; set; }
    public DateTime? InvalidatedAt { get; set; }
    public int CurrentVersion { get; set; }
    /// <summary>JSON array of deviation/approval register entries.</summary>
    public string DeviationsJson { get; set; } = "[]";
    public ICollection<PassportVersion> Versions { get; set; } = new List<PassportVersion>();
}

public class PassportVersion : Entity
{
    public Guid PassportId { get; set; }
    public Passport? Passport { get; set; }
    public int Version { get; set; }
    public PassportVersionStatus Status { get; set; }
    public string StorageKey { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long FileSize { get; set; }
    public string GeneratedBy { get; set; } = "";
    public DateTime GeneratedAt { get; set; }
    public string? SnapshotJson { get; set; }
}
