using Dspc.Application.Abstractions;
using Dspc.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Dspc.Application.Modules.Planning;

/// <summary>
/// The five demo tiles (spec §5.4). Inbound targets are resolved to the live purchase-order line id at request time,
/// because the seed uses deterministic-but-generated GUIDs. `TitleKey` points at the web app's i18n catalogue.
/// </summary>
public sealed class ScenarioPresetProvider(IAppDbContext db)
{
    private sealed record InboundTarget(string PoCode, string PartCode, int Days);

    private static readonly (string Key, string TitleKey, InboundTarget? Inbound, ScenarioChangeDto? Direct)[] Definitions =
    [
        ("DELAY_ACT40_10D", "planning.presets.ACT40_DELAY", new InboundTarget("PO-2026-0007", "ACT-40", 10), null),
        ("DELAY_MCUX7_14D", "planning.presets.MCU_X7_DELAY", new InboundTarget("PO-2026-0009", "MCU-X7", 14), null),
        ("BLOCK_LOT_HTS22", "planning.presets.HTS22_BLOCK", null, new ScenarioChangeDto(ScenarioChangeType.BLOCK_LOT, LotNumber: "HTS-22-2608")),
        ("PRIORITY_WO014", "planning.presets.WO014_PRIORITY", null, new ScenarioChangeDto(ScenarioChangeType.PRIORITY, OrderCode: "WO-2026-014", Priority: 5)),
        ("CAPACITY_INT_50", "planning.presets.WC_INT_CAPACITY", null, new ScenarioChangeDto(ScenarioChangeType.CAPACITY, WorkCenterCode: "WC-INT", Factor: 0.5))
    ];

    public async Task<IReadOnlyList<ScenarioPresetDto>> GetAsync(CancellationToken ct)
    {
        var wanted = Definitions.Where(d => d.Inbound is not null).Select(d => d.Inbound!.PoCode).Distinct().ToList();
        var lines = await db.PurchaseOrderLines.AsNoTracking()
            .Include(l => l.PurchaseOrder).Include(l => l.Part)
            .Where(l => wanted.Contains(l.PurchaseOrder!.Code))
            .ToListAsync(ct);

        var result = new List<ScenarioPresetDto>();
        foreach (var (key, titleKey, inbound, direct) in Definitions)
        {
            if (direct is not null) { result.Add(new ScenarioPresetDto(key, titleKey, [direct])); continue; }
            var line = lines.FirstOrDefault(l => l.PurchaseOrder!.Code == inbound!.PoCode
                                                 && string.Equals(l.Part!.Code, inbound.PartCode, StringComparison.OrdinalIgnoreCase));
            if (line is null) continue;   // seed variant without that line — tile is simply not offered
            result.Add(new ScenarioPresetDto(key, titleKey,
            [
                new ScenarioChangeDto(ScenarioChangeType.DELAY_INBOUND, PoLineId: line.Id, Days: inbound!.Days,
                    PoCode: inbound.PoCode, PartCode: inbound.PartCode)
            ]));
        }
        return result;
    }
}
