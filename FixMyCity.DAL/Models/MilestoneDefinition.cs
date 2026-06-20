using System;
using System.Collections.Generic;

namespace FixMyCity.DAL.Models;

public partial class MilestoneDefinition
{
    public MilestoneDefinition()
    {
        Certificates = new HashSet<Certificate>();
        PointsLedgers = new HashSet<PointsLedger>();
    }

    public int MilestoneId { get; set; }
    public string MilestoneName { get; set; }
    public int PointsThreshold { get; set; }
    public string Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<Certificate> Certificates { get; set; }
    public ICollection<PointsLedger> PointsLedgers { get; set; }
}
