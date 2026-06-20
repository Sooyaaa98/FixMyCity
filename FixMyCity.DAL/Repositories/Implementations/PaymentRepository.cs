using FixMyCity.DAL.Models;
using FixMyCity.DAL.Repositories.Interfaces;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Data;

namespace FixMyCity.DAL.Repositories.Implementations
{

    public class PaymentRepository : IPaymentRepository
    {
        private readonly FixMyCityDbContext _context;
        private readonly ILogger<PaymentRepository> _logger;

        public PaymentRepository(FixMyCityDbContext context, ILogger<PaymentRepository> logger)
        {
            _context = context;
            _logger  = logger ?? NullLogger<PaymentRepository>.Instance;
        }

        // Backward-compatible overload for existing `new PaymentRepository(_context)` callers.
        public PaymentRepository(FixMyCityDbContext context)
            : this(context, NullLogger<PaymentRepository>.Instance) { }

        // ── CreateContribution ────────────────────────────────────────────────────

        public int CreateContribution(int complaintId, int citizenUserId,
                                      decimal amount, string transactionRef)
        {
            // ISSUE 12 FIX: Replace direct EF save + in-memory duplicate check (TOCTOU race)
            // with an atomic SP that uses UPDLOCK to prevent concurrent duplicate inserts.
            try
            {
                var outId = new SqlParameter
                {
                    ParameterName = "@NewContributionId",
                    SqlDbType = SqlDbType.Int,
                    Direction = ParameterDirection.Output
                };

                _context.Database.ExecuteSqlRaw(
                    "EXEC dbo.usp_CreateContribution @ComplaintId, @CitizenUserId, @Amount, @TransactionRef, @NewContributionId OUTPUT",
                    new SqlParameter("@ComplaintId", complaintId),
                    new SqlParameter("@CitizenUserId", citizenUserId),
                    new SqlParameter("@Amount", amount),
                    new SqlParameter("@TransactionRef", transactionRef),
                    outId);

                return outId.Value != DBNull.Value ? (int)outId.Value : 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "CreateContribution failed: complaint {ComplaintId} citizen {CitizenUserId} amount {Amount} txRef {TxRef}",
                    complaintId, citizenUserId, amount, transactionRef);
                return 0;
            }
        }
        // ── UpdatePaymentStatus ───────────────────────────────────────────────────

        public bool UpdatePaymentStatus(string transactionRef, string newStatus,
                                        string failureReason = null)
        {
            try
            {
                // SP uses UPDLOCK + ROWLOCK — safe for concurrent gateway callbacks on same TxRef.
                _context.Database.ExecuteSqlRaw(
                    "EXEC dbo.usp_UpdatePaymentStatus @TransactionRef, @NewStatus, @FailureReason",
                    new SqlParameter("@TransactionRef", transactionRef),
                    new SqlParameter("@NewStatus", newStatus),
                    new SqlParameter("@FailureReason", (object)failureReason ?? DBNull.Value));
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "UpdatePaymentStatus failed: txRef {TxRef} -> {NewStatus}",
                    transactionRef, newStatus);
                return false;
            }
        }

        // ── GetContributionsByComplaint ───────────────────────────────────────────

        public List<Contribution> GetContributionsByComplaint(int complaintId)
        {
            return _context.Contributions
                           .Include(c => c.CitizenUser)
                           .Where(c => c.ComplaintId == complaintId)
                           .OrderByDescending(c => c.ContributedAt)
                           .ToList();
        }

        // ── GetFundingTotal ───────────────────────────────────────────────────────

        public decimal GetFundingTotal(int complaintId)
        {
            // fn_GetComplaintFunding returns SUM(Amount) WHERE PaymentStatus='Success'.
            try
            {
                var result = _context.Database
                    .SqlQueryRaw<decimal>(
                        "SELECT dbo.fn_GetComplaintFunding(@ComplaintId) AS Value",
                        new SqlParameter("@ComplaintId", complaintId))
                    .FirstOrDefault();
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetFundingTotal failed for complaint {ComplaintId}", complaintId);
                return 0m;
            }
        }

        // ── GetContributionsByCitizen ─────────────────────────────────────────────

        public List<Contribution> GetContributionsByCitizen(int citizenUserId)
        {
            return _context.Contributions
                           .Include(c => c.Complaint)
                               .ThenInclude(comp => comp.Category)
                           .Where(c => c.CitizenUserId == citizenUserId)
                           .OrderByDescending(c => c.ContributedAt)
                           .ToList();
        }
    }


}
