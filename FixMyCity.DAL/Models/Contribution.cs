namespace FixMyCity.DAL.Models;

public partial class Contribution
{
    public int ContributionId { get; set; }
    public int ComplaintId { get; set; }
    public int CitizenUserId { get; set; }
    public decimal Amount { get; set; }

    // F5 — NOT NULL; gateway reference required before insert.
    public string TransactionRef { get; set; }
    public string PaymentStatus { get; set; }   // 'Pending' | 'Success' | 'Failed' | 'Refunded'
    public string FailureReason { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime ContributedAt { get; set; }

    // Navigation
    public Complaint Complaint { get; set; }
    public User CitizenUser { get; set; }
}
