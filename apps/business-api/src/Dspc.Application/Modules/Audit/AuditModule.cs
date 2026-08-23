using System.Text;
using Dspc.Application.Abstractions;
using Dspc.Application.Common;
using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dspc.Application.Modules.Audit;

public sealed record AuditEventDto(long Id, DateTime OccurredAt, string User, string? Role, string Action, string Entity, string EntityCode, object? Before, object? After, string CorrelationId, string Source);
public sealed record AuditFilter(string? Entity, string? Code, string? User, DateTime? From, DateTime? To, int Page = 1, int PageSize = 50);

/// <summary>Adds append-only audit rows to the current unit of work.</summary>
public sealed class AuditWriter(IAppDbContext db, ICurrentUser user, IDemoClock clock) : IAuditWriter
{
    public void Write(string action, string entity, string entityCode, Guid? entityId, object? before, object? after, AuditSource source = AuditSource.Api)
    {
        db.AuditEvents.Add(new AuditEvent
        {
            OccurredAt = clock.UtcNow, UserName = user.IsAuthenticated ? user.Username : "system", UserRole = user.Role?.ToString(), Action = action, Entity = entity, EntityCode = entityCode, EntityId = entityId,
            BeforeJson = before is null ? null : Json.Serialize(before), AfterJson = after is null ? null : Json.Serialize(after), CorrelationId = user.CorrelationId, Source = source, IpAddress = user.IpAddress
        });
    }
}

public sealed class AuditQueries(IAppDbContext db)
{
    public async Task<ListResult<AuditEventDto>> ListAsync(AuditFilter f, CancellationToken ct)
    {
        var q = db.AuditEvents.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(f.Entity)) q = q.Where(a => a.Entity == f.Entity);
        if (!string.IsNullOrWhiteSpace(f.Code)) q = q.Where(a => a.EntityCode.Contains(f.Code));
        if (!string.IsNullOrWhiteSpace(f.User)) q = q.Where(a => a.UserName == f.User);
        if (f.From is { } from) q = q.Where(a => a.OccurredAt >= DateTime.SpecifyKind(from, DateTimeKind.Utc));
        if (f.To is { } to) q = q.Where(a => a.OccurredAt <= DateTime.SpecifyKind(to, DateTimeKind.Utc));
        var total = await q.CountAsync(ct);
        var page = Math.Max(1, f.Page); var size = Math.Clamp(f.PageSize, 1, 500);
        var list = await q.OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return ListResult.Of(list.Select(ToDto).ToList(), total);
    }

    public async Task<string> ExportCsvAsync(AuditFilter f, CancellationToken ct)
    {
        var res = await ListAsync(f with { PageSize = 500 }, ct);
        var sb = new StringBuilder("id;occurredAt;user;role;action;entity;entityCode;before;after;correlationId;source\n");
        foreach (var a in res.Items)
            sb.Append(a.Id).Append(';').Append(a.OccurredAt.ToString("O")).Append(';').Append(Csv(a.User)).Append(';').Append(Csv(a.Role)).Append(';').Append(Csv(a.Action)).Append(';').Append(Csv(a.Entity)).Append(';').Append(Csv(a.EntityCode)).Append(';')
              .Append(Csv(a.Before is null ? "" : Json.Serialize(a.Before))).Append(';').Append(Csv(a.After is null ? "" : Json.Serialize(a.After))).Append(';').Append(a.CorrelationId).Append(';').Append(a.Source).Append('\n');
        return sb.ToString();
    }

    private static string Csv(string? s) => s is null ? "" : "\"" + s.Replace("\"", "\"\"") + "\"";

    public static AuditEventDto ToDto(AuditEvent a) => new(a.Id, a.OccurredAt, a.UserName, a.UserRole, a.Action, a.Entity, a.EntityCode,
        a.BeforeJson is null ? null : Json.Deserialize<object>(a.BeforeJson), a.AfterJson is null ? null : Json.Deserialize<object>(a.AfterJson), a.CorrelationId, a.Source.ToString());
}
