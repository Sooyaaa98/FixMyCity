using Microsoft.AspNetCore.Authorization;
using FixMyCity.API.Models;
using FixMyCity.DAL.Infrastructure;
using FixMyCity.DAL.Models;
using FixMyCity.DAL.Repositories.Implementations;
using Microsoft.AspNetCore.Mvc;

namespace FixMyCity.API.Controllers
{
    // Solver is included because the controller exposes solver-facing endpoints
    // (GetPendingRequestsForSolver, GetAllRequestsForSolver, ResolvePWGRequest,
    // GetDeptProfile, UpdateDeptProfile). Excluding Solver here blocked the
    // entire solver PWG-collaboration workflow.
    [Route("api/[controller]/[action]")]
    [Authorize(Roles = "PWG,Solver,SuperAdmin")]
    [ApiController]
    public class PWGController : Controller
    {
        private readonly FixMyCityDbContext _context;

        public PWGController(FixMyCityDbContext context)
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
        //  PWG — Complaint Browsing
        // ═══════════════════════════════════════════════════════════════════

        // ── GET api/PWG/GetOpenComplaints ─────────────────────────────────────

        [HttpGet]
        public JsonResult GetOpenComplaints(short? categoryId, int? localityId, string? criticality)
        {
            try
            {
                var repo       = new PWGRepository(_context);
                var complaints = repo.GetOpenComplaintsForPWG(categoryId, localityId, criticality);
                return Json(complaints);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  PWG — Participation Requests
        // ═══════════════════════════════════════════════════════════════════

        // ── POST api/PWG/SubmitParticipationRequest ───────────────────────────

        [HttpPost]
        public JsonResult SubmitParticipationRequest(PWGParticipationRequestModel request)
        {
            try
            {
                var repo      = new PWGRepository(_context);
                int requestId = repo.SubmitParticipationRequest(
                                    request.ComplaintId, request.OrgId,
                                    request.RequestNote);
                return Json(new { success = requestId > 0, requestId });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, requestId = 0, error = ex.Message });
            }
        }

        // ── GET api/PWG/GetRequestsByOrg ──────────────────────────────────────

        [HttpGet]
        public JsonResult GetRequestsByOrg(int orgId)
        {
            try
            {
                var repo     = new PWGRepository(_context);
                var requests = repo.GetRequestsByOrg(orgId);
                return Json(requests);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  PWG — Progress Updates
        // ═══════════════════════════════════════════════════════════════════

        // ── POST api/PWG/ProgressUpdate ───────────────────────────────────────

        [HttpPost]
        public JsonResult ProgressUpdate(PWGProgressUpdateRequest request)
        {
            try
            {
                var repo   = new PWGRepository(_context);
                bool result = repo.AddProgressUpdate(
                                  request.ComplaintId, request.PWGUserId,
                                  request.ProgressNote, request.PhotoPath,
                                  request.PhotoFileName, request.PhotoFileSizeKB);
                return Json(new { success = result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Progress update failed.", error = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  PWG — Organisation Profile
        // ═══════════════════════════════════════════════════════════════════

        // ── GET api/PWG/GetOrgProfile ─────────────────────────────────────────

        [HttpGet]
        public JsonResult GetOrgProfile(int userId)
        {
            try
            {
                var repo = new PWGRepository(_context);
                var org  = repo.GetOrgByUserId(userId);

                if (org == null)
                    return Json(new { success = false, message = "Organisation not found." });

                return Json(org);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── PUT api/PWG/UpdateOrgProfile ──────────────────────────────────────

        [HttpPut]
        public JsonResult UpdateOrgProfile(UpdateOrgProfileRequest request)
        {
            try
            {
                var repo   = new PWGRepository(_context);
                bool result = repo.UpdateOrgProfile(
                                  request.OrgId, request.OrgName,
                                  request.ContactEmail, request.ContactPhone,
                                  request.Address);
                return Json(new { success = result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Profile update failed.", error = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  PWG — Filing Reports Against PWG
        // ═══════════════════════════════════════════════════════════════════

        // ── POST api/PWG/FilePWGReport ────────────────────────────────────────

        [HttpPost]
        public JsonResult FilePWGReport(FilePWGReportRequest request)
        {
            try
            {
                var repo     = new PWGRepository(_context);
                int reportId = repo.FilePWGReport(
                                   request.ComplaintId, request.ReportedOrgId,
                                   request.ReportedByUserId, request.ReportReason);
                return Json(new { success = reportId > 0, reportId });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, reportId = 0, error = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Solver — Participation Request Management
        // ═══════════════════════════════════════════════════════════════════

        // ── GET api/PWG/GetPendingRequestsForSolver ───────────────────────────

        [HttpGet]
        public JsonResult GetPendingRequestsForSolver(int solverUserId)
        {
            try
            {
                var repo     = new PWGRepository(_context);
                var requests = repo.GetPendingRequestsForSolver(solverUserId);
                return Json(requests);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── GET api/PWG/GetAllRequestsForSolver ───────────────────────────────

        [HttpGet]
        public JsonResult GetAllRequestsForSolver(int solverUserId)
        {
            try
            {
                var repo     = new PWGRepository(_context);
                var requests = repo.GetAllRequestsForSolver(solverUserId);
                return Json(requests);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── PUT api/PWG/ResolvePWGRequest ─────────────────────────────────────

        [HttpPut]
        public JsonResult ResolvePWGRequest(ResolvePWGRequestModel request)
        {
            try
            {
                var repo   = new PWGRepository(_context);
                bool result = repo.ResolvePWGRequest(
                                  request.RequestId, request.SolverUserId,
                                  request.Decision, request.DecisionNote);
                return Json(new { success = result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Request decision failed.", error = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Solver — Department Profile
        // ═══════════════════════════════════════════════════════════════════

        // ── GET api/PWG/GetDeptProfile ────────────────────────────────────────

        [HttpGet]
        public JsonResult GetDeptProfile(int userId)
        {
            try
            {
                var repo = new PWGRepository(_context);
                var dept = repo.GetDeptByUserId(userId);

                if (dept == null)
                    return Json(new { success = false, message = "Department not found." });

                return Json(dept);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── PUT api/PWG/UpdateDeptProfile ─────────────────────────────────────

        [HttpPut]
        public JsonResult UpdateDeptProfile(UpdateDeptProfileRequest request)
        {
            try
            {
                var repo   = new PWGRepository(_context);
                bool result = repo.UpdateDeptProfile(
                                  request.DeptId, request.DeptName, request.Ministry,
                                  request.ContactEmail, request.ContactPhone,
                                  request.Address, request.LocalityId);
                return Json(new { success = result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Profile update failed.", error = ex.Message });
            }
        }
    }
}
