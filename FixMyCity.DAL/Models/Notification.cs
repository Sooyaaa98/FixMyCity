namespace FixMyCity.DAL.Models;

// F10 — NotificationType CHECK constraint added.
public partial class Notification
{
    public int NotificationId { get; set; }
    public int UserId { get; set; }
    public int? ComplaintId { get; set; }
    public string Message { get; set; }
    public bool IsRead { get; set; }

    // F10 — 'StatusChange' | 'NewAssignment' | 'Registration' | 'PWGDecision' | 'WeeklyDigest'
    public string NotificationType { get; set; }

    // 'InApp' | 'Push' | 'Email'
    public string Channel { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public User User { get; set; }
    public Complaint Complaint { get; set; }
}
