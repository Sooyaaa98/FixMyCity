namespace FixMyCity.DAL.DTOs;

public class PlatformStats
{
    public int TotalComplaints { get; set; }
    public int Submitted { get; set; }
    public int InProgress { get; set; }
    public int Resolved { get; set; }
    public int Rejected { get; set; }
    public int Reopened { get; set; }
    public int Escalated { get; set; }
    public int Linked { get; set; }
    public int ActiveUsers { get; set; }
    public int TotalCitizens { get; set; }
    public int TotalSolvers { get; set; }
    public int TotalPWG { get; set; }
    public int TotalAdmins { get; set; }
}