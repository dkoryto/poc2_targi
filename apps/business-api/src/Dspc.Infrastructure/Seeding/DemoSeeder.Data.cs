using System.Text.Json.Nodes;
using Dspc.Application.Common;
using Dspc.Application.Modules.Planning;
using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dspc.Infrastructure.Seeding;

public sealed partial class DemoSeeder
{
    private async Task SeedAsync(string dir, Dictionary<string, int> counts, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var orgJson = Load(dir, "organization.json");
        var suppliersJson = Load(dir, "suppliers.json");
        var partsJson = Load(dir, "parts.json");
        var productsJson = Load(dir, "products.json");
        var usersJson = Load(dir, "users.json");
        var posJson = Load(dir, "purchase-orders.json");
        var shipmentsJson = Load(dir, "shipments.json");
        var lotsJson = Load(dir, "lots.json");
        var ordersJson = Load(dir, "production-orders.json");
        var baselineJson = Load(dir, "baseline.json");
        var qualityJson = Load(dir, "quality.json");

        // --- organization, site, lines, work centers
        var org = new Organization { Id = Id("ORG", S(orgJson["organization"], "code")), Code = S(orgJson["organization"], "code"), Name = S(orgJson["organization"], "name"), Country = S(orgJson["organization"], "country", "PL") };
        Stamp(org); db.Organizations.Add(org);
        var sites = new Dictionary<string, Site>();
        foreach (var s in Arr(orgJson, "sites"))
        {
            var site = new Site { Id = Id("SITE", S(s, "code")), Code = S(s, "code"), Name = S(s, "name"), Country = S(s, "country", "PL"), City = S(s, "city"), Latitude = D(s, "lat"), Longitude = D(s, "lon"), TimeZone = S(s, "timeZone", "Europe/Warsaw"), OrganizationId = org.Id, ProfileKey = S(s, "profileKey"), FeaturedScenarioKey = S(s, "featuredScenarioKey"), IsDefault = B(s, "isDefault"), Sequence = I(s, "sequence", 1) };
            Stamp(site); db.Sites.Add(site); sites[site.Code] = site;
        }
        var site0 = sites.Values.First();
        var lines = new Dictionary<string, AssemblyLine>();
        foreach (var l in Arr(orgJson, "lines"))
        {
            var line = new AssemblyLine { Id = Id("LINE", S(l, "code")), Code = S(l, "code"), Name = S(l, "name"), SiteId = sites[S(l, "site", site0.Code)].Id };
            Stamp(line); db.AssemblyLines.Add(line); lines[line.Code] = line;
        }
        var workCenters = new Dictionary<string, WorkCenter>();
        foreach (var w in Arr(orgJson, "workCenters"))
        {
            var wc = new WorkCenter { Id = Id("WC", S(w, "code")), Code = S(w, "code"), NamePl = S(w, "namePl"), NameEn = S(w, "nameEn"), SiteId = site0.Id, AssemblyLineId = lines[S(w, "line")].Id, HoursPerDay = D(w, "hoursPerDay", 16), ShiftStartHour = 6, Sequence = I(w, "sequence") };
            Stamp(wc); db.WorkCenters.Add(wc); workCenters[wc.Code] = wc;
        }
        counts["sites"] = sites.Count; counts["workCenters"] = workCenters.Count;

        // --- suppliers
        var suppliers = new Dictionary<string, Supplier>();
        var perfCount = 0;
        foreach (var s in Arr(suppliersJson))
        {
            var sup = new Supplier { Id = Id("SUP", S(s, "code")), Code = S(s, "code"), Name = S(s, "name"), Country = S(s, "country"), City = S(s, "city"), Latitude = D(s, "lat"), Longitude = D(s, "lon"), OtifPercent = D(s, "otif", 90), QualityScore = D(s, "qualityScore", 90), IsActive = true };
            Stamp(sup); db.Suppliers.Add(sup); suppliers[sup.Code] = sup;
            foreach (var p in Arr(s, "performance"))
            {
                var perf = new SupplierPerformance { Id = Id("SUPPERF", $"{sup.Code}:{S(p, "periodStart")}"), SupplierId = sup.Id, PeriodStart = Date(S(p, "periodStart")), PeriodEnd = Date(S(p, "periodEnd")), DeliveredLines = I(p, "deliveredLines"), OnTimeInFullLines = I(p, "onTimeInFullLines"), QualityRejections = I(p, "qualityRejections"), OtifPercent = D(p, "otifPercent") };
                Stamp(perf); db.SupplierPerformances.Add(perf); perfCount++;
            }
        }
        counts["suppliers"] = suppliers.Count;

        // --- parts
        var parts = new Dictionary<string, PartDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in Arr(partsJson))
        {
            var part = new PartDefinition
            {
                Id = Id("PART", S(p, "code")), Code = S(p, "code"), NamePl = S(p, "namePl"), NameEn = S(p, "nameEn"), Unit = S(p, "unit", "szt"), Criticality = I(p, "criticality", 3),
                Category = Enum.Parse<PartCategory>(S(p, "category", "Mechanika"), true), HasAlternativeSupplier = B(p, "hasAlternativeSupplier"),
                PrimarySupplierId = SN(p, "primarySupplier") is { } ps ? suppliers[ps].Id : null, AlternativeSupplierId = SN(p, "alternativeSupplier") is { } alt ? suppliers[alt].Id : null,
                RequiredDocumentTypesJson = Json.Serialize(Arr(p, "requiredDocumentTypes").Select(x => x.GetValue<string>()).ToList())
            };
            Stamp(part); db.Parts.Add(part); parts[part.Code] = part;
        }
        counts["parts"] = parts.Count;

