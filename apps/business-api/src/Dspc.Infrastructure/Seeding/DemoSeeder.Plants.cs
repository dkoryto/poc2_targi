using System.Text.Json.Nodes;
using Dspc.Application.Common;
using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Dspc.Application.Modules.Planning;
using Microsoft.Extensions.Logging;

namespace Dspc.Infrastructure.Seeding;

/// <summary>
/// Seeds the demo plants SITE-02..04 from <c>plants.json</c>. SITE-01 (Kielce) is seeded by
/// <see cref="DemoSeeder.SeedAsync"/> from the golden-path files and is deliberately untouched here —
/// see <c>docs/architecture/multi-site.md</c>. Each plant carries its own lines, work centres, purchase
/// orders, lots, orders and 12-week baseline, plus the scenario it is meant to demonstrate.
/// </summary>
public sealed partial class DemoSeeder
{
    private async Task SeedPlantsAsync(
        string dir, Organization org, Dictionary<string, Site> sites, Dictionary<string, Supplier> suppliers,
        Dictionary<string, PartDefinition> parts, Dictionary<string, ProductDefinition> products, Dictionary<string, BomVersion> boms,
        Dictionary<string, Dictionary<int, (string Pl, string En, string Wc)>> routing,
        Dictionary<Guid, string> supplierUserBySupplier, PassportTemplate template,
        Dictionary<string, int> counts, CancellationToken ct)
    {
        var path = System.IO.Path.Combine(dir, "plants.json");
        if (!File.Exists(path)) { log.LogInformation("plants.json not found — seeding SITE-01 only"); return; }
        var plantsJson = Load(dir, "plants.json");

        foreach (var plant in Arr(plantsJson, "plants"))
        {
            var sNode = plant["site"];
            var site = new Site
            {
                Id = Id("SITE", S(sNode, "code")), Code = S(sNode, "code"), Name = S(sNode, "name"), Country = S(sNode, "country", "PL"),
                City = S(sNode, "city"), Latitude = D(sNode, "lat"), Longitude = D(sNode, "lon"), TimeZone = S(sNode, "timeZone", "Europe/Warsaw"),
                OrganizationId = org.Id, ProfileKey = S(sNode, "profileKey"), FeaturedScenarioKey = S(sNode, "featuredScenarioKey"),
                IsDefault = false, Sequence = I(sNode, "sequence", 9)
            };
            Stamp(site); db.Sites.Add(site); sites[site.Code] = site;

            var lines = new Dictionary<string, AssemblyLine>();
            foreach (var l in Arr(plant, "lines"))
            {
                var line = new AssemblyLine { Id = Id("LINE", S(l, "code")), Code = S(l, "code"), Name = S(l, "name"), SiteId = site.Id };
                Stamp(line); db.AssemblyLines.Add(line); lines[line.Code] = line;
            }
            var workCenters = new Dictionary<string, WorkCenter>();
            foreach (var w in Arr(plant, "workCenters"))
            {
                var wc = new WorkCenter
                {
                    Id = Id("WC", S(w, "code")), Code = S(w, "code"), NamePl = S(w, "namePl"), NameEn = S(w, "nameEn"), SiteId = site.Id,
                    AssemblyLineId = lines[S(w, "line")].Id, HoursPerDay = D(w, "hoursPerDay", 16), ShiftStartHour = 6, Sequence = I(w, "sequence")
                };
                Stamp(wc); db.WorkCenters.Add(wc); workCenters[wc.Code] = wc;
            }
            counts["workCenters"] = counts.GetValueOrDefault("workCenters") + workCenters.Count;

            // --- purchase orders + lines + documents
            var poLines = new Dictionary<string, PurchaseOrderLine>();
            var shipmentRefByLine = new Dictionary<Guid, string>();
            foreach (var p in Arr(plant, "purchaseOrders"))
            {
                var sup = suppliers[S(p, "supplier")];
                var po = new PurchaseOrder
                {
                    Id = Id("PO", S(p, "code")), Code = S(p, "code"), SupplierId = sup.Id, SiteId = site.Id,
                    Status = Enum.Parse<PurchaseOrderStatus>(S(p, "status", "Open"), true), OrderedOn = Date(S(p, "orderedOn")), Notes = SN(p, "notes")
                };
                Stamp(po, Utc(S(p, "orderedOn"))); db.PurchaseOrders.Add(po);
                foreach (var l in Arr(p, "lines"))
                {
                    var part = parts[S(l, "part")];
                    var status = Enum.Parse<PurchaseOrderLineStatus>(S(l, "status", "Confirmed"), true);
                    var qty = M(l, "qty");
                    var line = new PurchaseOrderLine
                    {
                        Id = Id("POL", $"{po.Code}/{I(l, "lineNo")}"), PurchaseOrderId = po.Id, LineNo = I(l, "lineNo"), PartId = part.Id, Quantity = qty,
                        DeliveredQuantity = status == PurchaseOrderLineStatus.Delivered ? qty : 0, RequiredDate = Date(S(l, "requiredDate")),
                        Eta = Date(S(l, "eta")), OriginalEta = Date(S(l, "eta")), ProgressPercent = I(l, "progress"), Status = status,
                        LotNumber = SN(l, "lotNumber"), HeatNumber = SN(l, "heatNumber"), ProducedOn = DateN(SN(l, "producedOn")), ExpiresOn = DateN(SN(l, "expiresOn")),
                        SupplierConfirmed = B(l, "supplierConfirmed", true), DeliveredOn = DateN(SN(l, "deliveredOn"))
                    };
                    Stamp(line, po.CreatedAt); db.PurchaseOrderLines.Add(line); poLines[$"{po.Code}/{line.LineNo}"] = line;
                    if (SN(l, "shipment") is { } shc) shipmentRefByLine[line.Id] = shc;
                    foreach (var d in Arr(l, "documents"))
                    {
                        db.QualityDocuments.Add(BuildDocument(d, sup, supplierUserBySupplier, line.Id, null, line.LotNumber, line.HeatNumber));
                        counts["documents"] = counts.GetValueOrDefault("documents") + 1;
                    }
                }
            }
            counts["purchaseOrders"] = counts.GetValueOrDefault("purchaseOrders") + Arr(plant, "purchaseOrders").Count();
            counts["purchaseOrderLines"] = counts.GetValueOrDefault("purchaseOrderLines") + poLines.Count;

            // --- shipments
            var shipments = new Dictionary<string, Shipment>();
            foreach (var s in Arr(plant, "shipments"))
            {
                var po = db.PurchaseOrders.Local.First(x => x.Code == S(s, "po"));
                var sh = new Shipment
                {
                    Id = Id("SHP", S(s, "code")), Code = S(s, "code"), SupplierId = po.SupplierId, PurchaseOrderId = po.Id,
                    Status = Enum.Parse<ShipmentStatus>(S(s, "status", "Advised"), true), Carrier = SN(s, "carrier"), Vehicle = SN(s, "vehicle"),
                    PlannedDeparture = UtcN(SN(s, "plannedDeparture")), ActualDeparture = UtcN(SN(s, "actualDeparture")), Eta = Date(S(s, "eta")), Progress = D(s, "progress")
                };
                Stamp(sh); db.Shipments.Add(sh); shipments[sh.Code] = sh;
                var evIdx = 0;
                foreach (var e in Arr(s, "events"))
                {
                    var ev = new ShipmentEvent
                    {
                        Id = Id("SHPEV", $"{sh.Code}:{evIdx++}"), ShipmentId = sh.Id, Type = Enum.Parse<ShipmentEventType>(S(e, "type", "Note"), true),
                        OccurredAt = Utc(S(e, "at")), Note = SN(e, "note"), Location = SN(e, "location"), RecordedBy = supplierUserBySupplier.GetValueOrDefault(po.SupplierId, "system")
                    };
                    Stamp(ev, ev.OccurredAt); db.ShipmentEvents.Add(ev);
                }
            }
            foreach (var (lineId, code) in shipmentRefByLine)
                if (shipments.TryGetValue(code, out var sh)) poLines.Values.First(l => l.Id == lineId).ShipmentId = sh.Id;
            counts["shipments"] = counts.GetValueOrDefault("shipments") + shipments.Count;

            // --- lots
            var lots = new Dictionary<string, MaterialLot>(StringComparer.OrdinalIgnoreCase);
            foreach (var l in Arr(plant, "lots"))
            {
                var part = parts[S(l, "part")];
                var sup = suppliers[S(l, "supplier")];
                var poLine = SN(l, "poLine") is { } pl ? poLines[pl] : null;
                var lot = new MaterialLot
                {
                    Id = Id("LOT", S(l, "lotNumber")), LotNumber = S(l, "lotNumber"), HeatNumber = SN(l, "heatNumber"), PartId = part.Id, SupplierId = sup.Id,
                    SiteId = site.Id, PurchaseOrderLineId = poLine?.Id, Quantity = M(l, "quantity"), RemainingQuantity = M(l, "remaining"),
                    Unit = S(l, "unit", part.Unit), Status = Enum.Parse<MaterialLotStatus>(S(l, "status", "Accepted"), true),
                    ReceivedOn = DateN(SN(l, "receivedOn")), ProducedOn = DateN(SN(l, "producedOn")), ExpiresOn = DateN(SN(l, "expiresOn")),
                    CountryOfOrigin = S(l, "country", sup.Country)
                };
                Stamp(lot, lot.ReceivedOn is { } r ? clock.FromSiteLocal(r.ToDateTime(new TimeOnly(8, 0))) : clock.UtcNow);
                db.MaterialLots.Add(lot); lots[lot.LotNumber] = lot;
                foreach (var d in Arr(l, "documents"))
                {
                    db.QualityDocuments.Add(BuildDocument(d, sup, supplierUserBySupplier, null, lot.Id, lot.LotNumber, lot.HeatNumber));
                    counts["documents"] = counts.GetValueOrDefault("documents") + 1;
                }
                foreach (var q in Arr(l, "inspections"))
                {
                    var qi = new QualityInspection
                    {
                        Id = Id("QI", S(q, "code")), Code = S(q, "code"), MaterialLotId = lot.Id, Result = Enum.Parse<InspectionResult>(S(q, "result", "Passed"), true),
                        InspectedBy = S(q, "by", "quality"), InspectedAt = Utc(S(q, "at")), Notes = SN(q, "notes")
                    };
                    Stamp(qi, qi.InspectedAt); db.QualityInspections.Add(qi);
                    counts["inspections"] = counts.GetValueOrDefault("inspections") + 1;
                }
            }
            counts["lots"] = counts.GetValueOrDefault("lots") + lots.Count;

            // --- baseline, orders, operations
            var baseline = new PlanningBaseline
            {
                Id = Id("BASELINE", $"{site.Code}:v1"), SiteId = site.Id, Version = 1, Status = PlanningBaselineStatus.Active,
                HorizonStart = clock.T0Date, HorizonEnd = clock.T0Date.AddDays(83), ApprovedBy = "planner", ApprovedAt = clock.T0Utc.AddDays(-3),
                Notes = $"Plan bazowy 12 tygodni ({site.Code})"
            };
            Stamp(baseline, baseline.ApprovedAt); db.PlanningBaselines.Add(baseline);

            var orders = new Dictionary<string, ProductionOrder>();
            var opDefs = new Dictionary<string, OperationDefinition>(StringComparer.OrdinalIgnoreCase);
            var serials = new Dictionary<string, ProductSerial>();
            foreach (var o in Arr(plant, "orders"))
            {
                var code = S(o, "code");
                var productCode = S(o, "product");
                var order = new ProductionOrder
                {
                    Id = Id("WO", code), Code = code, ProductId = products[productCode].Id, BomVersionId = boms[productCode].Id, SiteId = site.Id,
                    AssemblyLineId = SN(o, "line") is { } lc ? lines[lc].Id : null, Quantity = I(o, "quantity", 1), Priority = I(o, "priority", 3),
                    ReleaseDate = Date(S(o, "releaseDate")), DueDate = Date(S(o, "dueDate")),
                    Status = Enum.Parse<ProductionOrderStatus>(S(o, "status", "Planned"), true), Frozen = B(o, "frozen"), CustomerReference = SN(o, "customerReference")
                };
                Stamp(order, clock.FromSiteLocal(order.ReleaseDate.ToDateTime(new TimeOnly(6, 0))).AddDays(-7));
                db.ProductionOrders.Add(order); orders[code] = order;

                foreach (var op in Arr(o, "operations"))
                {
                    var seq = I(op, "seq");
                    var wcCode = S(op, "wc");
                    var names = routing[productCode].GetValueOrDefault(seq, (Pl: "Operacja", En: "Operation", Wc: wcCode));
                    var status = Enum.Parse<OperationStatus>(S(op, "status", "Planned"), true);
                    var def = new OperationDefinition
                    {
                        Id = Id("OP", $"{code}/{seq}"), ProductionOrderId = order.Id, Code = $"{code}/{seq}", Sequence = seq, NamePl = names.Pl, NameEn = names.En,
                        WorkCenterId = workCenters[wcCode].Id, DurationHours = D(op, "hours"), Frozen = B(op, "frozen"), Status = status,
                        MaterialRequirementsJson = Json.Serialize(Arr(op, "materials").Select(r => new MaterialRequirement { PartCode = S(r, "part"), Quantity = M(r, "qty") }).ToList())
                    };
                    Stamp(def, order.CreatedAt); db.OperationDefinitions.Add(def); opDefs[def.Code] = def;
                    var so = new ScheduledOperation
                    {
                        Id = Id("SOP", $"{baseline.Id}:{def.Code}"), PlanningBaselineId = baseline.Id, OperationDefinitionId = def.Id,
                        WorkCenterId = def.WorkCenterId, AssemblyLineId = order.AssemblyLineId, Start = Utc(S(op, "start")), End = Utc(S(op, "end")), Frozen = def.Frozen
                    };
                    Stamp(so, baseline.CreatedAt); db.ScheduledOperations.Add(so);
                    counts["scheduledOperations"] = counts.GetValueOrDefault("scheduledOperations") + 1;
                }

                foreach (var r in Arr(o, "reservations"))
                {
                    var lot = SN(r, "lot") is { } ln ? lots.GetValueOrDefault(ln) : null;
                    var res = new Reservation
                    {
                        Id = Id("RES", $"{code}:{S(r, "part")}:{SN(r, "lot")}"), PartId = parts[S(r, "part")].Id, ProductionOrderId = order.Id,
                        MaterialLotId = lot?.Id, Quantity = M(r, "qty"), IsBlocked = lot?.Status is MaterialLotStatus.Blocked or MaterialLotStatus.Recalled
                    };
                    Stamp(res, order.CreatedAt); db.Reservations.Add(res);
                    counts["reservations"] = counts.GetValueOrDefault("reservations") + 1;
                }
                foreach (var s in Arr(o, "serials"))
                {
                    var serial = new ProductSerial
                    {
                        Id = Id("SER", S(s, "serial")), SerialNumber = S(s, "serial"), ProductId = order.ProductId, ProductionOrderId = order.Id,
                        BomVersionId = order.BomVersionId, Status = Enum.Parse<SerialStatus>(S(s, "status", "Planned"), true), CompletedAt = UtcN(SN(s, "completedAt"))
                    };
                    Stamp(serial, order.CreatedAt); db.ProductSerials.Add(serial); serials[serial.SerialNumber] = serial;
                    counts["serials"] = counts.GetValueOrDefault("serials") + 1;
                }
                var cIdx = 0;
                foreach (var c in Arr(o, "consumptions"))
                {
                    var mc = new MaterialConsumption
                    {
                        Id = Id("CONS", $"{code}:{cIdx++}"), ProductionOrderId = order.Id, OperationDefinitionId = opDefs.GetValueOrDefault($"{code}/{I(c, "opSeq")}")?.Id,
                        MaterialLotId = lots[S(c, "lot")].Id, ProductSerialId = SN(c, "serial") is { } sn ? serials[sn].Id : null,
                        Quantity = M(c, "qty"), ConsumedAt = Utc(S(c, "at")), RecordedBy = "operator.demo"
                    };
                    Stamp(mc, mc.ConsumedAt); db.MaterialConsumptions.Add(mc);
                    counts["consumptions"] = counts.GetValueOrDefault("consumptions") + 1;
                }
            }
            counts["productionOrders"] = counts.GetValueOrDefault("productionOrders") + orders.Count;

            // --- inventory balances for this plant's parts
            var reservedByPart = db.ChangeTracker.Entries<Reservation>().Select(e => e.Entity)
                .Where(r => !r.IsBlocked && orders.Values.Any(o => o.Id == r.ProductionOrderId))
                .GroupBy(r => r.PartId).ToDictionary(g => g.Key, g => g.Sum(r => r.Quantity));
            foreach (var partId in lots.Values.Select(l => l.PartId).Distinct())
            {
                var pl = lots.Values.Where(l => l.PartId == partId).ToList();
                var bal = new InventoryBalance
                {
                    Id = Id("INV", $"{site.Code}:{partId}"), PartId = partId, SiteId = site.Id,
                    OnHand = pl.Where(l => l.Status is MaterialLotStatus.Accepted or MaterialLotStatus.ConditionallyReleased).Sum(l => l.RemainingQuantity),
                    Blocked = pl.Where(l => l.Status is MaterialLotStatus.Blocked or MaterialLotStatus.Recalled).Sum(l => l.RemainingQuantity),
                    Reserved = reservedByPart.GetValueOrDefault(partId, 0)
                };
                Stamp(bal); db.InventoryBalances.Add(bal);
            }

            // --- serial inspections + passports
            foreach (var q in Arr(plant, "serialInspections"))
            {
                var qi = new QualityInspection
                {
                    Id = Id("QI", S(q, "code")), Code = S(q, "code"), ProductSerialId = serials[S(q, "serial")].Id,
                    Result = Enum.Parse<InspectionResult>(S(q, "result", "Passed"), true), InspectedBy = S(q, "by", "quality"),
                    InspectedAt = Utc(S(q, "at")), Notes = SN(q, "notes")
                };
                Stamp(qi, qi.InspectedAt); db.QualityInspections.Add(qi);
                counts["inspections"] = counts.GetValueOrDefault("inspections") + 1;
            }
            foreach (var p in Arr(plant, "passports"))
            {
                var serial = serials[S(p, "serial")];
                var pp = new Passport
                {
                    Id = Id("PASS", serial.SerialNumber), ProductSerialId = serial.Id, PassportTemplateId = template.Id,
                    Status = SeedPassportStatus(p), ApprovedBy = SN(p, "approvedBy"),
                    ApprovedAt = UtcN(SN(p, "approvedAt")), CurrentVersion = 0, DeviationsJson = "[]"
                };
                Stamp(pp, serial.CreatedAt); db.Passports.Add(pp);
                counts["passports"] = counts.GetValueOrDefault("passports") + 1;
            }
        }
        counts["sites"] = sites.Count;
    }
}
