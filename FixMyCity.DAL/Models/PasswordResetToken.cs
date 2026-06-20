using System;
using System.Collections.Generic;

namespace FixMyCity.DAL.Models;

public partial class PasswordResetToken
{
    public int TokenId { get; set; }
    public int UserId { get; set; }
    public string TokenHash { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UsedAt { get; set; }

    public User User { get; set; }
}
