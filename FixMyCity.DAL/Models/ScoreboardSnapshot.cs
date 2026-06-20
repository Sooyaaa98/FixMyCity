using System;
using System.Collections.Generic;

namespace FixMyCity.DAL.Models;

public partial class ScoreboardSnapshot
{
    public int SnapshotId { get; set; }
    public int UserId { get; set; }
    public string FullName { get; set; }   // denormalized for read performance
    public int? LocalityId { get; set; }
    public int Points { get; set; }
    public int Rank { get; set; }
    public DateTime SnapshotAt { get; set; }

    public User User { get; set; }
    public Locality Locality { get; set; }
}
