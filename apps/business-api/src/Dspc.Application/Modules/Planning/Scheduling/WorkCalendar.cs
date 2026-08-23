namespace Dspc.Application.Modules.Planning.Scheduling;

/// <summary>Working-time arithmetic per work center: Mon–Fri, shift window starting 06:00, hoursPerDay × capacityFactor, per-date overrides.</summary>
public sealed class WorkCalendar
{
    private readonly Dictionary<string, PlanWorkCenter> _centers;
    private const int ShiftStartHour = 6;

    public WorkCalendar(IEnumerable<PlanWorkCenter> centers)
    {
        _centers = centers.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);
    }

    public double HoursOn(string wc, DateOnly date)
    {
        if (!_centers.TryGetValue(wc, out var c)) throw new InvalidOperationException($"Unknown work center {wc}");
        var ovr = c.Calendar.FirstOrDefault(o => o.Date == date);
        if (ovr is not null) return Math.Max(0, ovr.AvailableHours * c.CapacityFactor);
        if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return 0;
        return Math.Max(0, c.HoursPerDay * c.CapacityFactor);
    }

    public (DateTime Start, DateTime End) WindowOn(string wc, DateOnly date)
    {
        var start = date.ToDateTime(new TimeOnly(ShiftStartHour, 0), DateTimeKind.Unspecified);
        return (start, start.AddHours(HoursOn(wc, date)));
    }

    /// <summary>If t is inside a working window returns t, otherwise the start of the next window.</summary>
    public DateTime NextWorkingTime(string wc, DateTime t)
    {
        var date = DateOnly.FromDateTime(t);
        for (var i = 0; i < 400; i++)
        {
            var (s, e) = WindowOn(wc, date);
            if (e > s)
            {
                if (t < s) return s;
                if (t < e) return t;
            }
            date = date.AddDays(1);
            t = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        }
        throw new InvalidOperationException($"No working time found for {wc} after {t}");
    }

    /// <summary>Adds working hours starting at a working time; returns the end instant (inside a window).</summary>
    public DateTime AddWorkingHours(string wc, DateTime start, double hours)
    {
        var t = NextWorkingTime(wc, start);
        var remaining = hours;
        for (var i = 0; i < 400 && remaining > 1e-9; i++)
        {
            var date = DateOnly.FromDateTime(t);
            var (s, e) = WindowOn(wc, date);
            if (e <= s || t >= e)
            {
                t = NextWorkingTime(wc, date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified));
                continue;
            }
            if (t < s) t = s;
            var avail = (e - t).TotalHours;
            if (avail >= remaining) return t.AddHours(remaining);
            remaining -= avail;
            t = NextWorkingTime(wc, date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified));
        }
        return t;
    }

    /// <summary>Working hours between two instants.</summary>
    public double WorkingHoursBetween(string wc, DateTime a, DateTime b)
    {
        if (b <= a) return 0;
        double total = 0;
        var date = DateOnly.FromDateTime(a);
        var last = DateOnly.FromDateTime(b);
        while (date <= last)
        {
            var (s, e) = WindowOn(wc, date);
            var os = s > a ? s : a;
            var oe = e < b ? e : b;
            if (oe > os) total += (oe - os).TotalHours;
            date = date.AddDays(1);
        }
        return Math.Round(total, 4);
    }
}