        // --- products, BOM, routing names
        var products = new Dictionary<string, ProductDefinition>();
        var boms = new Dictionary<string, BomVersion>();
        var routing = new Dictionary<string, Dictionary<int, (string Pl, string En, string Wc)>>();
        var bomItems = 0;
        foreach (var p in Arr(productsJson))
        {
            var prod = new ProductDefinition { Id = Id("PROD", S(p, "code")), Code = S(p, "code"), NamePl = S(p, "namePl"), NameEn = S(p, "nameEn"), SerialPrefix = S(p, "serialPrefix"), Family = S(p, "family") };
            Stamp(prod); db.Products.Add(prod); products[prod.Code] = prod;
            var bomNode = p["bom"];
            var bom = new BomVersion { Id = Id("BOM", $"{prod.Code}:{S(bomNode, "version", "A")}"), ProductId = prod.Id, Version = S(bomNode, "version", "A"), IsActive = true, EffectiveFrom = clock.T0Date.AddDays(-120) };
            Stamp(bom); db.BomVersions.Add(bom); boms[prod.Code] = bom;
            var seq = 0;
            foreach (var it in Arr(bomNode, "items"))
            {
                seq += 10;
                var item = new BomItem { Id = Id("BOMI", $"{bom.Id}:{S(it, "part")}"), BomVersionId = bom.Id, PartId = parts[S(it, "part")].Id, QuantityPerUnit = M(it, "qty"), Sequence = seq, ConsumedAtOperation = I(it, "op", 10), IsKeyComponent = B(it, "key") };
                Stamp(item); db.BomItems.Add(item); bomItems++;
            }
            routing[prod.Code] = Arr(p, "routing").ToDictionary(r => I(r, "seq"), r => (Pl: S(r, "namePl"), En: S(r, "nameEn"), Wc: S(r, "wc")));
        }
        counts["products"] = products.Count; counts["bomItems"] = bomItems;

        // --- users
        var users = new Dictionary<string, User>();
        foreach (var u in Arr(usersJson))
        {
            var user = new User
            {
                Id = Id("USER", S(u, "username")), Username = S(u, "username").ToLowerInvariant(), DisplayName = S(u, "displayName"), PasswordHash = hasher.Hash(options.Value.AccountPassword ?? S(u, "password", "demo")),
                Role = Enum.Parse<Role>(S(u, "role"), true), SupplierId = SN(u, "supplier") is { } sc ? suppliers[sc].Id : null, SiteId = site0.Id, Locale = "pl", IsActive = true, Description = SN(u, "description")
            };
            Stamp(user); db.Users.Add(user); users[user.Username] = user;
        }
        counts["users"] = users.Count;
        var supplierUserBySupplier = users.Values.Where(u => u.SupplierId is not null).GroupBy(u => u.SupplierId!.Value).ToDictionary(g => g.Key, g => g.First().Username);

