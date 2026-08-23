using System.Security.Cryptography;
using System.Text;
using Dspc.Application.Abstractions;
using Dspc.Application.Common;
using Dspc.Domain.Entities;
using Dspc.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog.Context;

namespace Dspc.Api.Middleware;

/// <summary>Reads or creates X-Correlation-Id, echoes it back, pushes it into the log context.</summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string Header = "X-Correlation-Id";
    private const string ItemKey = "CorrelationId";

    public static string Get(HttpContext ctx) => ctx.Items.TryGetValue(ItemKey, out var v) && v is string s ? s : ctx.TraceIdentifier;

    public async Task InvokeAsync(HttpContext ctx)
    {
        var incoming = ctx.Request.Headers[Header].FirstOrDefault();
        var id = !string.IsNullOrWhiteSpace(incoming) && incoming.Length <= 64 && incoming.All(c => char.IsLetterOrDigit(c) || c is '-' or '_' or ':') ? incoming : Guid.NewGuid().ToString("N");
        ctx.Items[ItemKey] = id;
        ctx.Response.OnStarting(() => { ctx.Response.Headers[Header] = id; return Task.CompletedTask; });
        using (LogContext.PushProperty("CorrelationId", id))
            await next(ctx);
    }
}

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        var h = ctx.Response.Headers;
        h["X-Content-Type-Options"] = "nosniff";
        h["X-Frame-Options"] = "DENY";
        h["Referrer-Policy"] = "no-referrer";
        h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        h["Cache-Control"] = ctx.Request.Path.StartsWithSegments("/swagger") ? "no-cache" : "no-store";
        if (!ctx.Request.Path.StartsWithSegments("/swagger"))
            h["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
        await next(ctx);
    }
}

/// <summary>Maps AppException → Problem Details; unexpected errors → generic 500 (no stack trace) + recent-errors buffer.</summary>
public sealed class ApiExceptionHandler(IProblemDetailsService problemDetails, IRecentErrors recentErrors, ILogger<ApiExceptionHandler> log) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        var correlationId = CorrelationIdMiddleware.Get(ctx);
        ProblemDetails pd;
        switch (ex)
        {
            case ValidationException v:
                pd = new ValidationProblemDetails(v.Errors) { Status = 400, Title = v.Title, Detail = v.Message };
                break;
            case UnprocessableException u:
                pd = new ProblemDetails { Status = 422, Title = u.Title, Detail = u.Message };
                // a dictionary payload becomes top-level extensions (e.g. `missing[]`, `requirements[]` — the shape the
                // web client reads); anything else keeps the original single `missing` key
                if (u.Payload is IReadOnlyDictionary<string, object?> parts)
                    foreach (var (key, value) in parts) pd.Extensions[key] = value;
                else if (u.Payload is not null) pd.Extensions["missing"] = u.Payload;
                break;
            case AppException a:
                pd = new ProblemDetails { Status = a.Status, Title = a.Title, Detail = a.Message };
                if (a.Errors is not null) pd.Extensions["errors"] = a.Errors;
                break;
            case DbUpdateConcurrencyException:
                pd = new ProblemDetails { Status = 412, Title = "Precondition failed", Detail = "The resource was modified by someone else. Reload and retry." };
                break;
            case BadHttpRequestException b:
                pd = new ProblemDetails { Status = b.StatusCode, Title = "Bad request", Detail = b.Message };
                break;
            case OperationCanceledException when ctx.RequestAborted.IsCancellationRequested:
                return true;
            default:
                log.LogError(ex, "Unhandled exception {CorrelationId}", correlationId);
                recentErrors.Record($"{ctx.Request.Method} {ctx.Request.Path}", ex.GetType().Name + ": " + ex.Message, correlationId);
                pd = new ProblemDetails { Status = 500, Title = "Internal error", Detail = "An unexpected error occurred. The error has been recorded; quote the correlation id when reporting it." };
                break;
        }
        pd.Type ??= $"https://httpstatuses.io/{pd.Status}";
        pd.Instance = ctx.Request.Path;
        ctx.Response.StatusCode = pd.Status ?? 500;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext { HttpContext = ctx, ProblemDetails = pd, Exception = ex });
    }
}

