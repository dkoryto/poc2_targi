using Dspc.Application.Abstractions;
using Dspc.Application.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dspc.Application.Modules.Sites;

public sealed record SiteDto(string Code, string Name, string City, string Country, double Lat, double Lon,
    string TimeZone, string ProfileKey, string FeaturedScenarioKey, bool IsDefault);

/// <summary>
/// Resolves the plant a request works on: the explicit <c>?siteCode=</c> when given, otherwise the caller's
/// default plant. Unknown plant → 404, a plant outside the caller's reach → 403. Supplier users may only
/// look at plants they actually deliver to. See <c>docs/architecture/multi-site.md</c>.
/// </summary>
public interface ISiteContext
{
    Task<Guid> ResolveAsync(string? siteCode, CancellationToken ct);
    Task<Site> ResolveSiteAsync(string? siteCode, CancellationToken ct);
    Task<IReadOnlyList<Site>> AvailableAsync(CancellationToken ct);
    Task<Site> DefaultAsync(CancellationToken ct);
}

public sealed class SiteContext(IAppDbContext db, ICurrentUser user) : ISiteContext
{
    private readonly Dictionary<string, Guid> _resolved = new(StringComparer.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<Site>> AvailableAsync(CancellationToken ct)
    {
        var all = await db.Sites.AsNoTracking().OrderBy(s => s.Sequence).ThenBy(s => s.Code).ToListAsync(ct);
        if (!user.IsSupplier || user.SupplierId is not { } supplierId) return all;
        // a supplier only sees the plants it actually ships to
        var supplied = await db.PurchaseOrders.AsNoTracking().Where(p => p.SupplierId == supplierId)
            .Select(p => p.SiteId).Distinct().ToListAsync(ct);
        var visible = all.Where(s => supplied.Contains(s.Id)).ToList();
        return visible.Count > 0 ? visible : all.Where(s => s.IsDefault).ToList();
    }

    public async Task<Site> DefaultAsync(CancellationToken ct)
    {
        var available = await AvailableAsync(ct);
        if (user.SiteId is { } sid && available.FirstOrDefault(s => s.Id == sid) is { } own) return own;
        return available.FirstOrDefault(s => s.IsDefault) ?? available.FirstOrDefault()
            ?? throw new NotFoundException("Site", "default");
    }

    public async Task<Site> ResolveSiteAsync(string? siteCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(siteCode)) return await DefaultAsync(ct);
        var site = await db.Sites.AsNoTracking().FirstOrDefaultAsync(s => s.Code == siteCode, ct)
            ?? throw new NotFoundException("Site", siteCode);
        var available = await AvailableAsync(ct);
        if (available.All(s => s.Id != site.Id)) throw new ForbiddenException($"Site '{siteCode}' is not available to this user.");
        return site;
    }

    public async Task<Guid> ResolveAsync(string? siteCode, CancellationToken ct)
    {
        var key = siteCode ?? "";
        if (_resolved.TryGetValue(key, out var cached)) return cached;
        var id = (await ResolveSiteAsync(siteCode, ct)).Id;
        _resolved[key] = id;
        return id;
    }
}

public sealed class SiteQueries(ISiteContext context)
{
    public async Task<IReadOnlyList<SiteDto>> ListAsync(CancellationToken ct) =>
        (await context.AvailableAsync(ct)).Select(s => new SiteDto(s.Code, s.Name, s.City, s.Country, s.Latitude, s.Longitude,
            s.TimeZone, s.ProfileKey, s.FeaturedScenarioKey, s.IsDefault)).ToList();
}
