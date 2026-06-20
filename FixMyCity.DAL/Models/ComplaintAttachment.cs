using System;
using System.Collections.Generic;

namespace FixMyCity.DAL.Models;

public partial class ComplaintAttachment
{
    public int AttachmentId { get; set; }
    public int ComplaintId { get; set; }
    public int? TimelineId { get; set; }
    public int UploadedByUserId { get; set; }
    public string AttachmentType { get; set; }
    public string FilePath { get; set; }
    public string FileName { get; set; }
    public int? FileSizeKB { get; set; }
    public DateTime UploadedAt { get; set; }

    public Complaint Complaint { get; set; }
    public ComplaintTimeline Timeline { get; set; }
    public User UploadedBy { get; set; }
}
