using FixMyCity.DAL.Models;
using FixMyCity.DAL.Repositories.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
namespace FixMyCity.DAL.Repositories.Implementations
{

    public class PWGRepository : IPWGRepository
    {
        private readonly FixMyCityDbContext _context;
        private readonly ILogger<PWGRepository> _logger;

        // Phase-2 hardening (2026-05-19): every previously-silent catch now logs via ILogger.
        public PWGRepository(FixMyCityDbContext context, ILogger<PWGRepository> logger)
        {
            _context = context;
            _logger  = logger ?? NullLogger<PWGRepository>.Instance;
        }

        // Backward-compatible overload for existing `new PWGRepository(_context)` callers.
        public PWGRepository(FixMyCityDbContext context)
            : this(context, NullLogger<PWGRepository>.Instance) { }

        // ── GetOpenComplaintsForPWG ───────────────────────────────────────────────

        public List<Complaint> GetOpenComplaintsForPWG(short? categoryId,
                                                        int? localityId,
                                                        string criticality)
        {
            var query = _context.Complaints
                                .Include(c => c.Category)
                                .Include(c => c.Department)
                                .Include(c => c.Citizen)
                                .Include(c => c.Locality)
                                .Include(c => c.MlScore)
                                .Where(c => c.Status != "Resolved"
                                         && c.Status != "Rejected"
                                         && c.Status != "Linked");

            if (categoryId.HasValue)
                query = query.Where(c => c.CategoryId == categoryId.Value);

            if (localityId.HasValue && localityId > 0)
                query = query.Where(c => c.LocalityId == localityId.Value);

            if (!string.IsNullOrWhiteSpace(criticality))
                query = query.Where(c => c.Criticality == criticality);

            return query.OrderByDescending(c => c.MlScore != null ? c.MlScore.PriorityScore : 0)
                        .ThenByDescending(c => c.SubmittedAt)
                        .ToList();
        }

        // ── SubmitParticipationRequest ────────────────────────────────────────────

        public int SubmitParticipationRequest(int complaintId, int orgId, string requestNote)
        {
            try
            {
                var outId = new SqlParameter
                {
                    ParameterName = "@NewRequestId",
                    SqlDbType = SqlDbType.Int,
                    Direction = ParameterDirection.Output
                };

                _context.Database.ExecuteSqlRaw(
                    "EXEC dbo.usp_SubmitPWGRequest @ComplaintId, @OrgId, @RequestNote, @NewRequestId OUTPUT",
                    new SqlParameter("@ComplaintId", complaintId),
                    new SqlParameter("@OrgId", orgId),
                    new SqlParameter("@RequestNote", (object)requestNote ?? DBNull.Value),
                    outId);

                return outId.Value != DBNull.Value ? (int)outId.Value : 0;
            }
            catch (Exception ex) { _logger.LogError(ex, "PWGRepository stored-procedure call failed"); return 0; }
        }

        // ── GetRequestsByOrg ──────────────────────────────────────────────────────

        public List<PwgparticipationRequest> GetRequestsByOrg(int orgId)
        {
            return _context.PWGParticipationRequests
                           .Include(r => r.Complaint)
                               .ThenInclude(c => c.Category)
                           .Include(r => r.Complaint)
                               .ThenInclude(c => c.Department)
                           .Include(r => r.Complaint)
                               .ThenInclude(c => c.Locality)
                           .Include(r => r.Organisation)
                           .Where(r => r.OrgId == orgId)
                           .OrderByDescending(r => r.RequestedAt)
                           .ToList();
        }

        // ── AddProgressUpdate ─────────────────────────────────────────────────────

        public bool AddProgressUpdate(int complaintId, int pwgUserId, string progressNote,
                                      string photoPath = null, string photoFileName = null,
                                      int? photoFileSizeKb = null)
        {
            try
            {
                var outSuccess = new SqlParameter
                {
                    ParameterName = "@IsSuccess",
                    SqlDbType = SqlDbType.Bit,
                    Direction = ParameterDirection.Output
                };

                // F4: SP routes photo to ComplaintAttachments (type='PWGProgress').
                _context.Database.ExecuteSqlRaw(
                    "EXEC dbo.usp_PWGProgressUpdate @ComplaintId, @PWGUserId, @ProgressNote, @IsSuccess OUTPUT, @PhotoPath, @PhotoFileName, @PhotoFileSizeKB",
                    new SqlParameter("@ComplaintId", complaintId),
                    new SqlParameter("@PWGUserId", pwgUserId),
                    new SqlParameter("@ProgressNote", progressNote),
                    outSuccess,
                    new SqlParameter("@PhotoPath", (object)photoPath ?? DBNull.Value),
                    new SqlParameter("@PhotoFileName", (object)photoFileName ?? DBNull.Value),
                    new SqlParameter("@PhotoFileSizeKB", (object)photoFileSizeKb ?? DBNull.Value));

                return outSuccess.Value != DBNull.Value && (bool)outSuccess.Value;
            }
            catch (Exception ex) { _logger.LogError(ex, "PWGRepository stored-procedure call failed"); return false; }
        }

        // ── GetOrgByUserId ────────────────────────────────────────────────────────

        public Organisation GetOrgByUserId(int userId)
        {
            return _context.Organisations
                           .Include(o => o.User)
                           .FirstOrDefault(o => o.UserId == userId);
        }

        // ── UpdateOrgProfile ──────────────────────────────────────────────────────

