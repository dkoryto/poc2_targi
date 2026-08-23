using Dspc.Application.Abstractions;
using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dspc.Application.Modules.Planning;

/// <summary>
/// The What-If tiles offered for a plant (spec §5.4). Targets are resolved against that plant's live data, because
/// the seed uses deterministic-but-generated GUIDs and every plant has its own orders, lots and work centres.
/// Exactly one tile is marked <c>featured</c>: the scenario the plant exists to demonstrate
/// (<c>Site.FeaturedScenarioKey</c>, see docs/architecture/multi-site.md).
/// </summary>
public sealed class ScenarioPresetProvider(IAppDbContext db)
{
    private sealed record Definition(string Key, string TitleKey, string PartCode, int Days, ScenarioChangeType Type);

    // Per-plant tiles are derived from the plant's own data; these describe what to look for.
    private static readonly Definition[] InboundDelays =
    [
        new("DELAY_ACT40_10D", "planning.presets.ACT40_DELAY", "ACT-40", 10, ScenarioChangeType.DELAY_INBOUND),
        new("DELAY_MCUX7_14D", "planning.presets.MCU_X7_DELAY", "MCU-X7", 14, ScenarioChangeType.DELAY_INBOUND),
    ];

    public async Task<IReadOnlyList<ScenarioPresetDto>> GetAsync(Site site, CancellationToken ct)
    {
        var result = new List<ScenarioPresetDto>();

        // --- inbound delays: pick the plant's soonest open line for the part, which is the one that actually bites
        var openLines = await db.PurchaseOrderLines.AsNoTracking()
            .Include(l => l.PurchaseOrder).Include(l => l.Part)
            .Where(l => l.PurchaseOrder!.SiteId == site.Id && l.Status != PurchaseOrderLineStatus.Delivered)
            .OrderBy(l => l.Eta).ThenBy(l => l.PurchaseOrder!.Code).ThenBy(l => l.LineNo)
            .ToListAsync(ct);

        foreach (var d in InboundDelays)
        {
            var line = openLines.FirstOrDefault(l => string.Equals(l.Part!.Code, d.PartCode, StringComparison.OrdinalIgnoreCase));
            if (line is null) continue;   // this plant does not buy that part — tile simply is not offered
            result.Add(new ScenarioPresetDto(d.Key, d.TitleKey,
                [new ScenarioChangeDto(ScenarioChangeType.DELAY_INBOUND, PoLineId: line.Id, Days: d.Days,
                    PoCode: line.PurchaseOrder!.Code, PartCode: line.Part!.Code)],
                d.Key == site.FeaturedScenarioKey));
        }

        // --- block a steel lot the plant has actually built into a finished product: blocking a lot that was never
        // consumed invalidates nothing, so prefer one with consumptions (SITE-01 → HTS-22-2608, SITE-03 → HTS-22-3110).
        var steelLots = await db.MaterialLots.AsNoTracking().Include(l => l.Part)
            .Where(l => l.SiteId == site.Id && l.Part!.Code == "HTS-22" && l.Status == MaterialLotStatus.Accepted)
            .OrderBy(l => l.LotNumber).Select(l => new { l.Id, l.LotNumber }).ToListAsync(ct);
        var consumedLotIds = await db.MaterialConsumptions.AsNoTracking()
            .Where(c => c.ProductSerial != null).Select(c => c.MaterialLotId).Distinct().ToListAsync(ct);
        var steelLot = steelLots.FirstOrDefault(l => consumedLotIds.Contains(l.Id))?.LotNumber
                       ?? steelLots.FirstOrDefault()?.LotNumber;
        if (steelLot is not null)
            result.Add(new ScenarioPresetDto("BLOCK_LOT_HTS22", "planning.presets.HTS22_BLOCK",
                [new ScenarioChangeDto(ScenarioChangeType.BLOCK_LOT, LotNumber: steelLot)],
                site.FeaturedScenarioKey == "BLOCK_LOT_HTS22"));

        // --- raise the priority of the plant's most urgent non-frozen order
        var order = await db.ProductionOrders.AsNoTracking()
            .Where(o => o.SiteId == site.Id && !o.Frozen && o.Status != ProductionOrderStatus.Completed && o.Status != ProductionOrderStatus.Cancelled)
            .OrderByDescending(o => o.Priority).ThenBy(o => o.DueDate).ThenBy(o => o.Code)
            .Select(o => o.Code).FirstOrDefaultAsync(ct);
        if (order is not null)
            // The title names the order, which differs per plant — pass it in rather than baking
            // Kielce's WO-2026-014 into the translation, where it was simply wrong elsewhere.
            result.Add(new ScenarioPresetDto("PRIORITY_WO014", "planning.presets.WO014_PRIORITY",
                [new ScenarioChangeDto(ScenarioChangeType.PRIORITY, OrderCode: order, Priority: 5)],
                site.FeaturedScenarioKey == "PRIORITY_WO014",
                new Dictionary<string, string> { ["orderCode"] = order }));

        // --- halve the plant's integration cell
        var wc = await db.WorkCenters.AsNoTracking()
            .Where(w => w.SiteId == site.Id && w.Code.EndsWith("WC-INT"))
            .OrderBy(w => w.Sequence).Select(w => w.Code).FirstOrDefaultAsync(ct)
            ?? await db.WorkCenters.AsNoTracking().Where(w => w.SiteId == site.Id).OrderBy(w => w.Sequence).Select(w => w.Code).FirstOrDefaultAsync(ct);
        if (wc is not null)
            result.Add(new ScenarioPresetDto("CAPACITY_INT_50", "planning.presets.WC_INT_CAPACITY",
                [new ScenarioChangeDto(ScenarioChangeType.CAPACITY, WorkCenterCode: wc, Factor: 0.5)],
                site.FeaturedScenarioKey == "CAPACITY_INT_50"));

        // the plant's own story first
        return result.OrderByDescending(r => r.Featured).ToList();
    }
}
