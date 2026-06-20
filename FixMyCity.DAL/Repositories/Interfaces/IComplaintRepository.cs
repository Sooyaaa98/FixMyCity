using FixMyCity.DAL.Models;

namespace FixMyCity.DAL.Repositories.Interfaces;

/// <summary>
/// Data-access contract for the full complaint lifecycle:
/// submission, routing, status updates, timeline, attachments, duplicate detection,
/// map view, keyword search, ratings, re-open, and category lookups.
/// Sprint 2: LocalityId replaces Locality; attachments replace ImagePath/ResolutionImagePath;
/// map, duplicate linking, and lat/lng added.
/// </summary>
public interface IComplaintRepository
{
    // ── Submission ────────────────────────────────────────────────────────────

    /// <summary>
    /// Submits a new complaint, auto-routes it to the correct approved department,
    /// and optionally stores a submission photo in ComplaintAttachments (F1).
    /// Calls usp_SubmitComplaint. Returns the new ComplaintId; 0 on failure.
    /// US14, US48.
    /// </summary>
    int SubmitComplaint(int citizenUserId, short categoryId, string title,
                        string description, int localityId, string address,
                        string criticality, decimal? latitude = null,
                        decimal? longitude = null, string filePath = null,
                        string fileName = null, int? fileSizeKb = null);

    // ── Reads ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a complaint by ID with full navigation:
    /// Citizen, Department, Category, Locality, ComplaintRatings, Attachments, MlScore.
    /// Null if not found. US17.
    /// </summary>
    Complaint GetComplaintById(int complaintId);

    /// <summary>
    /// Returns all complaints submitted by a citizen, newest first.
    /// Includes Category, Department, Locality. US16.
    /// </summary>
    List<Complaint> GetComplaintsByCitizen(int citizenUserId);

    /// <summary>
    /// Filters a citizen's complaints by optional status and localityId.
    /// Pass null/0 for "all". US18.
    /// </summary>
    List<Complaint> FilterComplaintsByCitizen(int citizenUserId,
                                               string status, int? localityId);

    /// <summary>
    /// Returns complaints assigned to a department with optional status,
    /// localityId, and criticality filters. Ordered by ML priority score desc.
    /// US38, US39.
    /// </summary>
    List<Complaint> GetComplaintsByDept(int deptId, string status,
                                         int? localityId, string criticality);

    /// <summary>
    /// Returns all active (not Resolved/Rejected/Linked) complaints in a locality,
    /// newest first. Includes Category, Department, Citizen. US21 home-page feed.
    /// </summary>
    List<Complaint> GetLocalityFeed(int localityId);

    /// <summary>
    /// Returns complaints with Latitude and Longitude set, optionally filtered by
    /// localityId. Used to populate the map view. US59.
    /// </summary>
    List<Complaint> GetMapComplaints(int? localityId);

    /// <summary>
    /// Searches complaints by keyword (Title/Description LIKE), optional categoryId,
    /// and optional localityId, newest first. US57.
    /// </summary>
    List<Complaint> SearchComplaints(string keyword, short? categoryId, int? localityId);

    // ── Status Updates ────────────────────────────────────────────────────────

    /// <summary>
    /// Updates complaint status and appends a timeline entry.
    /// Resolution photo stored in ComplaintAttachments (F1).
    /// Transition validated in DB against ComplaintStatusTransitions (F21).
    /// Calls usp_UpdateComplaintStatus. Returns true on success.
    /// US40, US60.
    /// </summary>
    bool UpdateComplaintStatus(int complaintId, int solverUserId, string newStatus,
                               string remark, string resolutionFilePath = null,
                               string resolutionFileName = null,
                               int? resolutionFileSizeKb = null);

    /// <summary>
    /// Sets the estimated resolution date and logs a timeline entry.
    /// Calls usp_SetEstimatedResolutionDate. Returns true on success. US41.
    /// </summary>
    bool SetEstimatedResolutionDate(int complaintId, int solverUserId, DateTime estDate);

    // ── Timeline ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the full timeline for a complaint, newest first.
    /// Each entry includes the Actor navigation. US17.
    /// </summary>
    List<ComplaintTimeline> GetTimeline(int complaintId);

    // ── Ratings & Re-open ─────────────────────────────────────────────────────

    /// <summary>
    /// Submits a 1–5 star rating for a resolved complaint and awards 1 point.
    /// Calls usp_RateComplaint. Returns true on success. US19.
    /// </summary>
    bool RateComplaint(int complaintId, int citizenUserId, byte stars, string comment);

    /// <summary>
    /// Returns the existing rating for a complaint/citizen pair.
    /// Null if not yet rated. Used to enforce one-rating-per-complaint. US19.
    /// </summary>
    ComplaintRating GetRating(int complaintId, int citizenUserId);

    /// <summary>
    /// Re-opens a resolved complaint after a below-3-star rating.
    /// F17 DB guard enforced inside SP. Calls usp_ReopenComplaint.
    /// Returns true on success. US20.
    /// </summary>
    bool ReopenComplaint(int complaintId, int citizenUserId, string reason);

    // ── Attachments ───────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all attachments for a complaint, optionally filtered by type
    /// ('Complaint' | 'Resolution' | 'PWGProgress' | 'Evidence').
    /// Includes UploadedBy navigation. US61, US62.
    /// </summary>
    List<ComplaintAttachment> GetAttachments(int complaintId, string attachmentType = null);

    /// <summary>
    /// Adds an attachment to ComplaintAttachments (F1 — sole file store).
    /// Calls usp_AddComplaintAttachment. Returns the new AttachmentId; 0 on failure.
    /// US60, US62.
    /// </summary>
    int AddAttachment(int complaintId, int uploadedByUserId, string attachmentType,
                      string filePath, string fileName = null, int? fileSizeKb = null,
                      int? timelineId = null);

    // ── Duplicate Detection ───────────────────────────────────────────────────

    /// <summary>
    /// Links a duplicate complaint to its original and sets its status to 'Linked'.
    /// Calls usp_LinkDuplicateComplaint. Returns true on success. US49.
    /// </summary>
    bool LinkDuplicateComplaint(int originalComplaintId, int linkedComplaintId,
                                int linkedByUserId);

    /// <summary>
    /// Returns the top 5 open complaints in the same locality and category
    /// to suggest as potential duplicates before submission. US49.
    /// </summary>
    List<Complaint> GetCandidateDuplicates(int localityId, short categoryId,
                                            int excludeComplaintId = 0);

    // ── Lookup ────────────────────────────────────────────────────────────────

    /// <summary>Returns all issue categories ordered by name.</summary>
    List<IssueCategory> GetAllCategories();
}
