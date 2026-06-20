using System;
using System.Collections.Generic;

namespace FixMyCity.DAL.Models;

public partial class PointsLedger
{
    public int LedgerId { get; set; }
    public int UserId { get; set; }
    public int PointsDelta { get; set; }
    public string Reason { get; set; }   // 'ComplaintRated' | 'PWGProgressUpdate' | 'ManualAward' | 'CertificateMilestone' | 'ComplaintSubmitted' | 'Other'
    public int? RefComplaintId { get; set; }
    public int? RefMilestoneId { get; set; }
    public DateTime EarnedAt { get; set; }

    public User User { get; set; }
    public Complaint Complaint { get; set; }
    public MilestoneDefinition Milestone { get; set; }
}
