// FixMyCity.API/Models/AIModels.cs

namespace FixMyCity.API.Models
{
    // ── Chatbot ───────────────────────────────────────────────────────────────
    public class ChatRequest
    {
        public List<ChatMessageDto> Messages { get; set; } = new();
        public int? ComplaintLookupId { get; set; }
    }

    public class ChatMessageDto
    {
        public string Role { get; set; }
        public string Content { get; set; }
    }

    // ── Trend forecast ────────────────────────────────────────────────────────
    public class ForecastRequest
    {
        public int? CategoryId { get; set; }
        public int Periods { get; set; } = 30;
    }

    // ── ISSUE 6 FIX: Request DTOs for frontend-triggered POST endpoints ────────
    // These replace loose primitive params that were incorrectly binding from
    // query string instead of the JSON request body.

    public class CategorizeTextRequest
    {
        public int ComplaintId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
    }

    public class AnalyzeImageRequest
    {
        public int ComplaintId { get; set; }
        public string FilePath { get; set; }
    }

    public class CheckDuplicatesRequest
    {
        public int ComplaintId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public int CategoryId { get; set; }
        public int LocalityId { get; set; }
        public int ExcludeId { get; set; }
    }

    // Phase 6.3 — solver invokes /api/ML/GetPwgVerdict to get an AI-suggested
    // APPROVE/REDO verdict on a PWG progress update.
    public class PwgVerdictRequest
    {
        public int    RequestId             { get; set; }
        public string ComplaintDescription  { get; set; } = "";
        public string ProgressNote          { get; set; } = "";
    }
}