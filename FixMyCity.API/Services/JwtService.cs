// FixMyCity.API/Services/JwtService.cs
// Generates short-lived access tokens (15 min) and long-lived refresh tokens (7 days).
// Refresh tokens are stored as SHA-256 hashes in dbo.UserRefreshTokens.
// Access token claims: UserId, Email, RoleName, LocalityId, DeptId.
// These claims are read by controllers via HttpContext.User and injected into
// DbSessionContext so RLS sees the real caller (not SuperAdmin bypass).

using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace FixMyCity.API.Services;

public interface IJwtService
{
    string GenerateAccessToken(int userId, string email, string roleName,
                               int localityId, int? deptId);
    string GenerateRefreshToken();
    ClaimsPrincipal? ValidateAccessToken(string token);
    Task<bool> SaveRefreshTokenAsync(int userId, string rawToken, CancellationToken ct = default);
    Task<int?> ValidateRefreshTokenAsync(string rawToken, CancellationToken ct = default);
    Task RevokeRefreshTokenAsync(string rawToken, CancellationToken ct = default);
    Task RevokeAllUserTokensAsync(int userId, CancellationToken ct = default);
}

public class JwtService(IConfiguration config, ILogger<JwtService> logger) : IJwtService
{
    private readonly string _secret   = config["Jwt:Secret"]!;
    private readonly string _issuer   = config["Jwt:Issuer"]!;
    private readonly string _audience = config["Jwt:Audience"]!;
    private readonly int    _accessMinutes  = int.Parse(config["Jwt:AccessTokenMinutes"]  ?? "15");
    private readonly int    _refreshDays    = int.Parse(config["Jwt:RefreshTokenDays"]    ?? "7");
    private readonly string _connStr = config.GetConnectionString("DefaultConnection")!;

    // ── Access token ──────────────────────────────────────────────────────────

    public string GenerateAccessToken(int userId, string email, string roleName,
                                      int localityId, int? deptId)
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,   userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            new(ClaimTypes.Role,               roleName),
            new("localityId",                  localityId.ToString()),
        };
        if (deptId.HasValue)
            claims.Add(new Claim("deptId", deptId.Value.ToString()));

        var token = new JwtSecurityToken(
            issuer:    _issuer,
            audience:  _audience,
            claims:    claims,
            notBefore: DateTime.UtcNow,
            expires:   DateTime.UtcNow.AddMinutes(_accessMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // ── Refresh token ─────────────────────────────────────────────────────────

    public string GenerateRefreshToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public ClaimsPrincipal? ValidateAccessToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var key     = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));

        try
        {
            return handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey         = key,
                ValidateIssuer           = true,
                ValidIssuer              = _issuer,
                ValidateAudience         = true,
                ValidAudience            = _audience,
                ValidateLifetime         = true,
                ClockSkew                = TimeSpan.Zero,
            }, out _);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Token validation failed: {msg}", ex.Message);
            return null;
        }
    }

    // ── DB operations ─────────────────────────────────────────────────────────

    public async Task<bool> SaveRefreshTokenAsync(int userId, string rawToken,
                                                   CancellationToken ct = default)
    {
        string hash    = HashToken(rawToken);
        var    expires = DateTime.UtcNow.AddDays(_refreshDays);

        await using var conn = new SqlConnection(_connStr);
        await conn.OpenAsync(ct);
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO dbo.UserRefreshTokens (UserId, TokenHash, ExpiresAt, CreatedAt)
            VALUES (@UserId, @Hash, @Exp, SYSUTCDATETIME())
            """;
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@Hash",   hash);
        cmd.Parameters.AddWithValue("@Exp",    expires);
        await cmd.ExecuteNonQueryAsync(ct);
        return true;
    }

    public async Task<int?> ValidateRefreshTokenAsync(string rawToken,
                                                       CancellationToken ct = default)
    {
        string hash = HashToken(rawToken);

        await using var conn = new SqlConnection(_connStr);
        await conn.OpenAsync(ct);
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            SELECT UserId FROM dbo.UserRefreshTokens
            WHERE  TokenHash = @Hash
              AND  ExpiresAt  > SYSUTCDATETIME()
              AND  RevokedAt  IS NULL
            """;
        cmd.Parameters.AddWithValue("@Hash", hash);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int uid ? uid : null;
    }

    public async Task RevokeRefreshTokenAsync(string rawToken,
                                               CancellationToken ct = default)
    {
        string hash = HashToken(rawToken);

        await using var conn = new SqlConnection(_connStr);
        await conn.OpenAsync(ct);
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE dbo.UserRefreshTokens
            SET    RevokedAt = SYSUTCDATETIME()
            WHERE  TokenHash = @Hash
            """;
        cmd.Parameters.AddWithValue("@Hash", hash);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task RevokeAllUserTokensAsync(int userId,
                                                CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_connStr);
        await conn.OpenAsync(ct);
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE dbo.UserRefreshTokens
            SET    RevokedAt = SYSUTCDATETIME()
            WHERE  UserId    = @UserId AND RevokedAt IS NULL
            """;
        cmd.Parameters.AddWithValue("@UserId", userId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string HashToken(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }
}
