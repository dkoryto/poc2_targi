using Dspc.Application.Abstractions;
using Dspc.Application.Modules.Identity;
using Microsoft.Extensions.Options;

namespace Dspc.Infrastructure.Services;

public sealed class DemoClock : IDemoClock
{
    public const string SiteTz = "Europe/Warsaw";
    private readonly TimeZoneInfo _tz = TimeZoneInfo.FindSystemTimeZoneById(SiteTz);
    private readonly DateOnly? _anchor;

    public DemoClock(IOptions<DemoOptions> options)
    {
        if (!string.IsNullOrWhiteSpace(options.Value.ClockAnchor) && DateOnly.TryParse(options.Value.ClockAnchor, out var d)) _anchor = d;
    }

    public TimeZoneInfo SiteTimeZone => _tz;
    public DateTime UtcNow => DateTime.UtcNow;
    public DateOnly Today => DateOnly.FromDateTime(ToSiteLocal(UtcNow));

    public DateOnly T0Date
    {
        get
        {
            var reference = _anchor ?? Today;
            var diff = ((int)reference.DayOfWeek + 6) % 7; // Monday = 0
            return reference.AddDays(-diff);
        }
    }

    public DateTime T0Utc => FromSiteLocal(T0Date.ToDateTime(new TimeOnly(6, 0), DateTimeKind.Unspecified));

    public DateTime ToSiteLocal(DateTime utc)
    {
        var u = utc.Kind == DateTimeKind.Utc ? utc : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(u, _tz), DateTimeKind.Unspecified);
    }

    public DateTime FromSiteLocal(DateTime local)
    {
        var l = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(l, _tz);
    }
}
