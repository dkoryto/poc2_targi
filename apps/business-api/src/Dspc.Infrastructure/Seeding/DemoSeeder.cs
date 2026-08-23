using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Dspc.Application.Abstractions;
using Dspc.Application.Common;
using Dspc.Application.Modules.Planning;
using Dspc.Application.Modules.Risk;
using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Dspc.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dspc.Infrastructure.Seeding;

public sealed class SeedOptions
{
    public const string Section = "Seed";
    /// <summary>Directory with the demo JSON files. Relative paths resolve against the content root; when unset the seeder walks up to find packages/demo-data.</summary>
    public string? Path { get; set; }

    /// <summary>
    /// Seed the demonstration content even when <c>Demo:Enabled</c> is false. Without this a
    /// production deployment migrates an empty database and nobody can log in, because the
    /// accounts themselves come from the seed.
    /// </summary>
    public bool Force { get; set; }

    /// <summary>Skip seeding entirely (used by tests that build their own fixture).</summary>
    public bool Skip { get; set; }

    /// <summary>
    /// Password for every seeded account. The built-in "demo" is fine on a closed stand, but a
    /// deployment reachable from the internet must set its own.
    /// </summary>
    public string? AccountPassword { get; set; }
}

/// <summary>Process-wide seed state (seeder itself is scoped).</summary>
public sealed class SeedState
{
    public SeedResult? Last { get; set; }
}

