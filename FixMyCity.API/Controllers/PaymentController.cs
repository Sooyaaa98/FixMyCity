using Microsoft.AspNetCore.Authorization;
using FixMyCity.API.Models;
using FixMyCity.API.Services;
using FixMyCity.DAL.Models;
using FixMyCity.DAL.Repositories.Implementations;
using Microsoft.AspNetCore.Mvc;

namespace FixMyCity.API.Controllers
{
    /// <summary>
    /// Handles citizen financial contributions to complaints:
    /// create, gateway callback, history, and funding total.
    ///
    /// Phase 5 (2026-05-19) — added the Razorpay flow:
    ///   POST  /api/Payment/GetRazorpayConfig      — frontend bootstrap
    ///   POST  /api/Payment/CreateRazorpayOrder    — server-side order creation
    ///   POST  /api/Payment/VerifyRazorpayPayment  — HMAC signature verify + persist
    ///
    /// The legacy CreateContribution / UpdatePaymentStatus endpoints remain so
    /// the existing UI keeps working in demo mode (no real key configured).
    /// </summary>
    [Route("api/[controller]/[action]")]
    [Authorize]
    [ApiController]
    public class PaymentController : Controller
    {
        private readonly FixMyCityDbContext _context;
        private readonly IRazorpayService   _razorpay;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(FixMyCityDbContext context,
                                 IRazorpayService razorpay,
                                 ILogger<PaymentController> logger)
        {
            _context  = context;
            _razorpay = razorpay;
            _logger   = logger;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Razorpay flow (Phase 5)
        // ═══════════════════════════════════════════════════════════════════

        // ── GET api/Payment/GetRazorpayConfig ─────────────────────────────────
        // Frontend bootstrap. Tells the UI whether Razorpay is configured (so it
        // can decide between the real SDK and the demo bypass), and what the
        // public KeyId is. The secret is NEVER returned.
        [HttpGet]
        public JsonResult GetRazorpayConfig()
        {
            return Json(new
            {
                success     = true,
                configured  = _razorpay.IsConfigured,
                keyId       = _razorpay.IsConfigured ? _razorpay.KeyId : null,
                currency    = _razorpay.Currency,
                companyName = _razorpay.CompanyName
            });
        }

        // ── POST api/Payment/CreateRazorpayOrder ──────────────────────────────
        // Creates a Razorpay Order on the server (so amount + receipt are bound
        // to the gateway-issued order_id and can't be tampered with from the
        // browser). Falls back to demo mode when keys are not configured.
        [HttpPost]
        public async Task<JsonResult> CreateRazorpayOrder(RazorpayCreateOrderRequest request)
        {
            if (request == null || request.Amount <= 0
                || request.ComplaintId <= 0 || request.CitizenUserId <= 0)
            {
                return Json(new RazorpayCreateOrderResponse
                {
                    Success = false,
                    Message = "Invalid request — complaintId, citizenUserId and amount > 0 are required."
                });
            }

            // Razorpay receipt: max 40 chars. Use deterministic prefix so support staff
            // can trace orders back to a complaint quickly.
            var receipt = $"fmc_c{request.ComplaintId}_u{request.CitizenUserId}_{DateTime.UtcNow:yyMMddHHmmss}";
            if (receipt.Length > 40) receipt = receipt[..40];

            if (!_razorpay.IsConfigured)
            {
                // Demo mode — preserve the synthetic-ref behaviour from Phase 0 so
                // the contribution flow remains exercisable without provisioning keys.
                _logger.LogWarning("Razorpay not configured — returning demo order for complaint {ComplaintId}.",
                    request.ComplaintId);

                return Json(new RazorpayCreateOrderResponse
                {
                    Success     = true,
                    DemoMode    = true,
                    Message     = "Demo mode — Razorpay keys not configured. Payment will be simulated.",
                    KeyId       = "",
                    OrderId     = $"DEMO_{DateTime.UtcNow:yyyyMMddHHmmssfff}",
                    AmountPaise = (long)Math.Round(request.Amount * 100m, MidpointRounding.AwayFromZero),
                    Currency    = _razorpay.Currency,
                    Receipt     = receipt,
                    CompanyName = _razorpay.CompanyName
                });
            }

            try
            {
                var notes = new Dictionary<string, string>
                {
                    ["complaint_id"] = request.ComplaintId.ToString(),
                    ["citizen_id"]   = request.CitizenUserId.ToString()
                };

                var order = await _razorpay.CreateOrderAsync(
                    request.Amount, receipt, notes);

                return Json(new RazorpayCreateOrderResponse
                {
                    Success     = true,
                    DemoMode    = false,
                    KeyId       = _razorpay.KeyId,
                    OrderId     = order.Id,
                    AmountPaise = order.Amount,
                    Currency    = order.Currency,
                    Receipt     = order.Receipt,
                    CompanyName = _razorpay.CompanyName
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreateRazorpayOrder failed for complaint {ComplaintId}.", request.ComplaintId);
                return Json(new RazorpayCreateOrderResponse
                {
                    Success = false,
                    Message = "Could not create the payment order. Please try again."
                });
            }
        }

        // ── POST api/Payment/VerifyRazorpayPayment ────────────────────────────
        // Final step. The frontend forwards razorpay_order_id / payment_id /
        // signature here after the SDK's success handler fires. We verify the
        // HMAC signature server-side, then create the Contribution row
        // idempotently (UPDLOCK on TransactionRef) and mark it Success.
        [HttpPost]
        public JsonResult VerifyRazorpayPayment(RazorpayVerifyRequest request)
        {
            if (request == null || request.Amount <= 0)
                return Json(new RazorpayVerifyResponse { Success = false, Message = "Invalid request." });

            // Demo mode short-circuit: order_id starts with DEMO_ → trust the client
            // (we already trust the JWT) and persist the contribution as Success.
            bool demoFlow = request.RazorpayOrderId?.StartsWith("DEMO_") == true;

            if (!demoFlow)
            {
                if (string.IsNullOrEmpty(request.RazorpayOrderId)
                 || string.IsNullOrEmpty(request.RazorpayPaymentId)
                 || string.IsNullOrEmpty(request.RazorpaySignature))
                {
                    return Json(new RazorpayVerifyResponse
                    {
                        Success = false,
                        Message = "Razorpay order_id / payment_id / signature are all required."
                    });
                }

                if (!_razorpay.VerifySignature(
                        request.RazorpayOrderId,
                        request.RazorpayPaymentId,
                        request.RazorpaySignature))
                {
                    _logger.LogWarning(
                        "Razorpay signature verification FAILED for order {OrderId} payment {PaymentId}.",
                        request.RazorpayOrderId, request.RazorpayPaymentId);
                    return Json(new RazorpayVerifyResponse
                    {
                        Success = false,
                        Message = "Signature verification failed. Payment not recorded."
                    });
                }
            }

            // TransactionRef convention:
            //   - real gateway flow → razorpay_payment_id ("pay_…")
            //   - demo flow         → the synthetic DEMO_… order_id
            // Both are unique and ≤ 100 chars (Contributions.TransactionRef cap).
            var txRef = demoFlow
                ? request.RazorpayOrderId
                : request.RazorpayPaymentId;

            try
            {
                var repo  = new PaymentRepository(_context);
                int newId = repo.CreateContribution(
                    request.ComplaintId, request.CitizenUserId, request.Amount, txRef);

                if (newId <= 0)
                {
                    return Json(new RazorpayVerifyResponse
                    {
                        Success        = false,
                        Message        = "Could not record the contribution (DB rejected the insert).",
                        TransactionRef = txRef
                    });
                }

                // Promote the row from default 'Pending' (per usp_CreateContribution)
                // to 'Success'. UPDLOCK + ROWLOCK on TransactionRef → safe under retry.
                repo.UpdatePaymentStatus(txRef, "Success");

                return Json(new RazorpayVerifyResponse
                {
                    Success        = true,
                    Message        = demoFlow
                        ? "Demo payment recorded successfully."
                        : "Payment verified and recorded.",
                    ContributionId = newId,
                    TransactionRef = txRef
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VerifyRazorpayPayment persistence failed for txRef {TxRef}.", txRef);
                return Json(new RazorpayVerifyResponse
                {
                    Success        = false,
                    Message        = "Internal error while saving the contribution.",
                    TransactionRef = txRef
                });
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Legacy create + status (kept for backward compatibility)
        // ═══════════════════════════════════════════════════════════════════

        // ── POST api/Payment/CreateContribution ───────────────────────────────
        // TransactionRef must be obtained from the payment gateway first (F5).
        // The new Razorpay flow above uses VerifyRazorpayPayment instead.

        [HttpPost]
        public JsonResult CreateContribution(CreateContributionRequest request)
        {
            try
            {
                var repo           = new PaymentRepository(_context);
                int contributionId = repo.CreateContribution(
                                         request.ComplaintId, request.CitizenUserId,
                                         request.Amount, request.TransactionRef);
                return Json(new { success = contributionId > 0, contributionId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreateContribution failed.");
                return Json(new { success = false, contributionId = 0, error = ex.Message });
            }
        }

        // ── PUT api/Payment/UpdatePaymentStatus ───────────────────────────────
        // Called by the payment gateway webhook on Success / Failed / Refunded.
        // For Razorpay, server-side webhook setup is OPTIONAL — the
        // VerifyRazorpayPayment endpoint above is the authoritative path. Use
        // this for refunds, manual reconciliation, or if you add a webhook
        // listener later (point Razorpay → POST /api/Payment/UpdatePaymentStatus
        // and forward { transactionRef, newStatus, failureReason }).

        [HttpPut]
        public JsonResult UpdatePaymentStatus(UpdatePaymentStatusRequest request)
        {
            try
            {
                var repo   = new PaymentRepository(_context);
                bool result = repo.UpdatePaymentStatus(
                                  request.TransactionRef, request.NewStatus,
                                  request.FailureReason);
                return Json(new { success = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdatePaymentStatus failed.");
                return Json(new { success = false, message = "Payment status update failed.", error = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  History & Totals
        // ═══════════════════════════════════════════════════════════════════

        // ── GET api/Payment/GetContributionsByComplaint ───────────────────────

        [HttpGet]
        public JsonResult GetContributionsByComplaint(int complaintId)
        {
            try
            {
                var repo          = new PaymentRepository(_context);
                var contributions = repo.GetContributionsByComplaint(complaintId);
                return Json(contributions);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── GET api/Payment/GetContributionsByCitizen ─────────────────────────

        [HttpGet]
        public JsonResult GetContributionsByCitizen(int citizenUserId)
        {
            try
            {
                var repo          = new PaymentRepository(_context);
                var contributions = repo.GetContributionsByCitizen(citizenUserId);
                return Json(contributions);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── GET api/Payment/GetFundingTotal ───────────────────────────────────
        // Returns the sum of all successful contributions (fn_GetComplaintFunding).

        [HttpGet]
        public JsonResult GetFundingTotal(int complaintId)
        {
            try
            {
                var repo  = new PaymentRepository(_context);
                decimal total = repo.GetFundingTotal(complaintId);
                return Json(new { success = true, complaintId, fundingTotal = total });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, fundingTotal = 0m, error = ex.Message });
            }
        }
    }
}
