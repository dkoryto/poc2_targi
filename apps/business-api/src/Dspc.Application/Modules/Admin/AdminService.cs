using System.Diagnostics;
using Dspc.Application.Abstractions;
using Dspc.Application.Modules.Planning;
using Dspc.Application.Modules.Risk;
using Dspc.Domain.Risk;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dspc.Application.Modules.Admin;

public sealed record WeightRow(string Code, double Weight);
public sealed record AdminSettingsDto(IReadOnlyList<WeightRow> RiskWeights, double RiskWeightsSum, int RiskNotifyThreshold, IReadOnlyList<WeightRow> ObjectiveWeights, int SolverTimeLimitMs, int HorizonWeeks, bool DemoMode, bool LocalAiEnabled, string StorageProvider, string TimeZone);
public sealed record AdminStatusDto(IReadOnlyList<ServiceStatus> Services, IReadOnlyList<RecentError> RecentErrors, DateTime ServerTime, string Version);

public sealed class LocalAiOptions
{
    public const string Section = "LocalAi";
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "http://local-ai:8000/v1";
    public string? Model { get; set; }
    /// <summary>Answer from the bundled deterministic fixture instead of calling the model (stable stand demo).</summary>
    public bool Simulator { get; set; }
}

public sealed class AdminService(IAppDbContext db, IOptions<RiskOptions> risk, IOptions<PlanningOptions> planning, IOptions<Identity.DemoOptions> demo, IOptions<LocalAiOptions> ai, IDocumentStorage storage, IRecentErrors errors, IExternalServiceProbe probe, IDemoClock clock)
{
    public AdminSettingsDto Settings()
    {
        var rw = risk.Value.Weights;
        var ow = planning.Value.Weights;
        // Canonical factor codes, see docs/architecture/risk-model.md — the UI localises them by code.
        IReadOnlyList<WeightRow> riskRows =
        [
            new("ETA_DEVIATION", rw.EtaDeviation),
            new("CRITICALITY", rw.Criticality),
            new("NO_ALTERNATIVE", rw.NoAlternative),
            new("DOC_COMPLETENESS", rw.DocCompleteness),
            new("SUPPLIER_RELIABILITY", rw.SupplierReliability),
            new("COVERAGE", rw.Coverage),
            new("LOGISTICS_EVENTS", rw.LogisticsEvents),
        ];
        IReadOnlyList<WeightRow> objectiveRows =
        [
            new("LATENESS_PER_DAY_PER_PRIORITY", ow.LatenessPerDayPerPriority),
            new("SHORTAGE_PER_UNIT", ow.ShortagePerUnit),
            new("DOWNTIME_PER_HOUR", ow.DowntimePerHour),
            new("DELIVERY_BREACH_PER_ORDER", ow.DeliveryBreachPerOrder),
            new("CHANGE_PER_MOVED_OPERATION", ow.ChangePerMovedOperation),
            new("CHANGEOVER_PER_SWITCH", ow.ChangeoverPerSwitch),
        ];
        return new(riskRows, rw.Sum, risk.Value.NotifyThreshold, objectiveRows, planning.Value.TimeLimitMs, planning.Value.HorizonWeeks, demo.Value.Enabled, ai.Value.Enabled, storage.Provider, clock.SiteTimeZone.Id);
    }

    public async Task<AdminStatusDto> StatusAsync(CancellationToken ct)
    {
        var services = new List<ServiceStatus>();
        var sw = Stopwatch.StartNew();
        try { await db.Sites.AsNoTracking().AnyAsync(ct); services.Add(new ServiceStatus("postgres", "up", sw.ElapsedMilliseconds, null)); }
        catch (Exception ex) { services.Add(new ServiceStatus("postgres", "down", sw.ElapsedMilliseconds, ex.Message)); }
        sw.Restart();
        try { var ok = await storage.HealthCheckAsync(ct); services.Add(new ServiceStatus(storage.Provider == "Minio" ? "minio" : "storage", ok ? "up" : "down", sw.ElapsedMilliseconds, storage.Provider)); }
        catch (Exception ex) { services.Add(new ServiceStatus("storage", "down", sw.ElapsedMilliseconds, ex.Message)); }
        services.Add(await probe.ProbePlanningEngineAsync(ct));
        services.Add(ai.Value.Enabled ? await probe.ProbeLocalAiAsync(ct) : new ServiceStatus("local-ai", "disabled", null, "LocalAi:Enabled=false"));
        return new AdminStatusDto(services, errors.List(), clock.UtcNow, typeof(AdminService).Assembly.GetName().Version?.ToString() ?? "1.0.0");
    }
}