        // --- purchase orders, lines, documents
        var poLines = new Dictionary<string, PurchaseOrderLine>();
        var pos = new Dictionary<string, PurchaseOrder>();
        var docCount = 0;
        var shipmentRefByLine = new Dictionary<Guid, string>();
        foreach (var p in Arr(posJson))
        {
            var sup = suppliers[S(p, "supplier")];
            var po = new PurchaseOrder { Id = Id("PO", S(p, "code")), Code = S(p, "code"), SupplierId = sup.Id, SiteId = site0.Id, Status = Enum.Parse<PurchaseOrderStatus>(S(p, "status", "Open"), true), OrderedOn = Date(S(p, "orderedOn")), Notes = SN(p, "notes") };
            Stamp(po, Utc(S(p, "orderedOn")) ); db.PurchaseOrders.Add(po); pos[po.Code] = po;
            foreach (var l in Arr(p, "lines"))
            {
                var part = parts[S(l, "part")];
                var status = Enum.Parse<PurchaseOrderLineStatus>(S(l, "status", "Confirmed"), true);
                var qty = M(l, "qty");
                var line = new PurchaseOrderLine
                {
                    Id = Id("POL", $"{po.Code}/{I(l, "lineNo")}"), PurchaseOrderId = po.Id, LineNo = I(l, "lineNo"), PartId = part.Id, Quantity = qty,
                    DeliveredQuantity = status == PurchaseOrderLineStatus.Delivered ? qty : 0, RequiredDate = Date(S(l, "requiredDate")), Eta = Date(S(l, "eta")), OriginalEta = Date(S(l, "eta")),
                    ProgressPercent = I(l, "progress"), Status = status, LotNumber = SN(l, "lotNumber"), HeatNumber = SN(l, "heatNumber"), ProducedOn = DateN(SN(l, "producedOn")), ExpiresOn = DateN(SN(l, "expiresOn")),
                    SupplierConfirmed = B(l, "supplierConfirmed", true), DeliveredOn = DateN(SN(l, "deliveredOn"))
                };
                Stamp(line, po.CreatedAt); db.PurchaseOrderLines.Add(line); poLines[$"{po.Code}/{line.LineNo}"] = line;
                if (SN(l, "shipment") is { } shc) shipmentRefByLine[line.Id] = shc;
                foreach (var d in Arr(l, "documents"))
                {
                    db.QualityDocuments.Add(BuildDocument(d, sup, supplierUserBySupplier, line.Id, null, line.LotNumber, line.HeatNumber));
                    docCount++;
                }
            }
        }
        counts["purchaseOrders"] = pos.Count; counts["purchaseOrderLines"] = poLines.Count;

        // --- shipments
        var shipments = new Dictionary<string, Shipment>();
        foreach (var s in Arr(shipmentsJson))
        {
            var po = pos[S(s, "po")];
            var sh = new Shipment
            {
                Id = Id("SHP", S(s, "code")), Code = S(s, "code"), SupplierId = po.SupplierId, PurchaseOrderId = po.Id, Status = Enum.Parse<ShipmentStatus>(S(s, "status", "Advised"), true),
                Carrier = SN(s, "carrier"), Vehicle = SN(s, "vehicle"), PlannedDeparture = UtcN(SN(s, "plannedDeparture")), ActualDeparture = UtcN(SN(s, "actualDeparture")), Eta = Date(S(s, "eta")), Progress = D(s, "progress")
            };
            Stamp(sh); db.Shipments.Add(sh); shipments[sh.Code] = sh;
            var evIdx = 0;
            foreach (var e in Arr(s, "events"))
            {
                var ev = new ShipmentEvent { Id = Id("SHPEV", $"{sh.Code}:{evIdx++}"), ShipmentId = sh.Id, Type = Enum.Parse<ShipmentEventType>(S(e, "type", "Note"), true), OccurredAt = Utc(S(e, "at")), Note = SN(e, "note"), Location = SN(e, "location"), RecordedBy = supplierUserBySupplier.GetValueOrDefault(po.SupplierId, "system") };
                Stamp(ev, ev.OccurredAt); db.ShipmentEvents.Add(ev);
            }
        }
        foreach (var (lineId, code) in shipmentRefByLine)
            if (shipments.TryGetValue(code, out var sh)) poLines.Values.First(l => l.Id == lineId).ShipmentId = sh.Id;
        counts["shipments"] = shipments.Count;

