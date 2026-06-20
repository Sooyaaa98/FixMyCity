using System;
using System.Collections.Generic;

namespace FixMyCity.DAL.Models;

public partial class AuditLog
{
    public int AuditId { get; set; }
    public int ActorUserId { get; set; }
    public string ActionType { get; set; }   // 'UserDeactivated' | 'UserBanned' | 'UserDeleted' | 'SolverApproved' | 'SolverRejected' | 'PWGApproved' | 'PWGRejected' | 'ComplaintReassigned' | 'PWGReportActioned' | 'AccountAnonymized'
    public int? TargetUserId { get; set; }
    public int? TargetOrgId { get; set; }
    public int? TargetDeptId { get; set; }
    public int? TargetComplaintId { get; set; }
    public string Reason { get; set; }
    public DateTime CreatedAt { get; set; }

    public User Actor { get; set; }
    public User TargetUser { get; set; }
    public Organisation TargetOrg { get; set; }
    public Department TargetDept { get; set; }
    public Complaint TargetComplaint { get; set; }
}
