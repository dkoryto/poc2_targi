using Dspc.Application.Abstractions;
using Dspc.Application.Common;
using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dspc.Application.Modules.Identity;

public sealed class DemoOptions
{
    public const string Section = "Demo";
    public bool Enabled { get; set; }
    /// <summary>Optional ISO date; when set T0 = Monday of that week, otherwise current week.</summary>
    public string? ClockAnchor { get; set; }
    public string DefaultRole { get; set; } = "DemoPresenter";
}

public sealed record LoginRequest(string Username, string Password);
public sealed record UserContextDto(Guid Id, string Username, string DisplayName, string Role, Guid? SupplierId, string? SupplierCode, string? SupplierName, Guid SiteId, string SiteCode, string Locale, bool DemoMode);
public sealed record LoginResponse(string AccessToken, DateTime ExpiresAt, UserContextDto User);
public sealed record DemoAccountDto(string Username, string Role, string? SupplierCode, string? Description);

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(200);
    }
}

public sealed class IdentityService(IAppDbContext db, IPasswordHasher hasher, IJwtTokenIssuer issuer, IOptions<DemoOptions> demo, IAuditWriter audit, IDemoClock clock)
{
    public bool DemoEnabled => demo.Value.Enabled;

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await db.Users.Include(u => u.Supplier).FirstOrDefaultAsync(u => u.Username == request.Username.ToLowerInvariant() && u.IsActive, ct);
        if (user is null || !hasher.Verify(request.Password, user.PasswordHash))
            throw new ForbiddenException("Invalid username or password.");
        audit.Write("Login", "User", user.Username, user.Id, null, new { user.Role });
        return await IssueAsync(user, ct);
    }

    public async Task<LoginResponse> DemoLoginAsync(string? role, string? supplierCode, CancellationToken ct)
    {
        if (!DemoEnabled) throw new NotFoundException("Endpoint", "demo-login");
        var roleName = string.IsNullOrWhiteSpace(role) ? demo.Value.DefaultRole : role;
        if (!Enum.TryParse<Role>(roleName, true, out var r))
            throw new Common.ValidationException(new Dictionary<string, string[]> { ["role"] = [$"Unknown role '{roleName}'."] });
        var q = db.Users.Include(u => u.Supplier).Where(u => u.Role == r && u.IsActive);
        if (r == Role.SupplierUser)
            q = supplierCode is null ? q.OrderBy(u => u.Username) : q.Where(u => u.Supplier!.Code == supplierCode);
        var user = await q.OrderBy(u => u.Username).FirstOrDefaultAsync(ct) ?? throw new NotFoundException("User for role", roleName);
        audit.Write("DemoLogin", "User", user.Username, user.Id, null, new { user.Role }, AuditSource.Demo);
        return await IssueAsync(user, ct);
    }

    public async Task<IReadOnlyList<DemoAccountDto>> DemoAccountsAsync(CancellationToken ct)
    {
        if (!DemoEnabled) throw new NotFoundException("Endpoint", "demo-accounts");
        return await db.Users.AsNoTracking().Include(u => u.Supplier).Where(u => u.IsActive).OrderBy(u => u.Role).ThenBy(u => u.Username)
            .Select(u => new DemoAccountDto(u.Username, u.Role.ToString(), u.Supplier != null ? u.Supplier.Code : null, u.Description)).ToListAsync(ct);
    }

    public async Task<UserContextDto> MeAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().Include(u => u.Supplier).FirstOrDefaultAsync(u => u.Id == userId, ct) ?? throw new NotFoundException("User", userId.ToString());
        return await ToContextAsync(user, ct);
    }

    private async Task<LoginResponse> IssueAsync(User user, CancellationToken ct)
    {
        var token = issuer.Issue(user, user.Supplier);
        return new LoginResponse(token.AccessToken, token.ExpiresAt, await ToContextAsync(user, ct));
    }

    private async Task<UserContextDto> ToContextAsync(User user, CancellationToken ct)
    {
        var site = await db.Sites.AsNoTracking().FirstAsync(s => s.Id == user.SiteId, ct);
        return new UserContextDto(user.Id, user.Username, user.DisplayName, user.Role.ToString(), user.SupplierId, user.Supplier?.Code, user.Supplier?.Name, user.SiteId, site.Code, user.Locale, DemoEnabled);
    }
}
