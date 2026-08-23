using System.Security.Claims;
using System.Security.Cryptography;
using Dspc.Application.Abstractions;
using Dspc.Domain.Entities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;

namespace Dspc.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string Section = "Identity:Jwt";
    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "dspc";
    public string Audience { get; set; } = "dspc";
    public int LifetimeMinutes { get; set; } = 480;
}

public static class DspcClaims
{
    public const string Role = "role";
    public const string SupplierId = "supplier_id";
    public const string SupplierCode = "supplier_code";
    public const string SiteId = "site_id";
}

/// <summary>PBKDF2-SHA256, 100k iterations, format: pbkdf2$iterations$saltB64$hashB64.</summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int Iterations = 100_000;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"pbkdf2${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string stored)
    {
        var parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != "pbkdf2") return false;
        var iterations = int.Parse(parts[1]);
        var salt = Convert.FromBase64String(parts[2]);
        var expected = Convert.FromBase64String(parts[3]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}

public sealed class JwtTokenIssuer(IOptions<JwtOptions> options) : IJwtTokenIssuer
{
    public IssuedToken Issue(User user, Supplier? supplier)
    {
        var o = options.Value;
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(o.LifetimeMinutes);
        // Short claim names; the API validates with MapInboundClaims=false, NameClaimType="unique_name", RoleClaimType="role".
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(JwtRegisteredClaimNames.Name, user.DisplayName),
            new(DspcClaims.Role, user.Role.ToString()),
            new(DspcClaims.SiteId, user.SiteId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        if (user.SupplierId is { } sid) claims.Add(new Claim(DspcClaims.SupplierId, sid.ToString()));
        if (supplier is not null) claims.Add(new Claim(DspcClaims.SupplierCode, supplier.Code));
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = o.Issuer,
            Audience = o.Audience,
            NotBefore = now,
            Expires = expires,
            IssuedAt = now,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(o.Key)), SecurityAlgorithms.HmacSha256)
        };
        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new IssuedToken(token, expires);
    }
}
