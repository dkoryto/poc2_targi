using System.Net.Http.Json;
using System.Text.Json;
using Dspc.Application.Abstractions;
using Dspc.Application.Common;
using Dspc.Application.Modules.Admin;
using Dspc.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dspc.Application.Modules.Documents;

public sealed record AiFieldSource(int? Page, string? Snippet);
public sealed record AiField(string Name, string? Value, double Confidence, AiFieldSource Source);
public sealed record AiIssue(string Code, string Message, string Severity);

/// <summary>Always a proposal: a human accepts or rejects it through <c>POST /documents/{id}/verify</c>.</summary>
public sealed record AiExtractionResult(
    IReadOnlyList<AiField> Fields, string SuggestedType, IReadOnlyList<AiIssue> Issues,
    string Status, string Model, bool Simulated, string Disclaimer);

/// <summary>
/// Optional local-LLM assistance for certificates, behind <c>LocalAi:Enabled</c>. The document never leaves the local
/// environment: the adapter talks to an OpenAI-compatible endpoint on the local network only. Results are proposals
/// with per-field confidence and source, never used for MRP maths, authorization or quality decisions. When the
/// endpoint is unreachable (or <c>LocalAi:Simulator</c> is set) a deterministic simulated answer keeps the demo stable.
/// </summary>
public sealed class AiExtractionService(
    IAppDbContext db, ISupplierScope scope, IOptions<LocalAiOptions> options, IHttpClientFactory http,
    IDemoClock clock, IAuditWriter audit, ILogger<AiExtractionService> log)
{
    public const string DisclaimerText = "Propozycja lokalnego modelu — wymaga akceptacji kontrolera jakości. Nie jest podstawą decyzji jakościowej ani planistycznej.";

    public bool Enabled => options.Value.Enabled;

    public async Task<AiExtractionResult> ExtractAsync(Guid documentId, CancellationToken ct)
    {
        var doc = await scope.Apply(db.QualityDocuments)
            .Include(d => d.MaterialLot).ThenInclude(l => l!.Part)
            .Include(d => d.MaterialLot).ThenInclude(l => l!.Supplier)
            .FirstOrDefaultAsync(d => d.Id == documentId, ct)
            ?? throw new NotFoundException("QualityDocument", documentId.ToString());

        var result = options.Value.Simulator ? Simulate(doc, "simulator") : await CallModelAsync(doc, ct);
        doc.AiSuggestionJson = Json.Serialize(result);
        doc.UpdatedAt = clock.UtcNow;
        audit.Write("Document.AiExtract", "QualityDocument", doc.DocumentNumber, doc.Id, null,
            new { result.SuggestedType, result.Model, result.Simulated, Fields = result.Fields.Count });
        await db.SaveChangesAsync(ct);
        return result;
    }

    private async Task<AiExtractionResult> CallModelAsync(Domain.Entities.QualityDocument doc, CancellationToken ct)
    {
        try
        {
            var client = http.CreateClient("local-ai");
            client.Timeout = TimeSpan.FromSeconds(20);
            var prompt = $"Extract certificate fields as JSON (documentNumber, lotNumber, heatNumber, issuedOn, material, standard). Document: {doc.FileName}, number {doc.DocumentNumber}, lot {doc.LotNumber}.";
            var payload = new
            {
                model = options.Value.Model ?? "local",
                messages = new object[] { new { role = "system", content = "You extract fields from quality certificates. Answer with compact JSON only." }, new { role = "user", content = prompt } },
                temperature = 0
            };
            var response = await client.PostAsJsonAsync("chat/completions", payload, ct);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            var content = body.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "{}";
            var fields = ParseFields(content);
            return new AiExtractionResult(fields, doc.Type.ToString(), CrossCheck(doc, fields), "Proposal", options.Value.Model ?? "local", false, DisclaimerText);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Local AI endpoint unavailable — falling back to the deterministic simulator");
            return Simulate(doc, "fallback-simulator");
        }
    }

    private static IReadOnlyList<AiField> ParseFields(string content)
    {
        try
        {
            var start = content.IndexOf('{');
            var end = content.LastIndexOf('}');
            if (start < 0 || end <= start) return [];
            using var parsed = JsonDocument.Parse(content[start..(end + 1)]);
            return parsed.RootElement.EnumerateObject()
                .Select(p => new AiField(p.Name, p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.ToString(), 0.72, new AiFieldSource(1, null)))
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>Deterministic stand-in so the stand demo always shows the same proposal.</summary>
    private static AiExtractionResult Simulate(Domain.Entities.QualityDocument doc, string model)
    {
        var lot = doc.MaterialLot;
        var fields = new List<AiField>
        {
            new("documentNumber", doc.DocumentNumber, 0.97, new AiFieldSource(1, $"Nr dokumentu: {doc.DocumentNumber}")),
            new("lotNumber", doc.LotNumber ?? lot?.LotNumber, 0.94, new AiFieldSource(1, $"Partia: {doc.LotNumber ?? lot?.LotNumber}")),
            new("heatNumber", doc.HeatNumber ?? lot?.HeatNumber, lot?.HeatNumber is null ? 0.41 : 0.9, new AiFieldSource(1, $"Wytop: {doc.HeatNumber ?? lot?.HeatNumber ?? "—"}")),
            new("issuedOn", doc.IssuedOn?.ToString("yyyy-MM-dd"), 0.88, new AiFieldSource(1, "Data wystawienia")),
            new("material", lot?.Part?.Code, lot?.Part is null ? 0.35 : 0.86, new AiFieldSource(1, $"Materiał: {lot?.Part?.NamePl}")),
            new("supplier", lot?.Supplier?.Name, lot?.Supplier is null ? 0.30 : 0.83, new AiFieldSource(1, $"Wytwórca: {lot?.Supplier?.Name}")),
            new("standard", doc.Type == DocumentType.MATERIAL_CERT ? "EN 10204 3.1" : null, doc.Type == DocumentType.MATERIAL_CERT ? 0.79 : 0.2, new AiFieldSource(2, "Norma"))
        };
        return new AiExtractionResult(fields, doc.Type.ToString(), CrossCheck(doc, fields), "Proposal", model, true, DisclaimerText);
    }

    /// <summary>Deterministic consistency checks between the proposal and the record — plain rules, not the model.</summary>
    private static IReadOnlyList<AiIssue> CrossCheck(Domain.Entities.QualityDocument doc, IReadOnlyList<AiField> fields)
    {
        var issues = new List<AiIssue>();
        var lotField = fields.FirstOrDefault(f => f.Name == "lotNumber")?.Value;
        var expectedLot = doc.LotNumber ?? doc.MaterialLot?.LotNumber;
        if (expectedLot is not null && lotField is not null && !string.Equals(expectedLot, lotField, StringComparison.OrdinalIgnoreCase))
            issues.Add(new AiIssue("LOT_MISMATCH", $"Numer partii w dokumencie ({lotField}) różni się od zarejestrowanego ({expectedLot}).", "High"));
        foreach (var f in fields.Where(f => f.Value is null || f.Confidence < 0.5))
            issues.Add(new AiIssue("LOW_CONFIDENCE", $"Pole '{f.Name}' wymaga ręcznego potwierdzenia.", "Low"));
        return issues;
    }
}
