using System;
using System.Collections.Generic;

namespace FixMyCity.DAL.Models;

public partial class NotificationPreference
{
    public int PrefId { get; set; }
    public int UserId { get; set; }
    public bool InAppEnabled { get; set; }
    public bool PushEnabled { get; set; }
    public bool EmailDigestEnabled { get; set; }
    public byte DigestFrequencyDays { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User User { get; set; }
}