/// <summary>
/// Deterministic demo seeder. All ids are derived from business codes (GUID v5-style), all dates are offsets from T0
/// (Monday 06:00 of the demo week) so every reset recreates the same state. Baseline operations come from the engine
/// fixture <c>packages/demo-data/baseline.json</c> (T0 = 2026-09-07) shifted to the live T0.
/// </summary>
public sealed partial class DemoSeeder(
    AppDbContext db, IDemoClock clock, IPasswordHasher hasher, IHostEnvironment env, IOptions<SeedOptions> options,
    IEnumerable<ISeedPostProcessor> postProcessors, RiskAssessmentService risk, IPlanImpactEvaluator impact,
    SeedState state, ILogger<DemoSeeder> log) : IDemoSeeder
{
    private static readonly DateOnly FixtureT0 = new(2026, 9, 7);
    public const string SeedVersion = "2026.08-wave1";

    public SeedResult? LastResult => state.Last;

    public async Task<SeedResult> SeedIfEmptyAsync(CancellationToken ct)
    {
        if (await db.Sites.AnyAsync(ct))
        {
            // The database already holds demo data, but it may have been written by an older build (e.g. a
            // single-plant seed upgraded in place). Re-seed when the stored version differs, otherwise the
            // demo would silently run on stale data that no longer matches this build.
            var stored = await ReadSeedVersionAsync(ct);
            if (stored == SeedVersion)
            {
                state.Last ??= new SeedResult(0, SeedVersion, clock.UtcNow, new Dictionary<string, int>());
                return state.Last;
            }
            log.LogInformation("Seed version changed ({Stored} -> {Current}); re-seeding demo data", stored ?? "unknown", SeedVersion);
        }
        return await ResetAsync(ct);
    }

    /// <summary>Seed version recorded in <c>seed_metadata</c>, or null when never written (pre-multi-site databases).</summary>
    private async Task<string?> ReadSeedVersionAsync(CancellationToken ct)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT seed_version FROM seed_metadata WHERE id = 1";
        if (db.Database.CurrentTransaction is not null) cmd.Transaction = db.Database.CurrentTransaction.GetDbTransaction();
        var value = await cmd.ExecuteScalarAsync(ct);
        return value as string;
    }

    private async Task WriteSeedVersionAsync(CancellationToken ct)
    {
        const string sql = """
            INSERT INTO seed_metadata (id, seed_version, seeded_at) VALUES (1, @p0, @p1)
            ON CONFLICT (id) DO UPDATE SET seed_version = EXCLUDED.seed_version, seeded_at = EXCLUDED.seeded_at
            """;
        await db.Database.ExecuteSqlRawAsync(sql, new object[] { SeedVersion, clock.UtcNow }, ct);
    }

    public async Task<SeedResult> ResetAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var dir = ResolveDirectory();
        log.LogInformation("Seeding demo data from {Dir} (T0 = {T0})", dir, clock.T0Date);
        var counts = new Dictionary<string, int>();
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            await TruncateAllAsync(ct);
            db.ChangeTracker.Clear();
            await SeedAsync(dir, counts, ct);
            await WriteSeedVersionAsync(ct);
            await tx.CommitAsync(ct);
        }
        db.ChangeTracker.Clear();
        foreach (var p in postProcessors.OrderBy(p => p.Order))
        {
            try { await p.RunAsync(ct); }
            catch (Exception ex) { log.LogWarning(ex, "Seed post-processor {Type} failed", p.GetType().Name); }
        }
        var result = new SeedResult(sw.ElapsedMilliseconds, SeedVersion, clock.UtcNow, counts);
        state.Last = result;
        log.LogInformation("Seed complete in {Ms} ms: {Counts}", result.DurationMs, string.Join(", ", counts.Select(c => $"{c.Key}={c.Value}")));
        return result;
    }

    private string ResolveDirectory()
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(options.Value.Path))
            candidates.Add(System.IO.Path.IsPathRooted(options.Value.Path) ? options.Value.Path : System.IO.Path.Combine(env.ContentRootPath, options.Value.Path));
        foreach (var root in new[] { env.ContentRootPath, AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var d = new DirectoryInfo(root);
            for (var i = 0; i < 7 && d is not null; i++, d = d.Parent)
                candidates.Add(System.IO.Path.Combine(d.FullName, "packages", "demo-data"));
        }
        var found = candidates.FirstOrDefault(c => File.Exists(System.IO.Path.Combine(c, "meta.json")));
        return found ?? throw new DirectoryNotFoundException("Demo seed directory not found. Set Seed:Path to the packages/demo-data folder.");
    }

    private async Task TruncateAllAsync(CancellationToken ct)
    {
        var tables = db.Model.GetEntityTypes().Select(e => e.GetTableName()).Where(t => !string.IsNullOrEmpty(t)).Distinct().Select(t => $"\"{t}\"");
        await db.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE {string.Join(", ", tables)} RESTART IDENTITY CASCADE", ct);
    }

    // ---------- helpers ----------

    private static JsonNode Load(string dir, string file) => JsonNode.Parse(File.ReadAllText(System.IO.Path.Combine(dir, file))) ?? throw new InvalidDataException(file);
    private static string S(JsonNode? n, string key, string fallback = "") => n?[key]?.GetValue<string>() ?? fallback;
    private static string? SN(JsonNode? n, string key) => n?[key] is JsonValue v ? v.GetValue<string>() : null;
    private static int I(JsonNode? n, string key, int fallback = 0) => n?[key] is JsonValue v ? v.GetValue<int>() : fallback;
    private static double D(JsonNode? n, string key, double fallback = 0) => n?[key] is JsonValue v ? v.GetValue<double>() : fallback;
    private static decimal M(JsonNode? n, string key, decimal fallback = 0) => n?[key] is JsonValue v ? v.GetValue<decimal>() : fallback;
    private static bool B(JsonNode? n, string key, bool fallback = false) => n?[key] is JsonValue v ? v.GetValue<bool>() : fallback;
    /// <summary>
    /// Reads a seeded passport status. <see cref="PassportStatus.Generated"/> is a derived state: it means a PDF
    /// version exists, and only the render pipeline may set it. Seed data asking for it is treated as
    /// <see cref="PassportStatus.Approved"/> so the post-processor renders a real document and promotes the passport.
    /// Without this, a passport could be seeded as "Generated" with no version, no PDF and no checksum — a detail
    /// screen with nothing on it.
    /// </summary>
    private static PassportStatus SeedPassportStatus(JsonNode? n)
    {
        var status = Enum.Parse<PassportStatus>(S(n, "status", "Draft"), true);
        return status == PassportStatus.Generated ? PassportStatus.Approved : status;
    }

    private static IEnumerable<JsonNode> Arr(JsonNode? n, string? key = null) => ((key is null ? n : n?[key]) as JsonArray)?.Where(x => x is not null).Select(x => x!) ?? Enumerable.Empty<JsonNode>();

    public static Guid Id(string kind, string key)
    {
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes($"dspc:{kind}:{key}"));
        var g = new byte[16];
        Array.Copy(hash, g, 16);
        g[7] = (byte)((g[7] & 0x0F) | 0x50);
        g[8] = (byte)((g[8] & 0x3F) | 0x80);
        return new Guid(g);
    }

    private static string Sha256Hex(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();

    [GeneratedRegex(@"^T0(?<d>[+-]\d+)?(?:\s+(?<h>\d{1,2}):(?<m>\d{2}))?$")]
    private static partial Regex T0Pattern();

    /// <summary>"T0+8" → date; "T0-3 14:00" → date part.</summary>
    private DateOnly Date(string? spec, DateOnly? fallback = null)
    {
        if (string.IsNullOrWhiteSpace(spec)) return fallback ?? clock.T0Date;
        var m = T0Pattern().Match(spec.Trim());
        if (m.Success) return clock.T0Date.AddDays(m.Groups["d"].Success ? int.Parse(m.Groups["d"].Value) : 0);
        if (DateOnly.TryParse(spec, out var d)) return ShiftFixture(d);
        throw new FormatException($"Bad date spec '{spec}'");
    }

    private DateOnly? DateN(string? spec) => string.IsNullOrWhiteSpace(spec) ? null : Date(spec);

    /// <summary>"T0+8 14:00" → UTC instant (site-local wall clock). "T0+8" → 06:00.</summary>
    private DateTime Utc(string? spec, DateTime? fallback = null)
    {
        if (string.IsNullOrWhiteSpace(spec)) return fallback ?? clock.T0Utc;
        var m = T0Pattern().Match(spec.Trim());
        if (m.Success)
        {
            var date = clock.T0Date.AddDays(m.Groups["d"].Success ? int.Parse(m.Groups["d"].Value) : 0);
            var h = m.Groups["h"].Success ? int.Parse(m.Groups["h"].Value) : 6;
            var mi = m.Groups["m"].Success ? int.Parse(m.Groups["m"].Value) : 0;
            return clock.FromSiteLocal(date.ToDateTime(new TimeOnly(h, mi), DateTimeKind.Unspecified));
        }
        if (DateTime.TryParse(spec, null, System.Globalization.DateTimeStyles.AssumeLocal | System.Globalization.DateTimeStyles.AdjustToUniversal, out _))
        {
            var local = DateTime.Parse(spec, null, System.Globalization.DateTimeStyles.None);
            return clock.FromSiteLocal(ShiftFixture(local));
        }
        throw new FormatException($"Bad datetime spec '{spec}'");
    }

    private DateTime? UtcN(string? spec) => string.IsNullOrWhiteSpace(spec) ? null : Utc(spec);

    private DateOnly ShiftFixture(DateOnly d) => d.AddDays(clock.T0Date.DayNumber - FixtureT0.DayNumber);
    private DateTime ShiftFixture(DateTime d) => DateTime.SpecifyKind(d.AddDays(clock.T0Date.DayNumber - FixtureT0.DayNumber), DateTimeKind.Unspecified);

    private void Stamp(Entity e, DateTime? at = null) { e.CreatedAt = at ?? clock.UtcNow; e.UpdatedAt = e.CreatedAt; }
}
