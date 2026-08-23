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
    /// <summary>i18n key describing what the plant makes, e.g. "site.profile.assembly".</summary>
    public string ProfileKey { get; set; } = "";
    /// <summary>Preset key of the scenario this plant is meant to demonstrate on the stand.</summary>
    public string FeaturedScenarioKey { get; set; } = "";
    /// <summary>The plant the app opens on when the user has no explicit choice.</summary>
    public bool IsDefault { get; set; }
    public int Sequence { get; set; }
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
