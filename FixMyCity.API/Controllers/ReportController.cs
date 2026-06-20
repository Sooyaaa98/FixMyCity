// FixMyCity.API/Controllers/ReportController.cs
// Exposes PDF generation endpoints for complaints, certificates, and PWG reports.
// All endpoints require JWT authorization. Role-specific access is enforced.
// PDFs are streamed directly — no temp files, no disk I/O.

using FixMyCity.API.Services;
using FixMyCity.DAL.Infrastructure;
using FixMyCity.DAL.Models;
using FixMyCity.DAL.Repositories.Implementations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FixMyCity.API.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class ReportController : Controller
    {
        private readonly FixMyCityDbContext _context;
        private readonly IQuestPdfService _pdf;

        public ReportController(FixMyCityDbContext context, IQuestPdfService pdf)
        {
            _context = context;
            _pdf = pdf;
        }

        // ── GET api/Report/ComplaintPdf?complaintId=N ─────────────────────────
        // Citizens can download their own complaints.
        // Solvers, PWG, and Admins can download any complaint.

        [HttpGet]
        public async Task<IActionResult> ComplaintPdf(int complaintId)
        {
            SetSessionContext();

            var complaint = await _context.Complaints
                .Include(c => c.Category)
                .Include(c => c.Locality)
                .Include(c => c.Citizen)
                .Include(c => c.Department)
                .FirstOrDefaultAsync(c => c.ComplaintId == complaintId);

            if (complaint == null) return NotFound("Complaint not found.");

            // Citizens can only download their own complaints
            var role = GetCurrentRole();
            var userId = GetCurrentUserId();
            if (role == "Citizen" && complaint.CitizenUserId != userId)
                return Forbid();

            // Load timeline
            var timeline = await _context.ComplaintTimelines
                .Where(t => t.ComplaintId == complaintId)
                .OrderBy(t => t.CreatedAt)
                .Select(t => new TimelineEntry(
                    t.NewStatus, t.ActorUserId.HasValue ? t.ActorUserId.ToString() : "System", t.CreatedAt, t.Remark))
                .ToListAsync();

            // Load ML scores
            var scores = await _context.ComplaintMlScores
                .FirstOrDefaultAsync(s => s.ComplaintId == complaintId);

            var data = new ComplaintPdfData(
                ComplaintId: complaint.ComplaintId,
                Title: complaint.Title,
                Description: complaint.Description,
                Status: complaint.Status,
                Criticality: complaint.Criticality ?? "Low",
                CategoryName: complaint.Category?.CategoryName ?? "—",
                LocalityName: complaint.Locality?.LocalityName ?? "—",
                Address: complaint.Address ?? "—",
                CitizenName: complaint.Citizen?.FullName ?? "—",
                CitizenEmail: complaint.Citizen?.Email ?? "—",
                DepartmentName: complaint.Department?.DeptName,
                SubmittedAt: complaint.SubmittedAt,
                ResolvedAt: complaint.ResolvedAt,
                PriorityScore: scores != null ? (float?)scores.PriorityScore : null,
                ResolutionProbability: scores != null ? (float?)scores.ResolutionProbability : null,
                PredictedResolutionDate: scores?.PredictedResolutionDate?.ToString("yyyy-MM-dd"),
                Timeline: timeline
            );

            byte[] pdf = _pdf.GenerateComplaintReport(data);
            string name = $"FixMyCity_Complaint_{complaintId}_{DateTime.Now:yyyyMMdd}.pdf";
            return File(pdf, "application/pdf", name);
        }

        // ── GET api/Report/CertificatePdf?certificateId=N ────────────────────

        [HttpGet]
        public async Task<IActionResult> CertificatePdf(int certificateId)
        {
            SetSessionContext();

            var cert = await _context.Certificates
                .Include(c => c.User)
                .Include(c => c.Milestone_Nav)
                .FirstOrDefaultAsync(c => c.CertificateId == certificateId);

            if (cert == null) return NotFound("Certificate not found.");

            var userId = GetCurrentUserId();
            var role = GetCurrentRole();
            if (role == "Citizen" && cert.UserId != userId)
                return Forbid();

            var data = new CertificatePdfData(
                CitizenName: cert.User?.FullName ?? "—",
                MilestoneName: cert.Milestone_Nav?.MilestoneName ?? cert.Milestone ?? "—",
                Description: cert.Milestone_Nav?.Description ?? "—",
                Points: cert.Milestone_Nav?.PointsThreshold ?? 0,
                AwardedAt: cert.IssuedAt,
                CertificateId: cert.CertificateId
            );

            byte[] pdf = _pdf.GenerateCertificate(data);
            string name = $"FixMyCity_Certificate_{certificateId}.pdf";
            return File(pdf, "application/pdf", name);
        }

        // ── GET api/Report/PwgReportPdf?reportId=N ───────────────────────────
        // PWG users, Solvers, and Admins only.

        [HttpGet]
        [Authorize(Roles = "PWG,Solver,SuperAdmin")]
        public async Task<IActionResult> PwgReportPdf(int reportId)
        {
            SetSessionContext();

            var report = await _context.PwgReports
                .Include(r => r.ReportedOrg)
                .Include(r => r.Complaint)
                .FirstOrDefaultAsync(r => r.ReportId == reportId);

            if (report == null) return NotFound("PWG report not found.");

            var data = new PwgReportPdfData(
                ReportId: report.ReportId,
                OrgName: report.ReportedOrg?.OrgName ?? "—",
                ComplaintTitle: report.Complaint?.Title ?? "—",
                WorkDescription: report.ReportReason ?? "—",
                StartDate: report.ReportedAt,
                EndDate: report.ReviewedAt ?? report.ClosedAt,
                FundUtilized: null,
                Status: report.Status ?? "Pending"
            );

            byte[] pdf = _pdf.GeneratePwgReport(data);
            string name = $"FixMyCity_PWGReport_{reportId}_{DateTime.Now:yyyyMMdd}.pdf";
            return File(pdf, "application/pdf", name);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void SetSessionContext()
        {
            DbSessionContext.CurrentUserId = GetCurrentUserId();
            DbSessionContext.CurrentUserRole = GetCurrentRole();
        }

        private int GetCurrentUserId()
        {
            var raw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                   ?? User.FindFirst("sub")?.Value;
            return int.TryParse(raw, out var id) ? id : 0;
        }

        private string GetCurrentRole() =>
            User.FindFirst(ClaimTypes.Role)?.Value ?? "Citizen";
    }
}