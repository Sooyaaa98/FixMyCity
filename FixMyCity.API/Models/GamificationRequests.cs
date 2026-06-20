namespace FixMyCity.API.Models
{
    public class SendNotificationRequest
    {
        public int     UserId           { get; set; }
        public int?    ComplaintId      { get; set; }
        public string  Message          { get; set; }
        public string  NotificationType { get; set; }   // StatusChange / NewAssignment / PWGDecision / WeeklyDigest
        public string  Channel          { get; set; }   // InApp / Push / Email — defaults to InApp
    }

    public class AwardPointsRequest
    {
        public int    UserId         { get; set; }
        public int    Points         { get; set; }
        public string Reason         { get; set; }   // ComplaintRated / PWGProgressUpdate / ManualAward / etc.
        public int?   RefComplaintId { get; set; }
        public int?   RefMilestoneId { get; set; }
    }

    public class IssueCertificateRequest
    {
        public int    UserId      { get; set; }
        public string Milestone   { get; set; }
        public string FilePath    { get; set; }
        public int?   MilestoneId { get; set; }
    }

    public class UpdateNotificationPrefsRequest
    {
        public int  UserId              { get; set; }
        public bool InAppEnabled        { get; set; }
        public bool PushEnabled         { get; set; }
        public bool EmailDigestEnabled  { get; set; }
        public byte DigestFrequencyDays { get; set; }
    }

    public class ArchiveNotificationRequest
    {
        public int  UserId         { get; set; }
        public int? NotificationId { get; set; }   // null = archive all read for user
    }
}
