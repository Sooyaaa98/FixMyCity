using Microsoft.AspNetCore.Authorization;
using FixMyCity.API.Models;
using FixMyCity.API.Services;
using FixMyCity.DAL.Models;
using FixMyCity.DAL.Repositories.Implementations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace FixMyCity.API.Controllers
{
    /// <summary>
    /// Handles all AI/ML data flows:
    ///   - Callback endpoints (Python AI service POSTs results back here)
    ///   - Read endpoints (Angular reads ML scores, tags, recommendations)
    ///   - On-demand triggers (admin triggers retrain, forecast, geo-cluster)
    ///   - Chatbot proxy
    ///
    /// Security: X-AI-Service-Key middleware protects write callbacks.
    ///           Public read endpoints are whitelisted in AIServiceKeyMiddleware.
    /// </summary>
    [Route("api/[controller]/[action]")]
    [Authorize]
    [ApiController]
    public class MLController : Controller
    {
        private readonly FixMyCityDbContext _context;
        private readonly AiService _aiService;

        public MLController(FixMyCityDbContext context, AiService aiService)
        {
            _context   = context;
            _aiService = aiService;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Phase-8 deletion note:
        //  The five callback endpoints (SaveMLScores, LogAIDecision,
        //  SaveEmbedding, SaveTags, SaveRecommendationCache) were write-backs
        //  from the Python AI service. AiService now performs those writes
        //  in-process via EF + the same SPs, so the HTTP surface is gone.
        //  The X-AI-Service-Key middleware and AIService config section are
        //  also retired in this phase.
        // ═══════════════════════════════════════════════════════════════════

        // ═══════════════════════════════════════════════════════════════════
        //  READ ENDPOINTS — consumed by Angular frontend
        // ═══════════════════════════════════════════════════════════════════

        // ── GET api/ML/GetMLScores ────────────────────────────────────────────

        [HttpGet]
        public JsonResult GetMLScores(int complaintId)
        {
            try
            {
                var repo = new MLRepository(_context);
                var scores = repo.GetMLScores(complaintId);

                if (scores == null)
                    return Json(new { success = false, message = "No ML scores found." });

                return Json(scores);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── GET api/ML/GetRecommendedComplaints ───────────────────────────────

        // Phase 6.2: AiService backs recommendations (Gemini embedding +
        // cosine similarity over current-model embeddings). DAL SP remains
        // the third-tier fallback if AiService returns nothing — preserves
        // the existing UX promise of "always something on the home feed".
        // Wire shape [{ complaintId }] unchanged.
        [HttpGet]
        public async Task<JsonResult> GetRecommendedComplaints(int userId, int topN = 10)
        {
            try
            {
                var aiIds = await _aiService.GetRecommendationsAsync(userId, topN);

                if (aiIds.Count > 0)
                    return Json(aiIds.Select(id => new { complaintId = id }));

                // Last-resort fallback: DAL stored-procedure SQL ranking.
                var mlRepo = new MLRepository(_context);
                var results = mlRepo.GetRecommendedComplaints(userId, topN);
                return Json(results);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── GET api/ML/GetUserInterests ───────────────────────────────────────

        [HttpGet]
        public JsonResult GetUserInterests(int userId)
        {
            try
            {
                var repo = new MLRepository(_context);
                var interests = repo.GetUserInterests(userId);
                return Json(interests);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── POST api/ML/AddUserInterest ───────────────────────────────────────

        [HttpPost]
        public JsonResult AddUserInterest(AddUserInterestRequest request)
        {
            try
            {
                if (!request.CategoryId.HasValue && !request.PreferredLocalityId.HasValue)
                    return Json(new { success = false, message = "Provide at least one of categoryId or preferredLocalityId." });

                var repo = new MLRepository(_context);
                bool result = repo.AddUserInterest(request.UserId, request.CategoryId, request.PreferredLocalityId);
                return Json(new { success = result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── DELETE api/ML/RemoveUserInterest ──────────────────────────────────

        [HttpDelete]
        public JsonResult RemoveUserInterest(RemoveUserInterestRequest request)
        {
            try
            {
                var repo = new MLRepository(_context);
                bool result = repo.RemoveUserInterest(request.UserId, request.CategoryId, request.PreferredLocalityId);
                return Json(new { success = result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── GET api/ML/GetTags ────────────────────────────────────────────────

        [HttpGet]
        public JsonResult GetTags(int complaintId)
        {
            try
            {
                var tags = _context.Database
                    .SqlQueryRaw<ComplaintTagDto>(
                        "SELECT Tag, Score FROM dbo.ComplaintTags WHERE ComplaintId = @p0 ORDER BY Score DESC",
                        complaintId)
                    .ToList();
                return Json(tags);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── GET api/ML/CheckAIHealth ──────────────────────────────────────────

        // Phase 4: routes through AiService (Gemini ping). The Python service
        // is no longer the source of truth for AI availability.
        [HttpGet]
        public async Task<JsonResult> CheckAIHealth()
        {
            try
            {
                bool healthy = await _aiService.IsHealthyAsync();
                return Json(new { success = true, aiServiceOnline = healthy });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, aiServiceOnline = false, error = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  AI FEATURE ENDPOINTS — triggered by frontend or admin
        // ═══════════════════════════════════════════════════════════════════

        // ── POST api/ML/CategorizeText ────────────────────────────────────────
        // Called by Angular form as user types — suggests a category

        // Phase 4: AiService backs this. Response shape kept identical to the
        // pre-migration wire — { success, suggestions, suggestedDescription? }
        // — so ml.service.ts:categorizeText() mapper is unchanged.
        [HttpPost]
        public async Task<JsonResult> CategorizeText([FromBody] CategorizeTextRequest request)
        {
            try
            {
                var result = await _aiService.CategorizeTextAsync(
                    request.ComplaintId, request.Title, request.Description);
                return Json(new
                {
                    success              = true,
                    suggestions          = result.Suggestions,
                    suggestedDescription = result.SuggestedDescription,
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, suggestions = new List<object>(), error = ex.Message });
            }

        }
        // ── POST api/ML/AnalyzeImage ──────────────────────────────────────────
        // Called after image upload — returns category suggestion + OCR text

        // Phase 4: AiService backs this. Wire stays { success, result: {
        // complaintId, suggestions, ocrText, gpsLat, gpsLon,
        // suggestedDescription } } so ml.service.ts:analyzeImage() is unchanged.
        [HttpPost]
        public async Task<JsonResult> AnalyzeImage([FromBody] AnalyzeImageRequest request)
        {
            try
            {
                var result = await _aiService.AnalyzeImageAsync(request.ComplaintId, request.FilePath);
                return Json(new { success = true, result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── POST api/ML/CheckDuplicates ───────────────────────────────────────
        // Called on submit form — warns citizen of similar existing complaints

        // Phase 5: AiService backs dedup. Vector embeddings now generated by
        // Gemini text-embedding-004 (768-dim) and persisted via EF in the
        // same ComplaintEmbeddings table the Python service used.
        // Wire shape { success, result: { complaintId, candidates, embeddingStored } }
        // unchanged — ml.service.ts:checkDuplicates() mapping still applies.
        [HttpPost]
        public async Task<JsonResult> CheckDuplicates([FromBody] CheckDuplicatesRequest request)
        {
            try
            {
                var result = await _aiService.CheckDuplicatesAsync(
                    request.ComplaintId, request.Title, request.Description,
                    request.CategoryId, request.LocalityId, request.ExcludeId);
                return Json(new { success = true, result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }
        // ── POST api/ML/GetForecast ───────────────────────────────────────────
        // Admin dashboard — Prophet trend forecast

        // Phase 6.1: AiService backs the admin trend narrative. Response
        // shape { success, result } unchanged — Angular treats result as
        // `any` (see ml.service.ts:getForecast).
        [HttpPost]
        public async Task<JsonResult> GetForecast(ForecastRequest request)
        {
            try
            {
                var result = await _aiService.GetForecastAsync(
                    request.CategoryId, request.Periods);
                return Json(new { success = true, result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // Phase 6.3: PWG verdict — new MLController action. Solver workflow
        // calls this with { complaintDescription, progressNote } and gets
        // { verdict: "APPROVE"|"REDO", reason, confidence }.
        [HttpPost]
        public async Task<JsonResult> GetPwgVerdict([FromBody] PwgVerdictRequest request)
        {
            try
            {
                var result = await _aiService.GetPwgVerdictAsync(
                    request.ComplaintDescription, request.ProgressNote);
                return Json(new { success = true, result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── POST api/ML/Chat ──────────────────────────────────────────────────
        // Citizen chatbot — proxies to Ollama via Python service

        // Phase 4: AiService backs the chatbot. DB context injection now
        // happens inside AiService.ChatAsync via FetchComplaintContextAsync.
        // Wire response { success, reply } unchanged.
        [HttpPost]
        public async Task<JsonResult> Chat(ChatRequest request)
        {
            try
            {
                var aiMessages = request.Messages
                    .Select(m => new ChatMessage { Role = m.Role, Content = m.Content })
                    .ToList();

                string reply = await _aiService.ChatAsync(aiMessages, request.ComplaintLookupId);
                return Json(new { success = true, reply });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    reply = "Chatbot is temporarily unavailable.",
                    error = ex.Message
                });
            }
        }

    }

    // ── Internal DTO for tag query ────────────────────────────────────────────
    public class ComplaintTagDto
    {
        public string Tag { get; set; }
        public decimal? Score { get; set; }
    }
}
