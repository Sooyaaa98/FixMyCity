using FixMyCity.DAL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FixMyCity.DAL.Repositories.Interfaces
{

    public interface IPWGRepository
    {
        // ── PWG — Complaint Browsing ──────────────────────────────────────────────

        List<Complaint> GetOpenComplaintsForPWG(short? categoryId, int? localityId,
                                                 string criticality);

        // ── PWG — Participation Requests ──────────────────────────────────────────

        int SubmitParticipationRequest(int complaintId, int orgId, string requestNote);


        List<PwgparticipationRequest> GetRequestsByOrg(int orgId);

        // ── PWG — Progress Updates ────────────────────────────────────────────────

        bool AddProgressUpdate(int complaintId, int pwgUserId, string progressNote,
                               string photoPath = null, string photoFileName = null,
                               int? photoFileSizeKb = null);

        // ── PWG — Organisation Profile ────────────────────────────────────────────

        Organisation GetOrgByUserId(int userId);

        bool UpdateOrgProfile(int orgId, string orgName, string contactEmail,
                              string contactPhone, string address);

        // ── PWG — Filing Reports ──────────────────────────────────────────────────

        int FilePWGReport(int complaintId, int reportedOrgId, int reportedByUserId,
                          string reportReason);

        // ── Solver — Participation Request Management ─────────────────────────────


        List<PwgparticipationRequest> GetPendingRequestsForSolver(int solverUserId);


        List<PwgparticipationRequest> GetAllRequestsForSolver(int solverUserId);


        bool ResolvePWGRequest(int requestId, int solverUserId,
                               string decision, string decisionNote);

        // ── Solver — Department Profile ───────────────────────────────────────────


        Department GetDeptByUserId(int userId);

        bool UpdateDeptProfile(int deptId, string deptName, string ministry,
                               string contactEmail, string contactPhone,
                               string address, int localityId);
    }


}
