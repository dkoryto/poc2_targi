using Dspc.Application.Abstractions;
using Dspc.Application.Common;
using Dspc.Application.Modules.Identity;
using Dspc.Domain.Common;
using Dspc.Domain.Events;
using Microsoft.Extensions.Options;

namespace Dspc.Application.Modules.Demo;

public sealed record DemoStatusDto(bool DemoMode, string? SeedVersion, DateTime? SeededAt, long? LastResetMs, DateTime T0, string TimeZone, DateTime ServerTime);
public sealed record DemoResetResultDto(long DurationMs, string SeedVersion, IReadOnlyDictionary<string, int> Counts);
public sealed record DemoStepDto(int Step, string TitleKey, string DescriptionKey, string Route, string Action, string? Role, string Scenario);

public sealed class DemoService(IDemoSeeder seeder, IOptions<DemoOptions> options, IDemoClock clock, IEventPublisher events, IAuditWriter audit, ICurrentUser user, IAppDbContext db)
{
    public bool Enabled => options.Value.Enabled;

    private void EnsureEnabled() { if (!Enabled) throw new NotFoundException("Endpoint", "demo"); }

    public async Task<DemoResetResultDto> ResetAsync(CancellationToken ct)
    {
        EnsureEnabled();
        var result = await seeder.ResetAsync(ct);
        audit.Write("Demo.Reset", "Demo", result.SeedVersion, null, null, new { result.DurationMs, result.Counts }, AuditSource.Demo);
        events.Publish(new DemoReset(clock.UtcNow, user.CorrelationId, result.DurationMs, result.SeedVersion));
        await db.SaveChangesAsync(ct);
        return new DemoResetResultDto(result.DurationMs, result.SeedVersion, result.Counts);
    }

    public DemoStatusDto Status()
    {
        var last = seeder.LastResult;
        return new DemoStatusDto(Enabled, last?.SeedVersion, last?.SeededAt, last?.DurationMs, clock.T0Utc, clock.SiteTimeZone.Id, clock.UtcNow);
    }

    public IReadOnlyList<DemoStepDto> Script()
    {
        EnsureEnabled();
        // i18n keys demo.script.N.title / .desc exist in the web bundle (N = 1..9); routes match apps/web/src/App.tsx.
        return new List<DemoStepDto>
        {
            new(1, "demo.script.1.title", "demo.script.1.desc", "/", "observe", "DemoPresenter", "main"),
            new(2, "demo.script.2.title", "demo.script.2.desc", "/supply/orders/PO-2026-0007", "eta:ACT-40:+10", "SupplierUser", "main"),
            new(3, "demo.script.3.title", "demo.script.3.desc", "/", "observe", "DemoPresenter", "main"),
            new(4, "demo.script.4.title", "demo.script.4.desc", "/planning?preset=ACT40_DELAY", "run-scenario", "ProductionPlanner", "main"),
            new(5, "demo.script.5.title", "demo.script.5.desc", "/planning", "compare", "ProductionPlanner", "main"),
            new(6, "demo.script.6.title", "demo.script.6.desc", "/trace/serials/PMV-2026-0007", "trace-back", "QualityInspector", "main"),
            new(7, "demo.script.7.title", "demo.script.7.desc", "/passports/PMV-2026-0007", "generate-pdf", "QualityInspector", "main"),
            new(8, "demo.script.8.title", "demo.script.8.desc", "/demo/summary", "observe", "DemoPresenter", "main"),
            new(9, "demo.script.9.title", "demo.script.9.desc", "/trace/lots/HTS-22-2608", "block-lot", "QualityInspector", "secondary"),
        };
    }
}
