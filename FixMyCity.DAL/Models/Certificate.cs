namespace FixMyCity.DAL.Models;

public partial class Certificate
{
    public int CertificateId { get; set; }
    public int UserId { get; set; }

    // P7 fix — nullable FK; filtered unique indexes handle nullability in DB.
    public int? MilestoneId { get; set; }

    public string Milestone { get; set; }
    public string VerificationCode { get; set; }
    public DateTime IssuedAt { get; set; }
    public string FilePath { get; set; }

    // Navigation
    public User User { get; set; }
    public MilestoneDefinition Milestone_Nav { get; set; }
}
