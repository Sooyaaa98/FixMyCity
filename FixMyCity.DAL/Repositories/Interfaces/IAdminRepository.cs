using FixMyCity.DAL.Models;
using FixMyCity.DAL.DTOs;

namespace FixMyCity.DAL.Repositories.Interfaces;

/// <summary>
/// Data-access contract for all Super Admin operations:
/// registration approval, user management (deactivate/ban), escalation handling,
/// PWG report review, audit log, and platform statistics.
/// Sprint 2: Ban support (F7), typed escalation, PWG report review, audit log, snapshot added.
/// </summary>
public interface IAdminRepository
{
    // ── Registration Approval ─────────────────────────────────────────────────

    /// <summary>
    /// Returns Departments filtered by approval status ('Pending'/'Approved'/'Rejected').
    /// Includes User, Category, Locality. US10.
    /// </summary>
    List<Department> GetDepartmentsByStatus(string approvalStatus);

    /// <summary>
    /// Returns Organisations filtered by approval status. Includes User. US11.
    /// </summary>
    List<Organisation> GetOrganisationsByStatus(string approvalStatus);

    /// <summary>
    /// Approves or rejects a Solver or PWG registration; notifies applicant and writes AuditLog.
    /// Calls usp_DecideRegistration. decision = "Approved" | "Rejected".
    /// Returns true on success. US10, US11.
    /// </summary>
    bool DecideRegistration(int userId, string decision, int adminUserId);

    // ── User Management ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns users by role name, ordered by FullName. Includes Role, Locality. US13.
    /// </summary>
    List<User> GetUsersByRole(string roleName);

    /// <summary>
    /// Deactivates a user account (IsActive=0, IsBanned=0).
    /// Calls usp_DeactivateUser with IsBan=0. Returns true on success. US13.
    /// </summary>
    bool DeactivateUser(int targetUserId, string reason, int adminUserId);

    /// <summary>
    /// Permanently bans a user (IsActive=0, IsBanned=1, BanReason, BannedAt set — F7).
    /// Calls usp_DeactivateUser with IsBan=1. Returns true on success. US13.
    /// </summary>
    bool BanUser(int targetUserId, string reason, int adminUserId);

    // ── Escalation Management ─────────────────────────────────────────────────

    /// <summary>
    /// Returns all complaints with Status='Escalated', oldest first.
    /// Includes Citizen, Department, Category, Locality. US55.
    /// </summary>
    List<Complaint> GetEscalatedComplaints();

    /// <summary>
    /// Manually escalates a complaint, optionally reassigning to a new department.
    /// F2 guard: complaint must be routed (DeptId NOT NULL) before escalation.
    /// Calls usp_FileEscalation with EscalationTrigger='Manual'.
    /// Returns true on success. US55.
    /// </summary>
    bool ManualEscalation(int complaintId, int adminUserId,
                          int? reassignedToDeptId, string reason);

    /// <summary>
    /// Returns escalation history for a complaint, newest first.
    /// Includes Actor, OriginalDept, ReassignedToDept navigations.
    /// </summary>
    List<EscalationLog> GetEscalationLog(int complaintId);

    // ── PWG Report Review ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns all PWG reports, optionally filtered by status ('Pending'/'Reviewed'/'Closed').
    /// Ordered by ReportedAt desc. Includes ReportedOrg, ReportedByUser, Complaint. US63.
    /// </summary>
    List<Pwgreport> GetAllPWGReports(string status = null);

    /// <summary>
    /// Admin reviews a PWG report and takes action (Warned/Suspended/Removed/Dismissed).
    /// Calls usp_ReviewPWGReport. finalClose=true closes the report in one call.
    /// Returns true on success. US63.
    /// </summary>
    bool ReviewPWGReport(int reportId, int adminUserId, string adminAction,
                         string adminNote = null, bool finalClose = false);

    /// <summary>
    /// Closes a reviewed PWG report. Calls usp_CloseReport.
    /// Returns true on success. US63.
    /// </summary>
    bool ClosePWGReport(int reportId, int adminUserId, string closeNote = null);

    // ── Complaint Oversight ───────────────────────────────────────────────────

    /// <summary>
    /// Returns all complaints platform-wide, newest first.
    /// Pass a non-null status to filter (e.g. "Escalated"). US12 dashboard navigation.
    /// </summary>
    List<Complaint> GetAllComplaints(string status = null);

    // ── Platform Statistics ───────────────────────────────────────────────────

    /// <summary>
    /// Reads live platform-wide stats via ADO.NET (3 result sets from usp_GetPlatformStats).
    /// Returns complaint status counts, active users, and role totals. US12.
    /// </summary>
    PlatformStats GetPlatformStats();

    /// <summary>
    /// Snapshots current stats to PlatformStatsSnapshot table.
    /// Calls usp_SnapshotPlatformStats. Intended for daily Azure Function trigger.
    /// Returns true on success.
    /// </summary>
    bool SnapshotPlatformStats();

    // ── Lookups ───────────────────────────────────────────────────────────────

    /// <summary>Returns all issue categories ordered by name.</summary>
    List<IssueCategory> GetAllCategories();

    /// <summary>
    /// Returns all approved departments. Used for escalation reassign dropdown.
    /// Includes Category, Locality.
    /// </summary>
    List<Department> GetAllDepartments();

    /// <summary>
    /// Returns recent audit log entries, newest first.
    /// Optionally filtered by ActionType. Includes Actor navigation.
    /// </summary>
    List<AuditLog> GetAuditLog(string actionType = null, int top = 100);
}

