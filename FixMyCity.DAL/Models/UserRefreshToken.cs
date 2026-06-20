using System;

namespace FixMyCity.DAL.Models;

/// <summary>
/// JWT refresh-token rotation table. Token stored as SHA-256 hex (CHAR(64)),
/// not the raw token. Created and revoked by
/// <see cref="FixMyCity.API.Services.JwtService"/>. Cascade-deletes when the
/// owning user is deleted.
/// </summary>
public partial class UserRefreshToken
{
    public int       TokenId   { get; set; }
    public int       UserId    { get; set; }
    public string    TokenHash { get; set; }
    public DateTime  ExpiresAt { get; set; }
    public DateTime  CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    public User User { get; set; }
}
