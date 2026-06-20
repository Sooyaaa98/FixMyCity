// FixMyCity.API/Services/RazorpayService.cs
//
// Server-side Razorpay integration. Follows the documented flow at
// https://github.com/razorpay/razorpay-dot-net (Standard Checkout):
//
//   1. CreateOrderAsync(amount, receipt, ...) → POST https://api.razorpay.com/v1/orders
//      Returns an Order with an `id` ("order_…"). The frontend opens the
//      Razorpay checkout modal using that order_id, the configured key_id,
//      and the same amount. Razorpay binds the payment to that specific order.
//
//   2. On modal success the SDK calls our `handler` with
//        razorpay_order_id, razorpay_payment_id, razorpay_signature.
//      The frontend forwards those three values to /api/Payment/VerifyRazorpayPayment,
//      which calls VerifySignature(orderId, paymentId, signature) below.
//
//   3. VerifySignature recomputes HMAC-SHA256(orderId + "|" + paymentId, keySecret)
//      and compares it to the provided signature (constant-time). Only on a
//      match do we treat the payment as authentic.
//
// We deliberately do NOT use the Razorpay.Api NuGet package: the surface we
// need is two short HTTP/crypto calls. Avoiding the dependency keeps the
// project file slim, eliminates one supply-chain attack vector, and matches
// what the official .NET docs show under "Manual integration".

using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FixMyCity.API.Services
{
    public interface IRazorpayService
    {
        /// <summary>
        /// True when KeyId starts with `rzp_test_` or `rzp_live_` AND a non-empty
        /// secret is configured. False when either knob is missing or still set
        /// to a placeholder — the controller then short-circuits to demo mode.
        /// </summary>
        bool IsConfigured { get; }

        string KeyId { get; }
        string CompanyName { get; }
        string Currency { get; }

        /// <summary>Creates a Razorpay Order via REST and returns the parsed response.</summary>
        Task<RazorpayOrder> CreateOrderAsync(decimal amountInRupees, string receipt,
                                             IDictionary<string, string>? notes = null,
                                             CancellationToken ct = default);

        /// <summary>
        /// Verifies the HMAC-SHA256 signature that Razorpay attaches to a successful
        /// checkout. Returns true iff the recomputed signature matches the supplied
        /// one. Constant-time comparison.
        /// </summary>
        bool VerifySignature(string orderId, string paymentId, string signatureHex);
    }

    public class RazorpayService : IRazorpayService
    {
        private const string OrdersEndpoint = "https://api.razorpay.com/v1/orders";

        private readonly HttpClient _http;
        private readonly ILogger<RazorpayService> _logger;
        private readonly string _keyId;
        private readonly string _keySecret;

        public string Currency    { get; }
        public string CompanyName { get; }
        public string KeyId       => _keyId;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(_keyId)
            && !string.IsNullOrWhiteSpace(_keySecret)
            && (_keyId.StartsWith("rzp_test_") || _keyId.StartsWith("rzp_live_"))
            && !_keyId.Contains("REPLACE_WITH_YOUR_KEY", StringComparison.OrdinalIgnoreCase);

        public RazorpayService(HttpClient http,
                               IConfiguration config,
                               ILogger<RazorpayService> logger)
        {
            _http        = http;
            _logger      = logger;
            _keyId       = config["Razorpay:KeyId"]       ?? "";
            _keySecret   = config["Razorpay:KeySecret"]   ?? "";
            Currency     = config["Razorpay:Currency"]    ?? "INR";
            CompanyName  = config["Razorpay:CompanyName"] ?? "FixMyCity";

            if (IsConfigured)
            {
                // Basic auth: key_id:key_secret base64
                var token = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes($"{_keyId}:{_keySecret}"));
                _http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Basic", token);
                _http.Timeout = TimeSpan.FromSeconds(15);
            }
        }

        public async Task<RazorpayOrder> CreateOrderAsync(decimal amountInRupees,
                                                          string receipt,
                                                          IDictionary<string, string>? notes = null,
                                                          CancellationToken ct = default)
        {
            if (!IsConfigured)
                throw new InvalidOperationException(
                    "Razorpay is not configured. Set Razorpay:KeyId / Razorpay:KeySecret.");

            if (amountInRupees <= 0)
                throw new ArgumentOutOfRangeException(nameof(amountInRupees),
                    "Amount must be greater than zero.");

            // Razorpay expects amount in the lowest currency denomination (paise for INR).
            long amountInPaise = (long)Math.Round(amountInRupees * 100m, MidpointRounding.AwayFromZero);

            var payload = new Dictionary<string, object>
            {
                ["amount"]   = amountInPaise,
                ["currency"] = Currency,
                ["receipt"]  = receipt,   // max 40 chars per Razorpay API
                ["payment_capture"] = 1,  // auto-capture
            };
            if (notes is { Count: > 0 })
                payload["notes"] = notes;

            using var body = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            HttpResponseMessage resp;
            try
            {
                resp = await _http.PostAsync(OrdersEndpoint, body, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Razorpay Orders API call failed (network).");
                throw new InvalidOperationException(
                    "Could not reach Razorpay. Please try again.", ex);
            }

            var raw = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Razorpay Orders API HTTP {Code}: {Body}", (int)resp.StatusCode, raw);
                throw new InvalidOperationException(
                    $"Razorpay refused the order ({(int)resp.StatusCode}): {raw}");
            }

            var order = JsonSerializer.Deserialize<RazorpayOrder>(raw,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (order is null || string.IsNullOrEmpty(order.Id))
                throw new InvalidOperationException(
                    "Razorpay returned an empty order response.");

            return order;
        }

        public bool VerifySignature(string orderId, string paymentId, string signatureHex)
        {
            if (!IsConfigured) return false;
            if (string.IsNullOrEmpty(orderId)
             || string.IsNullOrEmpty(paymentId)
             || string.IsNullOrEmpty(signatureHex)) return false;

            // payload = order_id + "|" + payment_id ; key = key_secret
            var payload = $"{orderId}|{paymentId}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_keySecret));
            var computed = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));

            byte[] supplied;
            try
            {
                supplied = Convert.FromHexString(signatureHex);
            }
            catch
            {
                return false;
            }
            if (supplied.Length != computed.Length) return false;
            return CryptographicOperations.FixedTimeEquals(supplied, computed);
        }
    }

    /// <summary>Razorpay Order resource (only fields we need).</summary>
    public class RazorpayOrder
    {
        [JsonPropertyName("id")]       public string Id       { get; set; } = "";
        [JsonPropertyName("entity")]   public string Entity   { get; set; } = "";
        [JsonPropertyName("amount")]   public long   Amount   { get; set; }   // paise
        [JsonPropertyName("currency")] public string Currency { get; set; } = "INR";
        [JsonPropertyName("receipt")]  public string Receipt  { get; set; } = "";
        [JsonPropertyName("status")]   public string Status   { get; set; } = "";
        [JsonPropertyName("created_at")] public long CreatedAt { get; set; }
    }
}
