namespace FixMyCity.API.Models
{
    public class PWGParticipationRequestModel
    {
        public int    ComplaintId { get; set; }
        public int    OrgId       { get; set; }
        public string RequestNote { get; set; }
    }

    public class PWGProgressUpdateRequest
    {
        public int    ComplaintId   { get; set; }
        public int    PWGUserId     { get; set; }
        public string ProgressNote  { get; set; }
        public string PhotoPath     { get; set; }
        public string PhotoFileName { get; set; }
        public int?   PhotoFileSizeKB { get; set; }
    }

    public class ResolvePWGRequestModel
    {
        public int    RequestId    { get; set; }
        public int    SolverUserId { get; set; }
        public string Decision     { get; set; }   // "Approved" or "Rejected"
        public string DecisionNote { get; set; }
    }

    public class UpdateOrgProfileRequest
    {
        public int    OrgId        { get; set; }
        public string OrgName      { get; set; }
        public string ContactEmail { get; set; }
        public string ContactPhone { get; set; }
        public string Address      { get; set; }
    }

    public class UpdateDeptProfileRequest
    {
        public int    DeptId       { get; set; }
        public string DeptName     { get; set; }
        public string Ministry     { get; set; }
        public string ContactEmail { get; set; }
        public string ContactPhone { get; set; }
        public string Address      { get; set; }
        public int    LocalityId   { get; set; }
    }

    public class FilePWGReportRequest
    {
        public int    ComplaintId     { get; set; }
        public int    ReportedOrgId   { get; set; }
        public int    ReportedByUserId{ get; set; }
        public string ReportReason    { get; set; }
    }
}