/// <summary>
/// Idempotency for POST/PATCH carrying an <c>Idempotency-Key</c>: the first response is stored for 24 h and replayed
/// for identical retries (same user, key and request body); a different body with the same key → 409.
/// </summary>
public sealed class IdempotencyMiddleware(RequestDelegate next)
{
    public const string Header = "Idempotency-Key";

    public async Task InvokeAsync(HttpContext ctx, AppDbContext db)
    {
        if (ctx.Request.Method is not ("POST" or "PATCH" or "PUT") || !ctx.Request.Headers.TryGetValue(Header, out var keyValues) || ctx.Request.HasFormContentType)
        { await next(ctx); return; }
        var rawKey = keyValues.ToString();
        if (string.IsNullOrWhiteSpace(rawKey) || rawKey.Length > 100) { await next(ctx); return; }

        ctx.Request.EnableBuffering();
        string body;
        using (var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8, leaveOpen: true)) { body = await reader.ReadToEndAsync(); ctx.Request.Body.Position = 0; }
        var user = ctx.User.Identity?.IsAuthenticated == true ? ctx.User.FindFirst("sub")?.Value ?? "anon" : "anon";
        var key = $"{user}:{rawKey}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ctx.Request.Method + " " + ctx.Request.Path + "\n" + body))).ToLowerInvariant();

        var existing = await db.IdempotencyRecords.AsNoTracking().FirstOrDefaultAsync(r => r.Key == key, ctx.RequestAborted);
        if (existing is not null && existing.CreatedAt > DateTime.UtcNow.AddHours(-24))
        {
            if (existing.RequestHash != hash)
            {
                ctx.Response.StatusCode = 409;
                ctx.Response.ContentType = "application/problem+json";
                await ctx.Response.WriteAsync("{\"type\":\"https://httpstatuses.io/409\",\"title\":\"Idempotency key reuse\",\"status\":409,\"detail\":\"The Idempotency-Key was already used with a different request.\"}");
                return;
            }
            ctx.Response.StatusCode = existing.StatusCode;
            ctx.Response.ContentType = "application/json";
            ctx.Response.Headers["Idempotent-Replayed"] = "true";
            await ctx.Response.WriteAsync(existing.ResponseBody);
            return;
        }

        var original = ctx.Response.Body;
        using var buffer = new MemoryStream();
        ctx.Response.Body = buffer;
        try
        {
            await next(ctx);
            buffer.Position = 0;
            var responseBody = await new StreamReader(buffer, Encoding.UTF8).ReadToEndAsync();
            buffer.Position = 0;
            await buffer.CopyToAsync(original);
            if (ctx.Response.StatusCode < 500 && (ctx.Response.ContentType ?? "").Contains("json") && responseBody.Length < 512 * 1024)
            {
                try
                {
                    db.IdempotencyRecords.Add(new IdempotencyRecord { Key = key, RequestHash = hash, StatusCode = ctx.Response.StatusCode, ResponseBody = responseBody, CreatedAt = DateTime.UtcNow });
                    await db.SaveChangesAsync(CancellationToken.None);
                }
                catch (DbUpdateException) { /* concurrent same-key insert – first one wins */ }
            }
        }
        finally { ctx.Response.Body = original; }
    }
}

/// <summary>Validates the endpoint's body argument with its FluentValidation validator → 400 Problem Details.</summary>
public sealed class ValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var validator = ctx.HttpContext.RequestServices.GetService<FluentValidation.IValidator<T>>();
        var arg = ctx.Arguments.OfType<T>().FirstOrDefault();
        if (validator is not null && arg is not null)
        {
            var result = await validator.ValidateAsync(arg, ctx.HttpContext.RequestAborted);
            if (!result.IsValid)
                throw new ValidationException(ValidationErrors.ToProblemDetails(result.Errors));
        }
        return await next(ctx);
    }
}
