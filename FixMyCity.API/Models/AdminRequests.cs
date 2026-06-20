namespace FixMyCity.API.Models
{
    public class DeptDecisionRequest
    {
        public int    UserId      { get; set; }   // Solver UserId being decided upon
        public string Decision    { get; set; }   // "Approved" or "Rejected"
        public int    AdminUserId { get; set; }
    }

    public class OrgDecisionRequest
    {
        public int    UserId      { get; set; }   // PWG UserId being decided upon
        public string Decision    { get; set; }   // "Approved" or "Rejected"
        public int    AdminUserId { get; set; }
    }

    public class DeactivateUserRequest
    {
        public int    TargetUserId { get; set; }
        public string Reason       { get; set; }
        public int    AdminUserId  { get; set; }
    }

    public class BanUserRequest
    {
        public int    TargetUserId { get; set; }
        public string Reason       { get; set; }
        public int    AdminUserId  { get; set; }
    }

    public class ManualEscalationRequest
    {
        public int    ComplaintId        { get; set; }
        public int    AdminUserId        { get; set; }
        public int?   ReassignedToDeptId { get; set; }
        public string Reason             { get; set; }
    }

    public class ReviewPWGReportRequest
    {
        public int    ReportId    { get; set; }
        public int    AdminUserId { get; set; }
        public string AdminAction { get; set; }   // Warned / Suspended / Removed / Dismissed
        public string AdminNote   { get; set; }
        public bool   FinalClose  { get; set; }
    }

    public class ClosePWGReportRequest
    {
        public int    ReportId    { get; set; }
        public int    AdminUserId { get; set; }
        public string CloseNote   { get; set; }
    }

    public class ReactivateUserRequest
    {
        public int    TargetUserId { get; set; }
        public string Reason       { get; set; }
        public int    AdminUserId  { get; set; }
    }
}
