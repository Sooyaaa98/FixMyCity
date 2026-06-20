using Microsoft.AspNetCore.Authorization;
using FixMyCity.API.Models;
using FixMyCity.DAL.Infrastructure;
using FixMyCity.DAL.Models;
using FixMyCity.DAL.Repositories.Implementations;
using Microsoft.AspNetCore.Mvc;

namespace FixMyCity.API.Controllers
{

    [Route("api/[controller]/[action]")]
    [Authorize(Roles = "SuperAdmin")]
    [ApiController]
    public class AdminController : Controller
    {
        private readonly FixMyCityDbContext _context;

        public AdminController(FixMyCityDbContext context)
        {
            _context = context;
        }
        // In PWGController, AdminController, MLController — at the top of methods that query Complaints:
        private void SetRlsContext(string role = "SuperAdmin", int? userId = null, int? deptId = null)
        {
            DbSessionContext.CurrentUserRole = role;
            DbSessionContext.CurrentUserId = userId;
            DbSessionContext.CurrentDeptId = deptId;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Registration Approval
        // ═══════════════════════════════════════════════════════════════════

        // ── GET api/Admin/GetPendingDepartments ───────────────────────────────

        [HttpGet]
        public JsonResult GetPendingDepartments()
        {
            try
            {
                var repo  = new AdminRepository(_context);
                var depts = repo.GetDepartmentsByStatus("Pending");
                return Json(depts);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── GET api/Admin/GetPendingOrganisations ─────────────────────────────

        [HttpGet]
        public JsonResult GetPendingOrganisations()
        {
            try
            {
                var repo = new AdminRepository(_context);
                var orgs = repo.GetOrganisationsByStatus("Pending");
                return Json(orgs);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── PUT api/Admin/DecideDeptRegistration ──────────────────────────────

        [HttpPut]
        public JsonResult DecideDeptRegistration(DeptDecisionRequest request)
        {
            try
            {
                var repo   = new AdminRepository(_context);
                bool result = repo.DecideRegistration(
                                  request.UserId, request.Decision, request.AdminUserId);
                return Json(new { success = result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Decision could not be recorded.", error = ex.Message });
            }
        }

        // ── PUT api/Admin/DecideOrgRegistration ───────────────────────────────

        [HttpPut]
        public JsonResult DecideOrgRegistration(OrgDecisionRequest request)
        {
            try
            {
                var repo   = new AdminRepository(_context);
                bool result = repo.DecideRegistration(
                                  request.UserId, request.Decision, request.AdminUserId);
                return Json(new { success = result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Decision could not be recorded.", error = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  User Management
        // ═══════════════════════════════════════════════════════════════════

        // ── GET api/Admin/GetUsersByRole ──────────────────────────────────────

        [HttpGet]
        public JsonResult GetUsersByRole(string roleName)
        {
            try
            {
                var repo  = new AdminRepository(_context);
                var users = repo.GetUsersByRole(roleName);
                return Json(users);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── PUT api/Admin/DeactivateUser ──────────────────────────────────────

        [HttpPut]
        public JsonResult DeactivateUser(DeactivateUserRequest request)
        {
            try
            {
                var repo   = new AdminRepository(_context);
                bool result = repo.DeactivateUser(
                                  request.TargetUserId, request.Reason, request.AdminUserId);
                return Json(new { success = result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Deactivation failed.", error = ex.Message });
            }
        }

        // ── PUT api/Admin/BanUser ─────────────────────────────────────────────

        [HttpPut]
        public JsonResult BanUser(BanUserRequest request)
        {
            try
            {
                var repo   = new AdminRepository(_context);
                bool result = repo.BanUser(
                                  request.TargetUserId, request.Reason, request.AdminUserId);
                return Json(new { success = result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Ban failed.", error = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Complaint Oversight
        // ═══════════════════════════════════════════════════════════════════

        // ── GET api/Admin/GetAllComplaints ────────────────────────────────────

        [HttpGet]
        public JsonResult GetAllComplaints(string status = "")
        {
            try
            {
                var repo       = new AdminRepository(_context);
                var complaints = repo.GetAllComplaints(status);
                return Json(complaints);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── GET api/Admin/GetEscalatedComplaints ──────────────────────────────

        [HttpGet]
        public JsonResult GetEscalatedComplaints()
        {
            try
            {
                var repo       = new AdminRepository(_context);
                var complaints = repo.GetEscalatedComplaints();
                return Json(complaints);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── POST api/Admin/ManualEscalation ───────────────────────────────────

        [HttpPost]
        public JsonResult ManualEscalation(ManualEscalationRequest request)
        {
            try
            {
                var repo   = new AdminRepository(_context);
                bool result = repo.ManualEscalation(
                                  request.ComplaintId, request.AdminUserId,
                                  request.ReassignedToDeptId, request.Reason);
                return Json(new { success = result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Escalation failed.", error = ex.Message });
            }
        }

        // ── GET api/Admin/GetEscalationLog ────────────────────────────────────

        [HttpGet]
        public JsonResult GetEscalationLog(int complaintId)
        {
            try
            {
                var repo = new AdminRepository(_context);
                var log  = repo.GetEscalationLog(complaintId);
                return Json(log);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  PWG Report Management
        // ═══════════════════════════════════════════════════════════════════

        // ── GET api/Admin/GetAllPWGReports ────────────────────────────────────

        [HttpGet]
        public JsonResult GetAllPWGReports(string status = "")
        {
            try
            {
                var repo    = new AdminRepository(_context);
                var reports = repo.GetAllPWGReports(string.IsNullOrWhiteSpace(status) ? null : status);

                // Project to a stable camelCase shape so Angular IPwgReport fields
                // (reportContent, createdAt) always align regardless of EF property names.
                // DB column is ReportReason — map to reportContent for front-end compatibility.
                return Json(reports.Select(r => new
                {
                    reportId         = r.ReportId,
                    complaintId      = r.ComplaintId,
                    reportedOrgId    = r.ReportedOrgId,
                    reportedByUserId = r.ReportedByUserId,
                    reportContent    = r.ReportReason,    // ← DB: ReportReason → Angular: reportContent
                    status           = r.Status,
                    adminAction      = r.AdminAction,
                    adminNote        = r.AdminNote,
                    createdAt        = r.ReportedAt,
                    reportedOrg      = r.ReportedOrg  == null ? null : new { orgId = r.ReportedOrg.OrgId,  orgName  = r.ReportedOrg.OrgName },
                    reportedByUser   = r.ReportedByUser == null ? null : new { userId = r.ReportedByUser.UserId, fullName = r.ReportedByUser.FullName },
                    complaint        = r.Complaint == null ? null : new
                    {
                        complaintId  = r.Complaint.ComplaintId,
                        title        = r.Complaint.Title,
                        categoryName = r.Complaint.Category != null ? r.Complaint.Category.CategoryName : null,
                    },
                }));
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── PUT api/Admin/ReviewPWGReport ─────────────────────────────────────

        [HttpPut]
        public JsonResult ReviewPWGReport(ReviewPWGReportRequest request)
        {
            try
            {
                var repo   = new AdminRepository(_context);
                bool result = repo.ReviewPWGReport(
                                  request.ReportId, request.AdminUserId,
                                  request.AdminAction, request.AdminNote,
                                  request.FinalClose);
                return Json(new { success = result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Review failed.", error = ex.Message });
            }
        }

        // ── PUT api/Admin/ClosePWGReport ──────────────────────────────────────

        [HttpPut]
        public JsonResult ClosePWGReport(ClosePWGReportRequest request)
        {
            try
            {
                var repo   = new AdminRepository(_context);
                bool result = repo.ClosePWGReport(
                                  request.ReportId, request.AdminUserId, request.CloseNote);
                return Json(new { success = result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Close failed.", error = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Platform Stats & Lookups
        // ═══════════════════════════════════════════════════════════════════

        // ── GET api/Admin/GetPlatformStats ────────────────────────────────────

        [HttpGet]
        public JsonResult GetPlatformStats()
        {
            try
            {
                var repo  = new AdminRepository(_context);
                var stats = repo.GetPlatformStats();
                return Json(stats);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── POST api/Admin/SnapshotPlatformStats ──────────────────────────────

        [HttpPost]
        public JsonResult SnapshotPlatformStats()
        {
            try
            {
                var repo   = new AdminRepository(_context);
                bool result = repo.SnapshotPlatformStats();
                return Json(new { success = result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Snapshot failed.", error = ex.Message });
            }
        }

        // ── GET api/Admin/GetAllDepartments ───────────────────────────────────

        [HttpGet]
        public JsonResult GetAllDepartments()
        {
            try
            {
                var repo  = new AdminRepository(_context);
                var depts = repo.GetAllDepartments();
                return Json(depts);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── GET api/Admin/GetAllCategories ────────────────────────────────────

        [HttpGet]
        public JsonResult GetAllCategories()
        {
            try
            {
                var repo       = new AdminRepository(_context);
                var categories = repo.GetAllCategories();
                return Json(categories);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── GET api/Admin/GetAuditLog ─────────────────────────────────────────

        [HttpGet]
        public JsonResult GetAuditLog(string actionType = "", int top = 100)
        {
            try
            {
                var repo = new AdminRepository(_context);
                var log  = repo.GetAuditLog(
                               string.IsNullOrWhiteSpace(actionType) ? null : actionType, top);
                return Json(log);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Phase 8 — feature-suggestion endpoints (06_FeatureSuggestions.sql)
        // ═══════════════════════════════════════════════════════════════════

        // ── §11 / §16 — POST api/Admin/BulkUpdateStatus ──────────────────────
        [HttpPost]
        public JsonResult BulkUpdateStatus(BulkUpdateStatusRequest request)
        {
            try
            {
                SetRlsContext("SuperAdmin", request.ActorUserId);
                var repo  = new FeatureRepository(_context);
                int count = repo.BulkUpdateStatus(
                                request.ComplaintIds, request.NewStatus,
                                request.ActorUserId, request.Remark);
                return Json(new { success = count > 0, updatedCount = count });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Bulk update failed.", error = ex.Message });
            }
        }

        // ── §12 — PUT api/Admin/ReassignDept ─────────────────────────────────
        [HttpPut]
        public JsonResult ReassignDept(ReassignDeptRequest request)
        {
            try
            {
                SetRlsContext("SuperAdmin", request.AdminUserId);
                var repo = new FeatureRepository(_context);
                bool ok  = repo.ReassignDept(
                              request.ComplaintId, request.NewDeptId,
                              request.AdminUserId, request.Reason);
                return Json(new { success = ok });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Reassignment failed.", error = ex.Message });
            }
        }

        // ── §6 — GET api/Admin/GetAppeals ────────────────────────────────────
        [HttpGet]
        public JsonResult GetAppeals(string status = "")
        {
            try
            {
                var repo  = new FeatureRepository(_context);
                var rows  = repo.GetAppeals(
                                string.IsNullOrWhiteSpace(status) ? null : status);
                // Flatten complaint + author info for the UI table.
                return Json(rows.Select(a => new
                {
                    a.AppealId,
                    a.ComplaintId,
                    complaintTitle = a.Complaint?.Title,
                    complaintCategory = a.Complaint?.Category?.CategoryName,
                    complaintLocality = a.Complaint?.Locality?.LocalityName,
                    citizenUserId = a.CitizenUserId,
                    citizenName   = a.CitizenUser?.FullName,
                    a.Reason,
                    a.Status,
                    a.Decision,
                    a.AdminNote,
                    a.CreatedAt,
                    a.ResolvedAt,
                    adminName = a.AdminUser?.FullName,
                }));
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── §6 — POST api/Admin/ResolveAppeal ────────────────────────────────
        [HttpPost]
        public JsonResult ResolveAppeal(ResolveAppealRequest request)
        {
            try
            {
                SetRlsContext("SuperAdmin", request.AdminUserId);
                var repo = new FeatureRepository(_context);
                bool ok  = repo.ResolveAppeal(
                              request.AppealId, request.AdminUserId,
                              request.Decision, request.AdminNote);
                return Json(new { success = ok });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Could not resolve appeal.", error = ex.Message });
            }
        }

        // ── PUT api/Admin/ReactivateUser ──────────────────────────────────────
        // Reverses a previous deactivation or ban. Directly updates IsActive/IsBanned
        // via EF (no SP needed — inverse of usp_DeactivateOrBanUser) and writes
        // an audit row so the log stays coherent.

        [HttpPut]
        public JsonResult ReactivateUser([FromBody] ReactivateUserRequest request)
        {
            try
            {
                var user = _context.Users.FirstOrDefault(u => u.UserId == request.TargetUserId);
                if (user == null)
                    return Json(new { success = false, message = "User not found." });

                user.IsActive = true;
                user.IsBanned = false;

                _context.AuditLogs.Add(new FixMyCity.DAL.Models.AuditLog
                {
                    ActionType   = "UserReactivated",
                    ActorUserId  = request.AdminUserId,
                    TargetUserId = request.TargetUserId,
                    Reason       = string.IsNullOrWhiteSpace(request.Reason)
                                       ? "Reactivated by Admin"
                                       : request.Reason,
                    CreatedAt = DateTime.UtcNow,
                });

                _context.SaveChanges();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Reactivation failed.", error = ex.Message });
            }
        }

        // ── §9 — GET api/Admin/GetComplaintTrend ─────────────────────────────
        [HttpGet]
        public JsonResult GetComplaintTrend(int days = 30)
        {
            try
            {
                var repo = new FeatureRepository(_context);
                return Json(repo.GetTrend(days));
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }
    }
}
