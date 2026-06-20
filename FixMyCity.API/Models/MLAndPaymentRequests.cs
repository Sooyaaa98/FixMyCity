namespace FixMyCity.API.Models
{
    // ── ML ────────────────────────────────────────────────────────────────────

    public class SaveMLScoresRequest
    {
        public int       ComplaintId              { get; set; }
        public DateTime? PredictedResolutionDate  { get; set; }
        public decimal?  ResolutionProbability    { get; set; }
        public decimal?  PriorityScore            { get; set; }
        public string    ModelVersion             { get; set; }
    }

    public class AddUserInterestRequest
    {
        public int    UserId              { get; set; }
        public short? CategoryId          { get; set; }
        public int?   PreferredLocalityId { get; set; }
    }

    public class RemoveUserInterestRequest
    {
        public int    UserId              { get; set; }
        public short? CategoryId          { get; set; }
        public int?   PreferredLocalityId { get; set; }
    }

    // ── Payment ───────────────────────────────────────────────────────────────

    public class CreateContributionRequest
    {
        public int     ComplaintId    { get; set; }
        public int     CitizenUserId  { get; set; }
        public decimal Amount         { get; set; }
        public string  TransactionRef { get; set; }   // must be obtained from gateway first (F5)
    }

    public class UpdatePaymentStatusRequest
    {
        public string TransactionRef { get; set; }
        public string NewStatus      { get; set; }   // Success / Failed / Refunded
        public string FailureReason  { get; set; }
    }

    // ── Razorpay (Phase 5) ────────────────────────────────────────────────────

    /// <summary>
    /// Frontend asks the server to create a Razorpay Order for a citizen
    /// contribution. The server creates the order via Razorpay's REST API,
    /// returns the order_id + amount + key_id, and the frontend then opens
    /// the checkout modal with those values.
    /// </summary>
    public class RazorpayCreateOrderRequest
    {
        public int     ComplaintId   { get; set; }
        public int     CitizenUserId { get; set; }
        public decimal Amount        { get; set; }   // rupees (server converts to paise)
    }

    public class RazorpayCreateOrderResponse
    {
        public bool    Success     { get; set; }
        public string  Message     { get; set; }
        public bool    DemoMode    { get; set; }    // true when no real key is configured
        public string  KeyId       { get; set; }    // empty in demo mode
        public string  OrderId     { get; set; }    // "order_…" — or synthetic in demo
        public long    AmountPaise { get; set; }
        public string  Currency    { get; set; }
        public string  Receipt     { get; set; }
        public string  CompanyName { get; set; }
    }

    /// <summary>
    /// After the Razorpay modal succeeds, the frontend sends these three values
    /// (provided by the Razorpay SDK) plus the original complaint + citizen +
    /// amount. The server (a) verifies the HMAC signature, (b) creates the
    /// Contribution row idempotently on TransactionRef = payment_id, and
    /// (c) flips its PaymentStatus to Success.
    /// </summary>
    public class RazorpayVerifyRequest
    {
        public int     ComplaintId      { get; set; }
        public int     CitizenUserId    { get; set; }
        public decimal Amount           { get; set; }
        public string  RazorpayOrderId   { get; set; }
        public string  RazorpayPaymentId { get; set; }
        public string  RazorpaySignature { get; set; }
    }

    public class RazorpayVerifyResponse
    {
        public bool   Success        { get; set; }
        public string Message        { get; set; }
        public int    ContributionId { get; set; }
        public string TransactionRef { get; set; }    // payment_id
    }
}
