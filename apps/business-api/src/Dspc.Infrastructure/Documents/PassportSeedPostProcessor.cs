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
            // Approved is the normal input. Generated-with-no-versions should be impossible (the seeder maps that
            // status down to Approved), but it is picked up too so a bad seed can never leave a passport claiming a
            // document it does not have.
            .Where(p => (p.Status == PassportStatus.Approved || p.Status == PassportStatus.Generated) && p.Versions.Count == 0)
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
                // Do not leave the passport claiming a document that was never produced.
                passport.Status = PassportStatus.Approved;
                passport.CurrentVersion = 0;
            }
        }
        if (candidates.Count > 0) await db.SaveChangesAsync(ct);

        await AssertNoEmptyGeneratedPassportsAsync(ct);
        log.LogInformation("Seed post-processing done in {Ms} ms: {Links} trace links, {Generated} passport PDFs", sw.ElapsedMilliseconds, links, generated);
        _ = clock;
    }

    /// <summary>
    /// A passport in <see cref="PassportStatus.Generated"/> asserts that a versioned PDF with a checksum exists.
    /// If the seed ever produces one without a version the demo silently shows an empty passport screen, so fail the
    /// seed instead and name the offending serials.
    /// </summary>
    private async Task AssertNoEmptyGeneratedPassportsAsync(CancellationToken ct)
    {
        var broken = await db.Passports
            .Where(p => p.Status == PassportStatus.Generated && p.Versions.Count == 0)
            .Select(p => p.ProductSerial!.SerialNumber)
            .OrderBy(s => s)
            .ToListAsync(ct);
        if (broken.Count > 0)
            throw new InvalidOperationException(
                $"Seed produced {broken.Count} passport(s) marked Generated with no PDF version: {string.Join(", ", broken)}. " +
                "Seed them as Approved (the post-processor renders and promotes them) or fix the completeness data.");
    }
}