        // --- lots, lot documents, inspections, NCRs
        var lots = new Dictionary<string, MaterialLot>(StringComparer.OrdinalIgnoreCase);
        var inspections = 0; var ncrs = 0;
        foreach (var l in Arr(lotsJson, "lots"))
        {
            var part = parts[S(l, "part")];
            var sup = suppliers[S(l, "supplier")];
            var poLine = SN(l, "poLine") is { } pl ? poLines[pl] : null;
            var status = Enum.Parse<MaterialLotStatus>(S(l, "status", "Accepted"), true);
            var lot = new MaterialLot
            {
                Id = Id("LOT", S(l, "lotNumber")), LotNumber = S(l, "lotNumber"), HeatNumber = SN(l, "heatNumber"), PartId = part.Id, SupplierId = sup.Id, PurchaseOrderLineId = poLine?.Id,
                SiteId = site0.Id, Quantity = M(l, "quantity"), RemainingQuantity = M(l, "remaining"), Unit = S(l, "unit", part.Unit), Status = status, ReceivedOn = DateN(SN(l, "receivedOn")), ProducedOn = DateN(SN(l, "producedOn")), ExpiresOn = DateN(SN(l, "expiresOn")),
                CountryOfOrigin = S(l, "country", sup.Country), BlockReason = SN(l, "blockReason"), BlockedAt = UtcN(SN(l, "blockedAt"))
            };
            Stamp(lot, lot.ReceivedOn is { } r ? clock.FromSiteLocal(r.ToDateTime(new TimeOnly(8, 0))) : now); db.MaterialLots.Add(lot); lots[lot.LotNumber] = lot;
            foreach (var d in Arr(l, "documents")) { db.QualityDocuments.Add(BuildDocument(d, sup, supplierUserBySupplier, null, lot.Id, lot.LotNumber, lot.HeatNumber)); docCount++; }
            foreach (var q in Arr(l, "inspections"))
            {
                var qi = new QualityInspection { Id = Id("QI", S(q, "code")), Code = S(q, "code"), MaterialLotId = lot.Id, Result = Enum.Parse<InspectionResult>(S(q, "result", "Passed"), true), InspectedBy = S(q, "by", "quality"), InspectedAt = Utc(S(q, "at")), Notes = SN(q, "notes") };
                Stamp(qi, qi.InspectedAt); db.QualityInspections.Add(qi); inspections++;
            }
            if (l["nonConformance"] is { } n)
            {
                var ncr = new NonConformance { Id = Id("NCR", S(n, "code")), Code = S(n, "code"), Title = S(n, "title"), Description = S(n, "description"), Status = Enum.Parse<NonConformanceStatus>(S(n, "status", "Open"), true), MaterialLotId = lot.Id, SupplierId = sup.Id, RaisedBy = S(n, "raisedBy", "quality"), RaisedAt = Utc(S(n, "raisedAt")) };
                Stamp(ncr, ncr.RaisedAt); db.NonConformances.Add(ncr); ncrs++;
            }
        }
        counts["lots"] = lots.Count; counts["documents"] = docCount; counts["nonConformances"] = ncrs;

