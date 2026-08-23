using Dspc.Application.Abstractions;
using Dspc.Application.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dspc.Application.Modules.Notifications;

// Shape per apps/web/src/api/types.ts (Notification): title/message are pre-rendered (PL), severity lower-case, `read`.
public sealed record NotificationDto(Guid Id, DateTime CreatedAt, string Title, string Message, string Severity, bool Read, string? Route, string? EventName, string TitleKey, string MessageKey, object? Params, string? TargetRole);

public static class NotificationTexts
{
    public static (string Title, string Message) Render(string titleKey, string messageKey, Dictionary<string, object?>? p)
    {
        string P(string k) => p is not null && p.TryGetValue(k, out var v) && v is not null ? v.ToString()! : "";
        return titleKey switch
        {
            "notifications.deliveryRisk.title" => ("Ryzyko dostawy: " + P("partCode"),
                $"Pozycja {P("poCode")}/{P("lineNo")} ({P("partCode")}) osiągnęła ryzyko {P("score")} ({P("category")}). Zagrożone zlecenia: {P("endangered")}."),
            "notifications.lotBlocked.title" => ("Partia zablokowana: " + P("lotNumber"), $"Partia {P("lotNumber")} została zablokowana. Dotknięte zlecenia: {P("orders")}; paszporty: {P("passports")}."),
            "notifications.scenarioCompleted.title" => ("Scenariusz przeliczony", $"Scenariusz „{P("name")}” zakończony ({P("solver")}, {P("elapsedMs")} ms)."),
            "notifications.planApproved.title" => ("Plan zatwierdzony", $"Nowa wersja planu bazowego v{P("version")} zatwierdzona przez {P("approvedBy")}."),
            "notifications.passportInvalidated.title" => ("Paszport unieważniony: " + P("serial"), $"Paszport {P("serial")} wymaga działania: {P("reason")}."),
            "notifications.documentRejected.title" => ("Dokument odrzucony", $"Dokument {P("documentNumber")} został odrzucony: {P("comment")}."),
            _ => (titleKey, messageKey)
        };
    }

    public static string Severity(Domain.Common.NotificationSeverity s) => s switch
    {
        Domain.Common.NotificationSeverity.Critical => "critical",
        Domain.Common.NotificationSeverity.Warning => "warn",
        _ => "info"
    };
}

public sealed class NotificationService(IAppDbContext db, ICurrentUser user, IDemoClock clock)
{
    /// <summary>Own + role-targeted + broadcast notifications; presenter and director see every role-targeted notification.</summary>
    private IQueryable<Notification> Mine() => user.Role is Domain.Common.Role.DemoPresenter or Domain.Common.Role.OperationsDirector
        ? db.Notifications.Where(n => n.UserId == null || n.UserId == user.Id)
        : db.Notifications.Where(n => (n.UserId != null && n.UserId == user.Id) || (n.TargetRole != null && n.TargetRole == user.Role) || (n.UserId == null && n.TargetRole == null));

    public async Task<ListResult<NotificationDto>> ListAsync(bool unreadOnly, CancellationToken ct)
    {
        var q = Mine().AsNoTracking();
        if (unreadOnly) q = q.Where(n => !n.IsRead);
        var list = await q.OrderByDescending(n => n.CreatedAt).Take(100).ToListAsync(ct);
        var total = await Mine().CountAsync(n => !n.IsRead, ct);
        return ListResult.Of(list.Select(ToDto).ToList(), total);
    }

    public static NotificationDto ToDto(Notification n)
    {
        var p = Json.Deserialize<Dictionary<string, object?>>(n.ParamsJson);
        var (title, message) = NotificationTexts.Render(n.TitleKey, n.MessageKey, p);
        var eventName = p is not null && p.TryGetValue("eventName", out var e) ? e?.ToString() : null;
        return new NotificationDto(n.Id, n.CreatedAt, title, message, NotificationTexts.Severity(n.Severity), n.IsRead, n.Route, eventName, n.TitleKey, n.MessageKey, p, n.TargetRole?.ToString());
    }

    public async Task MarkReadAsync(Guid id, CancellationToken ct)
    {
        var n = await Mine().FirstOrDefaultAsync(n => n.Id == id, ct) ?? throw new NotFoundException("Notification", id.ToString());
        if (!n.IsRead) { n.IsRead = true; n.ReadAt = clock.UtcNow; n.UpdatedAt = clock.UtcNow; await db.SaveChangesAsync(ct); }
    }

    public async Task MarkAllReadAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        foreach (var n in await Mine().Where(n => !n.IsRead).ToListAsync(ct)) { n.IsRead = true; n.ReadAt = now; n.UpdatedAt = now; }
        await db.SaveChangesAsync(ct);
    }
}
