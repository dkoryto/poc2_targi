using Dspc.Domain.Common;

namespace Dspc.Domain.Entities;

public class Organization : Entity
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Country { get; set; } = "PL";
}

public class Site : Entity
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Country { get; set; } = "PL";
    public string City { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string TimeZone { get; set; } = "Europe/Warsaw";
    public Guid OrganizationId { get; set; }
}

public class User : VersionedEntity
{
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public Role Role { get; set; }
    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public Guid SiteId { get; set; }
    public string Locale { get; set; } = "pl";
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }
}