        // --- logistics events
        var lre = 0;
        foreach (var e in Arr(lotsJson, "logisticsEvents"))
        {
            var shipment = SN(e, "shipment") is { } sc ? shipments[sc] : null;
            var sup = SN(e, "supplier") is { } suc ? suppliers[suc] : (shipment is not null ? suppliers.Values.First(s => s.Id == shipment.SupplierId) : null);
            var ev = new LogisticsRiskEvent { Id = Id("LRE", S(e, "code")), Code = S(e, "code"), Type = Enum.Parse<LogisticsEventType>(S(e, "type"), true), Severity = Enum.Parse<EventSeverity>(S(e, "severity", "MEDIUM"), true), SupplierId = sup?.Id, ShipmentId = shipment?.Id, Region = SN(e, "region") ?? sup?.Country, Description = S(e, "description"), StartedAt = Utc(S(e, "startedAt")), ResolvedAt = UtcN(SN(e, "resolvedAt")) };
            Stamp(ev, ev.StartedAt); db.LogisticsRiskEvents.Add(ev); lre++;
        }
        counts["logisticsEvents"] = lre;

        // --- production orders: fixture (active orders) + production-orders.json (status, serials, reservations, WO-2026-011)
        var orderMeta = Arr(ordersJson, "orders").ToDictionary(o => S(o, "code"));
        var orders = new Dictionary<string, ProductionOrder>();
        var opDefs = new Dictionary<string, OperationDefinition>(StringComparer.OrdinalIgnoreCase);
        var baseline = new PlanningBaseline
        {
            Id = Id("BASELINE", $"{site0.Code}:v1"), SiteId = site0.Id, Version = 1, Status = PlanningBaselineStatus.Active, HorizonStart = Date(S(baselineJson, "horizonStart")), HorizonEnd = Date(S(baselineJson, "horizonEnd")),
            ApprovedBy = "planner", ApprovedAt = clock.T0Utc.AddDays(-3), Notes = S(orgJson["baseline"], "notes", "Plan bazowy (seed)")
        };
        Stamp(baseline, baseline.ApprovedAt); db.PlanningBaselines.Add(baseline);
        var scheduled = 0;

        void AddOrder(string code, string productCode, int priority, int quantity, DateOnly due, DateOnly release, bool frozen, string? lineCode, IEnumerable<(int Seq, string Wc, double Hours, bool Frozen, DateTime Start, DateTime End, List<MaterialRequirement> Reqs, OperationStatus Status)> ops, ProductionOrderStatus status, JsonNode? meta)
        {
            var prod = products[productCode];
            var order = new ProductionOrder
            {
                Id = Id("WO", code), Code = code, ProductId = prod.Id, BomVersionId = boms[productCode].Id, SiteId = site0.Id, AssemblyLineId = lineCode is null ? null : lines[lineCode].Id,
                Quantity = quantity, Priority = priority, ReleaseDate = release, DueDate = due, Status = status, Frozen = frozen, CustomerReference = SN(meta, "customerReference")
            };
            Stamp(order, clock.FromSiteLocal(release.ToDateTime(new TimeOnly(6, 0))).AddDays(-7)); db.ProductionOrders.Add(order); orders[code] = order;
            foreach (var op in ops)
            {
                var names = routing[productCode].GetValueOrDefault(op.Seq, (Pl: "Operacja", En: "Operation", Wc: op.Wc));
                var def = new OperationDefinition
                {
                    Id = Id("OP", $"{code}/{op.Seq}"), ProductionOrderId = order.Id, Code = $"{code}/{op.Seq}", Sequence = op.Seq, NamePl = names.Pl, NameEn = names.En, WorkCenterId = workCenters[op.Wc].Id,
                    DurationHours = op.Hours, Frozen = op.Frozen, Status = op.Status, MaterialRequirementsJson = Json.Serialize(op.Reqs)
                };
                Stamp(def, order.CreatedAt); db.OperationDefinitions.Add(def); opDefs[def.Code] = def;
                var so = new ScheduledOperation { Id = Id("SOP", $"{baseline.Id}:{def.Code}"), PlanningBaselineId = baseline.Id, OperationDefinitionId = def.Id, WorkCenterId = def.WorkCenterId, AssemblyLineId = order.AssemblyLineId, Start = op.Start, End = op.End, Frozen = op.Frozen };
                Stamp(so, baseline.CreatedAt); db.ScheduledOperations.Add(so); scheduled++;
            }
        }

