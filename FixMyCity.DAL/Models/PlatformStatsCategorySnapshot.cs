using System;

namespace FixMyCity.DAL.Models;

/// <summary>
/// Per-category extension of <see cref="PlatformStatsSnapshot"/>. Composite key
/// (SnapshotId, CategoryId). Used by the Prophet per-category trend forecast.
/// </summary>
public partial class PlatformStatsCategorySnapshot
{
    public int   SnapshotId    { get; set; }
    public short CategoryId    { get; set; }
    public int   NewComplaints { get; set; }
    public int   Resolved      { get; set; }
    public int   InProgress    { get; set; }

    public IssueCategory Category { get; set; }
}
