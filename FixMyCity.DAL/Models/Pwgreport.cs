using System;
using System.Collections.Generic;

namespace FixMyCity.DAL.Models;

public partial class Pwgreport
{
    public int ReportId { get; set; }
    public int ComplaintId { get; set; }
    public int ReportedOrgId { get; set; }
    public int ReportedByUserId { get; set; }
    public string ReportReason { get; set; }
    public int? AdminReviewedByUserId { get; set; }
    public string AdminAction { get; set; }   // 'Warned' | 'Suspended' | 'Removed' | 'Dismissed'
    public string AdminNote { get; set; }
    public DateTime ReportedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string Status { get; set; }   // 'Pending' | 'Reviewed' | 'Closed'

    public Complaint Complaint { get; set; }
    public Organisation ReportedOrg { get; set; }
    public User ReportedByUser { get; set; }
    public User AdminReviewedBy { get; set; }
}