        foreach (var o in Arr(baselineJson, "orders"))
        {
            var code = S(o, "code");
            var meta = orderMeta.GetValueOrDefault(code);
            var opStatus = (meta?["operationStatus"] as JsonObject)?.ToDictionary(k => int.Parse(k.Key), k => Enum.Parse<OperationStatus>(k.Value!.GetValue<string>(), true)) ?? new();
            var ops = Arr(o, "operations").Select(op => (
                Seq: I(op, "sequence"), Wc: S(op, "workCenterCode"), Hours: D(op, "durationHours"), Frozen: B(op, "frozen"),
                Start: clock.FromSiteLocal(ShiftFixture(DateTime.Parse(S(op, "baselineStart")))), End: clock.FromSiteLocal(ShiftFixture(DateTime.Parse(S(op, "baselineEnd")))),
                Reqs: Arr(op, "materialRequirements").Select(r => new MaterialRequirement { PartCode = S(r, "partCode"), Quantity = M(r, "quantity") }).ToList(),
                Status: opStatus.GetValueOrDefault(I(op, "sequence"), OperationStatus.Planned))).ToList();
            AddOrder(code, S(o, "productCode"), I(o, "priority", 3), I(o, "quantity"), ShiftFixture(DateOnly.Parse(S(o, "dueDate"))), ShiftFixture(DateOnly.Parse(S(o, "releaseDate"))), B(o, "frozen"),
                SN(meta, "line") ?? SN(o, "lineCode"), ops, Enum.Parse<ProductionOrderStatus>(S(meta, "status", "Planned"), true), meta);
        }
        foreach (var (code, meta) in orderMeta.Where(kv => !orders.ContainsKey(kv.Key)))
        {
            var ops = Arr(meta, "operations").Select(op => (
                Seq: I(op, "seq"), Wc: S(op, "wc"), Hours: D(op, "hours"), Frozen: true, Start: Utc(S(op, "start")), End: Utc(S(op, "end")),
                Reqs: Arr(op, "materials").Select(r => new MaterialRequirement { PartCode = S(r, "part"), Quantity = M(r, "qty") }).ToList(),
                Status: Enum.Parse<OperationStatus>(S(op, "status", "Completed"), true))).ToList();
            AddOrder(code, S(meta, "product"), I(meta, "priority", 3), I(meta, "quantity"), Date(S(meta, "dueDate")), Date(S(meta, "releaseDate")), B(meta, "frozen"), SN(meta, "line"), ops,
                Enum.Parse<ProductionOrderStatus>(S(meta, "status", "Completed"), true), meta);
        }
        counts["productionOrders"] = orders.Count; counts["scheduledOperations"] = scheduled;

        // --- reservations, serials, consumptions
        var serials = new Dictionary<string, ProductSerial>();
        var reservations = 0; var consumptions = 0;
        foreach (var (code, meta) in orderMeta)
        {
            var order = orders[code];
            foreach (var r in Arr(meta, "reservations"))
            {
                var lot = SN(r, "lot") is { } ln ? lots[ln] : null;
                var res = new Reservation { Id = Id("RES", $"{code}:{S(r, "part")}:{SN(r, "lot")}"), PartId = parts[S(r, "part")].Id, ProductionOrderId = order.Id, MaterialLotId = lot?.Id, Quantity = M(r, "qty"), IsBlocked = lot?.Status is MaterialLotStatus.Blocked or MaterialLotStatus.Recalled };
                Stamp(res, order.CreatedAt); db.Reservations.Add(res); reservations++;
            }
            foreach (var s in Arr(meta, "serials"))
            {
                var serial = new ProductSerial { Id = Id("SER", S(s, "serial")), SerialNumber = S(s, "serial"), ProductId = order.ProductId, ProductionOrderId = order.Id, BomVersionId = order.BomVersionId, Status = Enum.Parse<SerialStatus>(S(s, "status", "Planned"), true), CompletedAt = UtcN(SN(s, "completedAt")) };
                Stamp(serial, order.CreatedAt); db.ProductSerials.Add(serial); serials[serial.SerialNumber] = serial;
            }
            var cIdx = 0;
            foreach (var c in Arr(meta, "consumptions"))
            {
                var mc = new MaterialConsumption
                {
                    Id = Id("CONS", $"{code}:{cIdx++}"), ProductionOrderId = order.Id, OperationDefinitionId = opDefs.GetValueOrDefault($"{code}/{I(c, "opSeq")}")?.Id, MaterialLotId = lots[S(c, "lot")].Id,
                    ProductSerialId = SN(c, "serial") is { } sn ? serials[sn].Id : null, Quantity = M(c, "qty"), ConsumedAt = Utc(S(c, "at")), RecordedBy = "operator.demo"
                };
                Stamp(mc, mc.ConsumedAt); db.MaterialConsumptions.Add(mc); consumptions++;
            }
        }
        counts["reservations"] = reservations; counts["serials"] = serials.Count; counts["consumptions"] = consumptions;

