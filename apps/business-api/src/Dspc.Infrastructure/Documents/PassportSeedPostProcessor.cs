using System.Diagnostics;
using Dspc.Application.Abstractions;
using Dspc.Application.Modules.Passports;
using Dspc.Application.Modules.Quality;
using Dspc.Domain.Common;
using Dspc.Domain.Quality;
using Dspc.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dspc.Infrastructure.Documents;

/// <summary>
/// After seeding: rebuild the traceability index and generate the PDFs for the passports the demo starts with
/// (<c>PMV-2026-0007</c>, <c>PMV-2026-0008</c> — "already issued" documents). Runs outside the seed transaction and is
/// budgeted: the whole <c>/demo/reset</c> must stay under 10 s, so rendering happens concurrently and a failure only
/// downgrades those passports to <c>Approved</c> instead of breaking the reset.
/// </summary>
public sealed class PassportSeedPostProcessor(
    AppDbContext db, PassportService passports, TraceabilityIndex trace, IDemoClock clock, ILogger<PassportSeedPostProcessor> log) : ISeedPostProcessor
{
    public int Order => 20;

    public async Task RunAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var links = await trace.RebuildAsync(ct);

        var candidates = await db.Passports
            .Include(p => p.ProductSerial).ThenInclude(s => s!.Product)
            .Include(p => p.ProductSerial).ThenInclude(s => s!.ProductionOrder)
            .Include(p => p.Versions)
            .Include(p => p.Template)
            .Where(p => p.Status == PassportStatus.Approved && p.Versions.Count == 0)
            .ToListAsync(ct);

        var generated = 0;
        foreach (var passport in candidates)
        {
            try
            {
                var facts = await passports.FactsAsync(passport, ct);
                var completeness = PassportCompletenessEvaluator.Evaluate(facts);
                if (!completeness.Complete)
                {
                    log.LogInformation("Seed passport {Serial} is not complete ({Missing}) — left as {Status}",
                        facts.SerialNumber, string.Join(", ", completeness.Missing.Select(m => m.Code)), passport.Status);
                    continue;
                }
                await passports.GenerateVersionAsync(passport, facts, completeness, passport.ApprovedBy ?? "quality", ct);
                generated++;
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Could not generate the seeded passport PDF for {Serial}", passport.ProductSerial?.SerialNumber);
            }
        }
        if (generated > 0) await db.SaveChangesAsync(ct);
        log.LogInformation("Seed post-processing done in {Ms} ms: {Links} trace links, {Generated} passport PDFs", sw.ElapsedMilliseconds, links, generated);
        _ = clock;
    }
}
