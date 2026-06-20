using System;
using System.Collections.Generic;

namespace FixMyCity.DAL.Models;

public partial class PlatformStatsSnapshot
{
    public int SnapshotId { get; set; }

    public int TotalComplaints { get; set; }

    public int Submitted { get; set; }

    public int InProgress { get; set; }

    public int Resolved { get; set; }

    public int Rejected { get; set; }

    public int Reopened { get; set; }

    public int Escalated { get; set; }

    public int Linked { get; set; }

    public int ActiveUsers { get; set; }

    public DateOnly SnapshotDate { get; set; }

    public DateTime CreatedAt { get; set; }
}