        // --- inventory balances (denormalised view of lots + reservations)
        var reservedByPart = db.ChangeTracker.Entries<Reservation>().Select(e => e.Entity).Where(r => !r.IsBlocked).GroupBy(r => r.PartId).ToDictionary(g => g.Key, g => g.Sum(r => r.Quantity));
        foreach (var part in parts.Values)
        {
            var pl = lots.Values.Where(l => l.PartId == part.Id).ToList();
            var bal = new InventoryBalance { Id = Id("INV", $"{site0.Code}:{part.Code}"), PartId = part.Id, SiteId = site0.Id, OnHand = pl.Where(l => l.Status is MaterialLotStatus.Accepted or MaterialLotStatus.ConditionallyReleased).Sum(l => l.RemainingQuantity), Blocked = pl.Where(l => l.Status is MaterialLotStatus.Blocked or MaterialLotStatus.Recalled).Sum(l => l.RemainingQuantity), Reserved = reservedByPart.GetValueOrDefault(part.Id, 0) };
            Stamp(bal); db.InventoryBalances.Add(bal);
        }

        // --- passport template, serial inspections, passports
        var tplNode = qualityJson["passportTemplate"];
        var template = new PassportTemplate { Id = Id("PTPL", S(tplNode, "code", "DQP-01")), Code = S(tplNode, "code", "DQP-01"), Name = S(tplNode, "name"), Description = S(tplNode, "description"), IsDemo = true };
        Stamp(template); db.PassportTemplates.Add(template);
        var rseq = 0;
        foreach (var r in Arr(tplNode, "requirements"))
        {
            var req = new QualityRequirement { Id = Id("QREQ", $"{template.Code}:{S(r, "code")}"), PassportTemplateId = template.Id, Code = S(r, "code"), TitlePl = S(r, "titlePl"), TitleEn = S(r, "titleEn"), Sequence = ++rseq, Mandatory = B(r, "mandatory", true) };
            Stamp(req); db.QualityRequirements.Add(req);
        }
        foreach (var q in Arr(qualityJson, "serialInspections"))
        {
            var qi = new QualityInspection { Id = Id("QI", S(q, "code")), Code = S(q, "code"), ProductSerialId = serials[S(q, "serial")].Id, Result = Enum.Parse<InspectionResult>(S(q, "result", "Passed"), true), InspectedBy = S(q, "by", "quality"), InspectedAt = Utc(S(q, "at")), Notes = SN(q, "notes") };
            Stamp(qi, qi.InspectedAt); db.QualityInspections.Add(qi); inspections++;
        }
        var passports = 0;
        foreach (var p in Arr(qualityJson, "passports"))
        {
            var serial = serials[S(p, "serial")];
            var pp = new Passport
            {
                Id = Id("PASS", serial.SerialNumber), ProductSerialId = serial.Id, PassportTemplateId = template.Id, Status = SeedPassportStatus(p),
                ApprovedBy = SN(p, "approvedBy"), ApprovedAt = UtcN(SN(p, "approvedAt")), CurrentVersion = 0, DeviationsJson = (p["deviations"] as JsonArray)?.ToJsonString() ?? "[]"
            };
            Stamp(pp, serial.CreatedAt); db.Passports.Add(pp); passports++;
        }
        counts["inspections"] = inspections; counts["passports"] = passports;

