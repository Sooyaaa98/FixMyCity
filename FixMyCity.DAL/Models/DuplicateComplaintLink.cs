using System;
using System.Collections.Generic;

namespace FixMyCity.DAL.Models;

public partial class DuplicateComplaintLink
{
    public int LinkId { get; set; }
    public int OriginalComplaintId { get; set; }
    public int LinkedComplaintId { get; set; }
    public int LinkedByUserId { get; set; }
    public DateTime LinkedAt { get; set; }

    public Complaint OriginalComplaint { get; set; }
    public Complaint LinkedComplaint { get; set; }
    public User LinkedByUser { get; set; }
}