        public bool UpdateOrgProfile(int orgId, string orgName, string contactEmail,
                                     string contactPhone, string address)
        {
            try
            {
                _context.Database.ExecuteSqlRaw(
                    "EXEC dbo.usp_UpdateOrgProfile @OrgId, @OrgName, @ContactEmail, @ContactPhone, @Address",
                    new SqlParameter("@OrgId", orgId),
                    new SqlParameter("@OrgName", orgName),
                    new SqlParameter("@ContactEmail", contactEmail),
                    new SqlParameter("@ContactPhone", contactPhone),
                    new SqlParameter("@Address", address));
                return true;
            }
            catch (Exception ex) { _logger.LogError(ex, "PWGRepository stored-procedure call failed"); return false; }
        }

        // ── FilePWGReport ─────────────────────────────────────────────────────────

        public int FilePWGReport(int complaintId, int reportedOrgId,
                                 int reportedByUserId, string reportReason)
        {
            try
            {
                var outId = new SqlParameter
                {
                    ParameterName = "@NewReportId",
                    SqlDbType = SqlDbType.Int,
                    Direction = ParameterDirection.Output
                };

                _context.Database.ExecuteSqlRaw(
                    "EXEC dbo.usp_FilePWGReport @ComplaintId, @ReportedOrgId, @ReportedByUserId, @ReportReason, @NewReportId OUTPUT",
                    new SqlParameter("@ComplaintId", complaintId),
                    new SqlParameter("@ReportedOrgId", reportedOrgId),
                    new SqlParameter("@ReportedByUserId", reportedByUserId),
                    new SqlParameter("@ReportReason", reportReason),
                    outId);

                return outId.Value != DBNull.Value ? (int)outId.Value : 0;
            }
            catch (Exception ex) { _logger.LogError(ex, "PWGRepository stored-procedure call failed"); return 0; }
        }

        // ── GetPendingRequestsForSolver ───────────────────────────────────────────

        public List<PwgparticipationRequest> GetPendingRequestsForSolver(int solverUserId)
        {
            int deptId = _context.Departments
                                 .Where(d => d.UserId == solverUserId)
                                 .Select(d => d.DeptId)
                                 .FirstOrDefault();

            return _context.PWGParticipationRequests
                           .Include(r => r.Complaint)
                               .ThenInclude(c => c.Category)
                           .Include(r => r.Complaint)
                               .ThenInclude(c => c.Locality)
                           .Include(r => r.Organisation)
                           .Where(r => r.Complaint.DeptId == deptId
                                    && r.Status == "Pending")
                           .OrderByDescending(r => r.RequestedAt)
                           .ToList();
        }

        // ── GetAllRequestsForSolver ───────────────────────────────────────────────

        public List<PwgparticipationRequest> GetAllRequestsForSolver(int solverUserId)
        {
            int deptId = _context.Departments
                                 .Where(d => d.UserId == solverUserId)
                                 .Select(d => d.DeptId)
                                 .FirstOrDefault();

            return _context.PWGParticipationRequests
                           .Include(r => r.Complaint)
                               .ThenInclude(c => c.Category)
                           .Include(r => r.Complaint)
                               .ThenInclude(c => c.Locality)
                           .Include(r => r.Organisation)
                           .Where(r => r.Complaint.DeptId == deptId)
                           .OrderByDescending(r => r.RequestedAt)
                           .ToList();
        }

        // ── ResolvePWGRequest ─────────────────────────────────────────────────────

        public bool ResolvePWGRequest(int requestId, int solverUserId,
                                      string decision, string decisionNote)
        {
            try
            {
                _context.Database.ExecuteSqlRaw(
                    "EXEC dbo.usp_ResolvePWGRequest @RequestId, @SolverUserId, @Decision, @DecisionNote",
                    new SqlParameter("@RequestId", requestId),
                    new SqlParameter("@SolverUserId", solverUserId),
                    new SqlParameter("@Decision", decision),
                    new SqlParameter("@DecisionNote", (object)decisionNote ?? DBNull.Value));
                return true;
            }
            catch (Exception ex) { _logger.LogError(ex, "PWGRepository stored-procedure call failed"); return false; }
        }

        // ── GetDeptByUserId ───────────────────────────────────────────────────────

        public Department GetDeptByUserId(int userId)
        {
            return _context.Departments
                           .Include(d => d.User)
                           .Include(d => d.Category)
                           .Include(d => d.Locality)
                           .FirstOrDefault(d => d.UserId == userId);
        }

        // ── UpdateDeptProfile ─────────────────────────────────────────────────────

        public bool UpdateDeptProfile(int deptId, string deptName, string ministry,
                                      string contactEmail, string contactPhone,
                                      string address, int localityId)
        {
            try
            {
                _context.Database.ExecuteSqlRaw(
                    "EXEC dbo.usp_UpdateDeptProfile @DeptId, @DeptName, @Ministry, @ContactEmail, @ContactPhone, @Address, @LocalityId",
                    new SqlParameter("@DeptId", deptId),
                    new SqlParameter("@DeptName", deptName),
                    new SqlParameter("@Ministry", ministry),
                    new SqlParameter("@ContactEmail", contactEmail),
                    new SqlParameter("@ContactPhone", contactPhone),
                    new SqlParameter("@Address", address),
                    new SqlParameter("@LocalityId", localityId));
                return true;
            }
            catch (Exception ex) { _logger.LogError(ex, "PWGRepository stored-procedure call failed"); return false; }
        }
    }


}
