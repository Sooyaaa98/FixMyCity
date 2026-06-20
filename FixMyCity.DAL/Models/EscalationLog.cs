using System;
using System.Collections.Generic;

namespace FixMyCity.DAL.Models;

public partial class EscalationLog
{
    public int EscalationId { get; set; }
    public int ComplaintId { get; set; }
    public string EscalationTrigger { get; set; }   // 'Auto' | 'Manual'
    public DateTime EscalatedAt { get; set; }
    public int? ActorUserId { get; set; }
    public int OriginalDeptId { get; set; }
    public int? ReassignedToDeptId { get; set; }
    public string Reason { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public Complaint Complaint { get; set; }
    public User Actor { get; set; }
    public Department OriginalDept { get; set; }
    public Department ReassignedToDept { get; set; }
}