        // --- seeded notifications
        var nCount = 0;
        foreach (var n in Arr(qualityJson, "notifications"))
        {
            var note = new Notification { Id = Id("NOTIF", $"{S(n, "titleKey")}:{nCount}"), TargetRole = SN(n, "targetRole") is { } tr ? Enum.Parse<Role>(tr, true) : null, Severity = Enum.Parse<NotificationSeverity>(S(n, "severity", "Info"), true), TitleKey = S(n, "titleKey"), MessageKey = S(n, "messageKey"), ParamsJson = (n["params"] as JsonObject)?.ToJsonString() ?? "{}", Route = SN(n, "route"), IsRead = false };
            Stamp(note, Utc(S(n, "createdAt"))); db.Notifications.Add(note); nCount++;
        }
        counts["notifications"] = nCount;

        await db.SaveChangesAsync(ct);

        // --- rule-based risk for every open line (no events at seed), baseline KPI snapshot, audit
        await SeedPlantsAsync(dir, org, sites, suppliers, parts, products, boms, routing, supplierUserBySupplier, template, counts, ct);
        await db.SaveChangesAsync(ct);

        var scored = await risk.RecalculateAffectedAsync(null, "Seed", ct, raiseEvents: false);
        counts["riskAssessments"] = scored;
        foreach (var s in sites.Values)
        {
            var b = await db.PlanningBaselines.FirstAsync(x => x.SiteId == s.Id && x.Status == PlanningBaselineStatus.Active, ct);
            var eval = await impact.EvaluateAsync(s.Id, null, ct);
            b.KpiJson = Json.Serialize(eval.Evaluation.Kpi);
        }
        db.AuditEvents.Add(new AuditEvent { OccurredAt = clock.UtcNow, UserName = "system", Action = "Demo.Seed", Entity = "Demo", EntityCode = SeedVersion, AfterJson = Json.Serialize(new { seedVersion = SeedVersion, t0 = clock.T0Date, counts }), CorrelationId = "seed", Source = AuditSource.Seed });
        await db.SaveChangesAsync(ct);
    }

    private QualityDocument BuildDocument(JsonNode d, Supplier sup, Dictionary<Guid, string> supplierUserBySupplier, Guid? lineId, Guid? lotId, string? lotNumber, string? heatNumber)
    {
        var number = S(d, "number");
        var status = Enum.Parse<DocumentStatus>(S(d, "status", "Pending"), true);
        var issued = DateN(SN(d, "issuedOn"));
        var verifiedAt = UtcN(SN(d, "verifiedAt"));
        var uploadedAt = issued is { } i ? clock.FromSiteLocal(i.ToDateTime(new TimeOnly(12, 0))) : clock.T0Utc.AddDays(-1);
        var sha = Sha256Hex($"{number}:{S(d, "fileName")}");
        var doc = new QualityDocument
        {
            Id = Id("DOC", number), Type = Enum.Parse<DocumentType>(S(d, "type"), true), Status = status, DocumentNumber = number, FileName = S(d, "fileName"), ContentType = "application/pdf",
            SizeBytes = status == DocumentStatus.Missing ? 0 : 120_000 + (Math.Abs(number.GetHashCode()) % 300_000), Sha256 = status == DocumentStatus.Missing ? "" : sha,
            StorageKey = status == DocumentStatus.Missing ? "" : $"documents/{sup.Code}/{number}.pdf", IssuedOn = issued, PurchaseOrderLineId = lineId, MaterialLotId = lotId, SupplierId = sup.Id,
            LotNumber = lotNumber, HeatNumber = heatNumber, UploadedBy = supplierUserBySupplier.GetValueOrDefault(sup.Id, "supplier.portal"), UploadedAt = uploadedAt,
            VerifiedBy = SN(d, "verifiedBy"), VerifiedAt = verifiedAt, VerificationComment = SN(d, "comment"), Version = 1
        };
        Stamp(doc, uploadedAt);
        return doc;
    }
}
