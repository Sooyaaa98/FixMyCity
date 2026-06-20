namespace FixMyCity.DAL.DTOs;

// ─────────────────────────────────────────────────────────────────────────────
// Phase 8 — DTOs for the new feature wave (§5, §9, §17, §20).
// These are flat, read-only projections returned by the new stored procedures.
// They are marked HasNoKey in OnModelCreating so EF doesn't try to track them.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// §9 — one daily row from usp_GetComplaintTrend. Used by the admin analytics
/// dashboard to render a 30-day complaint volume chart.
/// </summary>
public class ComplaintTrendRow
{
    public DateTime Date     { get; set; }
    public int      Count    { get; set; }
    public int      Resolved { get; set; }
}

/// <summary>
/// §20 — one row from usp_GetActivityFeed. Unifies complaint, status, points,
/// certificate and comment events into a single time-ordered feed.
/// </summary>
public class ActivityFeedRow
{
    public string   EventType   { get; set; }   // ComplaintSubmitted | StatusChange | PointsAwarded | CertificateIssued | CommentPosted
    public string   Description { get; set; }
    public int?     RelatedId   { get; set; }
    public DateTime CreatedAt   { get; set; }
}

/// <summary>
/// §17 — one row from usp_GetPublicFeed. The transparency portal renders
/// these rows to anonymous (un-authenticated) visitors. Description is
/// LEFT()-truncated to 200 chars at the DB tier to prevent leaking
/// long-form PII through the public surface.
/// </summary>
public class PublicFeedRow
{
    public int       ComplaintId   { get; set; }
    public string    Title         { get; set; }
    public string    Description   { get; set; }
    public string    Status        { get; set; }
    public string    Criticality   { get; set; }
    public DateTime  SubmittedAt   { get; set; }
    public DateTime? ResolvedAt    { get; set; }
    public decimal?  Latitude      { get; set; }
    public decimal?  Longitude     { get; set; }
    public short?    CategoryId    { get; set; }
    public string    CategoryName  { get; set; }
    public int?      LocalityId    { get; set; }
    public string    LocalityName  { get; set; }
    public int?      DeptId        { get; set; }
    public string    DeptName      { get; set; }
    public int       UpvoteCount   { get; set; }
}

/// <summary>
/// §5 — one row from usp_GetNearbyComplaints. Adds a Haversine `DistanceKm`
/// to the standard complaint columns the map view uses.
/// </summary>
public class NearbyComplaintRow
{
    public int       ComplaintId   { get; set; }
    public string    Title         { get; set; }
    public string    Status        { get; set; }
    public string    Criticality   { get; set; }
    public decimal?  Latitude      { get; set; }
    public decimal?  Longitude     { get; set; }
    public DateTime  SubmittedAt   { get; set; }
    public short?    CategoryId    { get; set; }
    public string    CategoryName  { get; set; }
    public int?      LocalityId    { get; set; }
    public string    LocalityName  { get; set; }
    public double    DistanceKm    { get; set; }
}

/// <summary>
/// §1 — result row returned by usp_ToggleComplaintUpvote (count + has-upvoted flag).
/// </summary>
public class UpvoteResult
{
    public int  NewCount   { get; set; }
    public bool HasUpvoted { get; set; }
}
