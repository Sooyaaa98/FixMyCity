// FixMyCity.API/Services/AiService.cs
//
// Phase-1 SKELETON. Scoped service that will wrap all outbound AI HTTP calls
// (Google Gemini + OpenAI moderation) as the Python ml_service is retired.
//
// This commit (Phase 1.2) adds only the constructor, config accessors, shared
// helpers, and IsHealthyAsync. Feature methods (AnalyzeImage, CategorizeText,
// Chat, CheckDuplicates, Score, AutoTag, PwgVerdict, Forecast,
// Recommendations) land incrementally in Phases 3-6 per implementation_order.md.
//
// CONTRACT: every public method is fail-open. Exceptions are caught and a
// safe, documented fallback is returned. Callers never need their own try/catch.
//
// DI: registered as Scoped in Program.cs because it holds FixMyCityDbContext.
// Background callers (AIPendingQueueProcessor) MUST resolve via
// IServiceScopeFactory.CreateScope() — see risk_analysis.md R6.

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FixMyCity.DAL.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace FixMyCity.API.Services
{
    public class AiService
    {
        private readonly IHttpClientFactory _http;
        private readonly IConfiguration _config;
        private readonly FixMyCityDbContext _db;
        private readonly ILogger<AiService> _logger;
        private readonly IWebHostEnvironment _env;

        // ── Config accessors ─────────────────────────────────────────────────
        // Read each call so a live appsettings change (rare in prod) is picked
        // up without restart. Defaults match appsettings.json placeholders.

        // ── Groq (primary text AI — OpenAI-compatible) ──────────────────────
        private string GroqKey       => _config["Groq:ApiKey"] ?? "";
        private string GroqModel     => _config["Groq:Model"] ?? "llama-3.3-70b-versatile";
        private string GroqFastModel => _config["Groq:FastModel"] ?? "llama-3.1-8b-instant";
        private string GroqBase      => _config["Groq:BaseUrl"] ?? "https://api.groq.com/openai/v1";
        private bool   GroqConfigured =>
            !string.IsNullOrWhiteSpace(GroqKey) &&
            !GroqKey.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase);

        // ── Gemini (image analysis + embeddings only) ────────────────────────
        private string GeminiKey   => _config["Gemini:ApiKey"] ?? "";
        private string GeminiModel => _config["Gemini:Model"] ?? "gemini-2.0-flash";
        private string GeminiEmbed => _config["Gemini:EmbedModel"] ?? "models/text-embedding-004";
        private string GeminiBase  => _config["Gemini:BaseUrl"]
                                      ?? "https://generativelanguage.googleapis.com/v1beta";

        public AiService(
            IHttpClientFactory http,
            IConfiguration config,
            FixMyCityDbContext db,
            ILogger<AiService> logger,
            IWebHostEnvironment env)
        {
            _http   = http;
            _config = config;
            _db     = db;
            _logger = logger;
            _env    = env;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Shared helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Low-level POST to Gemini's generateContent endpoint. Throws on
        /// network / non-2xx — public feature methods wrap it in try/catch
        /// and translate failures into their own documented fallback.
        /// </summary>
        internal async Task<string> GeminiGenerateAsync(
            object requestBody, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(GeminiKey) ||
                GeminiKey.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Gemini:ApiKey is not configured (placeholder still in appsettings).");
            }

            var url = $"{GeminiBase}/models/{GeminiModel}:generateContent?key={GeminiKey}";
            var json = JsonSerializer.Serialize(requestBody);
            using var content  = new StringContent(json, Encoding.UTF8, "application/json");
            using var client   = _http.CreateClient();
            client.Timeout     = TimeSpan.FromSeconds(30);
            using var response = await client.PostAsync(url, content, ct);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement
                      .GetProperty("candidates")[0]
                      .GetProperty("content")
                      .GetProperty("parts")[0]
                      .GetProperty("text")
                      .GetString() ?? "";
        }

        /// <summary>
        /// Low-level POST to Groq's OpenAI-compatible /chat/completions endpoint.
        /// Pass <paramref name="model"/> = null to use the configured GroqModel
        /// (llama-3.3-70b-versatile), or GroqFastModel for high-volume cheap calls.
        /// Throws on non-2xx — callers wrap in try/catch.
        /// </summary>
        internal async Task<string> GroqGenerateAsync(
            string systemPrompt,
            string userPrompt,
            string? model = null,
            int maxTokens = 1024,
            CancellationToken ct = default)
        {
            if (!GroqConfigured)
                throw new InvalidOperationException(
                    "Groq:ApiKey is not configured (placeholder still in appsettings).");

            var url  = $"{GroqBase}/chat/completions";
            var body = new
            {
                model    = model ?? GroqModel,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user",   content = userPrompt   },
                },
                max_tokens  = maxTokens,
                temperature = 0.1,
            };

            var json = JsonSerializer.Serialize(body);
            using var content  = new StringContent(json, Encoding.UTF8, "application/json");
            using var client   = _http.CreateClient();
            client.Timeout     = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", GroqKey);

            using var response = await client.PostAsync(url, content, ct);
            response.EnsureSuccessStatusCode();

            var raw = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(raw);
            return doc.RootElement
                      .GetProperty("choices")[0]
                      .GetProperty("message")
                      .GetProperty("content")
                      .GetString() ?? "";
        }

        /// <summary>
        /// Multi-turn Groq conversation (for ChatAsync). Accepts a pre-built
        /// list of { role, content } message objects.
        /// </summary>
        internal async Task<string> GroqConversationAsync(
            object[] messages,
            int maxTokens = 512,
            CancellationToken ct = default)
        {
            if (!GroqConfigured)
                throw new InvalidOperationException("Groq:ApiKey not configured.");

            var url  = $"{GroqBase}/chat/completions";
            var body = new
            {
                model       = GroqModel,
                messages,
                max_tokens  = maxTokens,
                temperature = 0.2,
            };

            var json = JsonSerializer.Serialize(body);
            using var content  = new StringContent(json, Encoding.UTF8, "application/json");
            using var client   = _http.CreateClient();
            client.Timeout     = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", GroqKey);

            using var response = await client.PostAsync(url, content, ct);
            response.EnsureSuccessStatusCode();

            var raw = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(raw);
            return doc.RootElement
                      .GetProperty("choices")[0]
                      .GetProperty("message")
                      .GetProperty("content")
                      .GetString() ?? "";
        }

        /// <summary>
        /// Returns a Gemini text-embedding-004 vector (768-dim) for the input.
        /// Fail-open: returns empty list on any error so callers can fall back
        /// to non-vector recommendation paths.
        /// </summary>
        public async Task<List<float>> GetEmbeddingAsync(
            string text, CancellationToken ct = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(GeminiKey) ||
                    GeminiKey.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase))
                {
                    return new List<float>();
                }

                var url  = $"{GeminiBase}/{GeminiEmbed}:embedContent?key={GeminiKey}";
                var body = new
                {
                    model    = GeminiEmbed,
                    content  = new { parts = new[] { new { text } } },
                    taskType = "SEMANTIC_SIMILARITY",
                    outputDimensionality = 768
                };
                var json = JsonSerializer.Serialize(body);
                using var content  = new StringContent(json, Encoding.UTF8, "application/json");
                using var client   = _http.CreateClient();
                client.Timeout     = TimeSpan.FromSeconds(15);
                using var response = await client.PostAsync(url, content, ct);
                response.EnsureSuccessStatusCode();

                var raw = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(raw);
                return doc.RootElement
                          .GetProperty("embedding")
                          .GetProperty("values")
                          .EnumerateArray()
                          .Select(v => v.GetSingle())
                          .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AiService] GetEmbeddingAsync failed");
                return new List<float>();
            }
        }

        /// <summary>Cosine similarity between two equal-length vectors. 0 on shape mismatch.</summary>
        public static float CosineSimilarity(List<float> a, List<float> b)
        {
            if (a is null || b is null || a.Count == 0 || b.Count == 0 || a.Count != b.Count)
                return 0f;

            float dot = 0, normA = 0, normB = 0;
            for (int i = 0; i < a.Count; i++)
            {
                dot   += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }
            float denom = MathF.Sqrt(normA) * MathF.Sqrt(normB);
            return denom == 0 ? 0f : dot / denom;
        }

        /// <summary>
        /// Strips ```json ... ``` markdown fences that Gemini sometimes wraps
        /// around JSON responses despite "return only JSON" prompts.
        /// </summary>
        internal static string StripFences(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return raw ?? "";
            var s = raw.Trim();
            if (!s.StartsWith("```")) return s;

            var lines = s.Split('\n').ToList();
            lines.RemoveAt(0);                                  // drop opening ``` (or ```json)
            if (lines.LastOrDefault()?.TrimEnd() == "```")
                lines.RemoveAt(lines.Count - 1);                // drop closing ```
            return string.Join('\n', lines).Trim();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Health
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Pings Groq (primary) with a minimal chat request.
        /// Falls back to a Gemini probe if Groq is not configured.
        /// Returns false on any failure (network, auth, quota).
        /// Used by GET /api/ML/CheckAIHealth.
        /// </summary>
        public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
        {
            // Try Groq first (primary text AI).
            if (GroqConfigured)
            {
                try
                {
                    var reply = await GroqGenerateAsync(
                        systemPrompt: "You are a health-check bot.",
                        userPrompt:   "Reply with exactly one word: OK",
                        model:        GroqFastModel,
                        maxTokens:    5,
                        ct:           ct);
                    return !string.IsNullOrWhiteSpace(reply);
                }
                catch { /* fall through to Gemini probe */ }
            }

            // Fallback: probe Gemini (used for images/embeddings).
            try
            {
                if (string.IsNullOrWhiteSpace(GeminiKey) ||
                    GeminiKey.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase))
                    return false;

                var probe = new
                {
                    contents = new[] { new { parts = new[] { new { text = "ping" } } } },
                    generationConfig = new { maxOutputTokens = 5 },
                };
                var r = await GeminiGenerateAsync(probe, ct);
                return r != null;
            }
            catch { return false; }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Phase 3 — synchronous user-facing AI features
        // ─────────────────────────────────────────────────────────────────────
        //
        // DTO shapes returned by these methods MUST keep camelCase property
        // names so the Angular ml.service.ts mappers find the fields they
        // expect (see fmc-interfaces/ml.interface.ts and migration_plan.md §5).
        // Suggestions are anonymous objects with { categoryId, categoryName,
        // confidence } — never snake_case.

        /// <summary>
        /// Returns AI-suggested categories for a complaint's free-text title +
        /// description, plus a short rewritten description ready for the
        /// submit form. Wire shape:
        ///   { suggestions: [{ categoryId, categoryName, confidence }],
        ///     suggestedDescription }
        /// Fail-open: returns empty suggestions on any error.
        /// </summary>
        public async Task<CategorizeTextResult> CategorizeTextAsync(
            int complaintId,
            string title,
            string description,
            CancellationToken ct = default)
        {
            var empty = new CategorizeTextResult { ComplaintId = complaintId };

            try
            {
                var categories = await FetchCategoriesAsync(ct);
                if (categories.Count == 0) return empty;

                var catList = string.Join("\n",
                    categories.Select(c => $"{c.CategoryId}. {c.CategoryName}"));

                var prompt = $$"""
                    Classify this civic complaint from Bengaluru, India.

                    TITLE: {{title}}
                    DESCRIPTION: {{description}}

                    Available categories (use these exact IDs and names):
                    {{catList}}

                    Return ONLY valid JSON in this exact shape, no markdown, no commentary:
                    {
                      "suggestions": [
                        { "categoryId": <int>, "categoryName": "<exact name>", "confidence": <float 0-1> }
                      ],
                      "suggestedDescription": "<one or two sentences describing the issue>"
                    }

                    Rules:
                    - Up to 3 suggestions, sorted by confidence desc.
                    - confidence must be between 0 and 1.
                    - suggestedDescription: rewrite the complaint in clear, neutral language.
                    """;

                // Use fast Groq model — structured JSON output, low latency.
                var raw = await GroqGenerateAsync(
                    systemPrompt: "You are a civic-complaint categorisation assistant for Bengaluru, India. Return ONLY valid JSON — no markdown, no commentary.",
                    userPrompt:   prompt,
                    model:        GroqFastModel,
                    maxTokens:    512,
                    ct:           ct);

                using var doc = JsonDocument.Parse(StripFences(raw));

                var suggestions = ExtractSuggestions(doc.RootElement, categories);
                var suggestedDesc = doc.RootElement.TryGetProperty("suggestedDescription", out var d)
                    ? d.GetString() ?? "" : "";

                return new CategorizeTextResult
                {
                    ComplaintId          = complaintId,
                    Suggestions          = suggestions,
                    SuggestedDescription = suggestedDesc,
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AiService] CategorizeTextAsync failed");
                return empty;
            }
        }

        /// <summary>
        /// Analyses a complaint photo: returns AI category suggestions, OCR
        /// text, and a one-line description. GPS fields are returned as null
        /// (EXIF extraction is browser-side / future work).
        /// `filePath` may be either an https URL (Cloudinary or other) or a
        /// disk basename under Uploads:BasePath.
        /// Wire shape:
        ///   { complaintId, suggestions: [...], ocrText, gpsLat, gpsLon,
        ///     suggestedDescription }
        /// </summary>
        public async Task<ImageAnalysisResult> AnalyzeImageAsync(
            int complaintId,
            string filePath,
            CancellationToken ct = default)
        {
            var empty = new ImageAnalysisResult { ComplaintId = complaintId };

            try
            {
                if (string.IsNullOrWhiteSpace(filePath)) return empty;

                var (bytes, mime) = await LoadImageBytesAsync(filePath, ct);
                if (bytes is null || bytes.Length == 0) return empty;

                var categories = await FetchCategoriesAsync(ct);
                if (categories.Count == 0) return empty;

                var catList = string.Join("\n",
                    categories.Select(c => $"{c.CategoryId}. {c.CategoryName}"));

                var prompt = $$"""
                    Analyse this civic complaint photo from Bengaluru, India.

                    Available categories (use these exact IDs and names):
                    {{catList}}

                    Return ONLY valid JSON in this exact shape, no markdown:
                    {
                      "suggestions": [
                        { "categoryId": <int>, "categoryName": "<exact name>", "confidence": <float 0-1> }
                      ],
                      "ocrText": "<any text visible in the image, or empty string>",
                      "suggestedDescription": "<one or two sentence description of the civic issue>"
                    }

                    Rules:
                    - Up to 3 suggestions, sorted by confidence desc.
                    - ocrText: include any sign text, addresses, numbers visible. Empty string if none.
                    - suggestedDescription: focus on the civic problem (pothole, leak, garbage, etc.).
                    """;

                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new object[]
                            {
                                new { text = prompt },
                                new { inline_data = new { mime_type = mime, data = Convert.ToBase64String(bytes) } },
                            },
                        },
                    },
                };

                var raw = await GeminiGenerateAsync(requestBody, ct);
                using var doc = JsonDocument.Parse(StripFences(raw));
                var root = doc.RootElement;

                var suggestions = ExtractSuggestions(root, categories);
                var ocrText = root.TryGetProperty("ocrText", out var o) ? o.GetString() ?? "" : "";
                var suggestedDesc = root.TryGetProperty("suggestedDescription", out var d)
                    ? d.GetString() ?? "" : "";

                return new ImageAnalysisResult
                {
                    ComplaintId          = complaintId,
                    Suggestions          = suggestions,
                    OcrText              = ocrText,
                    GpsLat               = null,
                    GpsLon               = null,
                    SuggestedDescription = suggestedDesc,
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AiService] AnalyzeImageAsync failed for {filePath}", filePath);
                return empty;
            }
        }

        /// <summary>
        /// Toxicity check via OpenAI's free moderation endpoint. Fail-open:
        /// any error returns isToxic=false so civic submissions are never
        /// blocked by an outage. Mirrors MLServiceClient.CheckToxicityAsync.
        /// </summary>
        public async Task<ToxicityResult> CheckToxicityAsync(
            int complaintId,
            string text,
            CancellationToken ct = default)
        {
            var safe = new ToxicityResult { IsToxic = false, Reason = "", Confidence = 0f };

            try
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    return safe;
                }

                if (!GroqConfigured) return safe;

                var prompt = $$"""
                    Analyze if this text contains toxic content (such as profanity, hate speech, harassment, threats, or severe vulgarity).

                    TEXT: {{text}}

                    Return ONLY valid JSON in this exact shape, no markdown, no commentary:
                    {
                      "isToxic": <bool>,
                      "reason": "<comma-separated categories violated, e.g., profanity, harassment, or empty string>",
                      "confidence": <float 0-1>
                    }
                    """;

                var raw = await GroqGenerateAsync(
                    systemPrompt: "You are a content moderation assistant. Return ONLY valid JSON — no markdown.",
                    userPrompt:   prompt,
                    model:        GroqFastModel,
                    maxTokens:    100,
                    ct:           ct);

                using var doc = JsonDocument.Parse(StripFences(raw));
                var root = doc.RootElement;

                bool isToxic = root.GetProperty("isToxic").GetBoolean();
                string reason = root.TryGetProperty("reason", out var r) ? r.GetString() ?? "" : "";
                float confidence = root.TryGetProperty("confidence", out var c) ? (float)c.GetDouble() : 0.8f;

                return new ToxicityResult
                {
                    IsToxic    = isToxic,
                    Reason     = reason,
                    Confidence = (float)Math.Round(confidence, 4),
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AiService] CheckToxicityAsync via Groq failed — allowing through");
                return safe;
            }
        }

        /// <summary>
        /// DB-grounded chatbot via Groq (Llama 3.3-70b).
        /// If the user mentions a complaint ID the authoritative DB record is
        /// injected into the last user turn so the model cannot hallucinate.
        /// Uses the OpenAI-compatible conversation format: system + history.
        /// </summary>
        public async Task<string> ChatAsync(
            List<ChatMessage> messages,
            int? complaintLookupId = null,
            CancellationToken ct = default)
        {
            try
            {
                if (messages is null || messages.Count == 0)
                    return "Please type a question.";

                if (!GroqConfigured)
                    return "The AI assistant is not configured. Please contact the administrator.";

                var lastUser = messages.LastOrDefault(m => m.Role == "user")?.Content ?? "";
                complaintLookupId ??= ExtractComplaintId(lastUser);

                string dbContext = "";
                if (complaintLookupId.HasValue && complaintLookupId.Value > 0)
                    dbContext = await FetchComplaintContextAsync(complaintLookupId.Value, ct);

                // Build OpenAI-format conversation: [system, ...history, user(last)]
                var groqMessages = new List<object>
                {
                    new { role = "system", content = ChatSystemPrompt }
                };

                foreach (var m in messages)
                {
                    var role    = m.Role == "assistant" ? "assistant" : "user";
                    var content = m.Content ?? "";

                    // Inject real DB data into the LAST user message only.
                    bool isLast = ReferenceEquals(m, messages[^1]);
                    if (isLast && role == "user" && !string.IsNullOrEmpty(dbContext))
                        content = $"[REAL DATA FROM DATABASE]\n{dbContext}\n[END DATA]\n\n{content}";

                    groqMessages.Add(new { role, content });
                }

                var raw = await GroqConversationAsync(groqMessages.ToArray(), maxTokens: 512, ct: ct);
                return string.IsNullOrWhiteSpace(raw)
                    ? "I couldn't generate a reply. Please try again."
                    : raw.Trim();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AiService] ChatAsync failed");
                return "The chatbot is temporarily unavailable. Please try again shortly.";
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Phase 5 — background AI: dedup, scoring, auto-tagging
        // ─────────────────────────────────────────────────────────────────────
        //
        // None of these methods are called directly from a public HTTP path —
        // they're invoked by AIPendingQueueProcessor (background) and by
        // ComplaintController.SubmitComplaint's fire-and-forget Task.Run.
        // Both call sites resolve AiService from a fresh IServiceScope so the
        // DbContext lifetime is correct (see risk_analysis.md R6).

        public const string EmbeddingModelVersion = "google-text-embedding-004";
        private const float DuplicateThreshold = 0.85f;
        private const float CandidateThreshold = 0.70f;

        /// <summary>
        /// Computes a Google embedding for the new complaint, compares it
        /// against existing embeddings in the same locality (same model
        /// version only — R13), and stores the new embedding via
        /// SaveEmbeddingAsync.
        ///
        /// Returns the shape Angular ml.service.ts:checkDuplicates expects:
        ///   { complaintId, candidates: [{ complaintId, similarity, isDuplicate }],
        ///     embeddingStored }
        /// </summary>
        public async Task<DuplicateCheckResult> CheckDuplicatesAsync(
            int complaintId,
            string title,
            string description,
            int categoryId,
            int localityId,
            int excludeId = 0,
            CancellationToken ct = default)
        {
            var empty = new DuplicateCheckResult { ComplaintId = complaintId };

            try
            {
                var combined = $"{title}. {description}".Trim();
                if (combined.Length < 3) return empty;

                var vec = await GetEmbeddingAsync(combined, ct);
                if (vec.Count == 0) return empty;

                // Fetch up to 500 recent same-locality open complaints with
                // current-model embeddings. R13: mixed model dims are unsafe
                // to compare; filtering by ModelVersion drops legacy rows.
                var pool = await _db.ComplaintEmbeddings
                    .AsNoTracking()
                    .Where(ce =>
                        ce.ModelVersion == EmbeddingModelVersion &&
                        ce.Complaint.LocalityId == localityId &&
                        ce.ComplaintId != excludeId &&
                        ce.ComplaintId != complaintId &&
                        ce.Complaint.Status != "Resolved" &&
                        ce.Complaint.Status != "Rejected" &&
                        ce.Complaint.Status != "Linked")
                    .OrderByDescending(ce => ce.GeneratedAt)
                    .Take(500)
                    .Select(ce => new { ce.ComplaintId, ce.EmbeddingJson })
                    .ToListAsync(ct);

                var hits = new List<DuplicateCandidate>();
                foreach (var row in pool)
                {
                    try
                    {
                        var other = JsonSerializer.Deserialize<List<float>>(row.EmbeddingJson ?? "[]");
                        if (other is null || other.Count == 0) continue;
                        var sim = CosineSimilarity(vec, other);
                        if (sim < CandidateThreshold) continue;

                        hits.Add(new DuplicateCandidate
                        {
                            ComplaintId = row.ComplaintId,
                            Similarity  = (float)Math.Round(sim, 4),
                            IsDuplicate = sim >= DuplicateThreshold,
                        });
                    }
                    catch { /* malformed row — skip silently */ }
                }

                var top = hits
                    .OrderByDescending(c => c.Similarity)
                    .Take(10)
                    .ToList();

                bool stored = false;
                if (complaintId > 0)
                    stored = await SaveEmbeddingAsync(complaintId, vec, ct);

                return new DuplicateCheckResult
                {
                    ComplaintId     = complaintId,
                    Candidates      = top,
                    EmbeddingStored = stored,
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AiService] CheckDuplicatesAsync failed for #{id}", complaintId);
                return empty;
            }
        }

        /// <summary>
        /// MERGE-style upsert on ComplaintEmbeddings keyed by ComplaintId.
        /// Returns false on any failure so callers can flag the embedding as
        /// "not stored" without aborting the surrounding workflow.
        /// </summary>
        public async Task<bool> SaveEmbeddingAsync(
            int complaintId, List<float> embedding, CancellationToken ct = default)
        {
            if (complaintId <= 0 || embedding is null || embedding.Count == 0) return false;

            try
            {
                var json = JsonSerializer.Serialize(embedding);

                // EF tracked path: FindAsync returns a tracked entity if it
                // exists (NoTracking only applies to LINQ queries, not Find).
                var existing = await _db.ComplaintEmbeddings
                    .FirstOrDefaultAsync(e => e.ComplaintId == complaintId, ct);

                if (existing is null)
                {
                    _db.ComplaintEmbeddings.Add(new ComplaintEmbedding
                    {
                        ComplaintId   = complaintId,
                        EmbeddingJson = json,
                        ModelVersion  = EmbeddingModelVersion,
                        GeneratedAt   = DateTime.UtcNow,
                    });
                }
                else
                {
                    _db.ComplaintEmbeddings.Attach(existing);
                    existing.EmbeddingJson = json;
                    existing.ModelVersion  = EmbeddingModelVersion;
                    existing.GeneratedAt   = DateTime.UtcNow;
                    _db.Entry(existing).State = EntityState.Modified;
                }

                await _db.SaveChangesAsync(ct);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AiService] SaveEmbeddingAsync failed for #{id}", complaintId);
                return false;
            }
        }

        /// <summary>
        /// Deterministic priority-score algorithm. No external AI call —
        /// weights are configured in AiScoring (appsettings.json). Persists
        /// the score (MERGE) to ComplaintMLScores so the solver dashboard
        /// can sort by priority.
        /// </summary>
        public async Task<MlScoreResult> ScoreComplaintAsync(
            int complaintId,
            int categoryId,
            string criticality,
            int localityId,
            int? deptId,
            bool hasPwg = false,
            decimal fundingAmount = 0m,
            bool wasEscalated = false,
            int descriptionLen = 0,
            int daysOpen = 0,
            CancellationToken ct = default)
        {
            var section = _config.GetSection("AiScoring");

            float score = section.GetSection("CriticalityWeights").GetValue<float>(criticality, 10f);

            if (hasPwg)
                score += section.GetValue<float>("PwgBonus", 10f);

            if (fundingAmount > 0)
            {
                var bonusPer100 = section.GetValue<float>("FundingBonusPer100", 5f);
                score += Math.Min(20f, (float)(fundingAmount / 100m) * bonusPer100);
            }

            if (wasEscalated)
                score += section.GetValue<float>("EscalationPenalty", -20f);

            if (daysOpen > 0)
            {
                var penalty = section.GetValue<float>("AgePenaltyPerDay", -0.5f);
                score += Math.Max(-30f, daysOpen * penalty);
            }

            if (descriptionLen > 0)
            {
                var bonus = section.GetValue<float>("DescriptionLengthBonus", 0.01f);
                score += Math.Min(5f, descriptionLen * bonus);
            }

            score = Math.Clamp(score, 0f, 100f);

            var resolutionProb = criticality switch
            {
                "Critical" => 0.45f,
                "High"     => 0.60f,
                "Medium"   => 0.72f,
                "Low"      => 0.85f,
                _          => 0.65f,
            };

            var baseDays = criticality switch
            {
                "Critical" => 7,
                "High"     => 14,
                "Medium"   => 21,
                _          => 30,
            };

            var predicted = DateTime.UtcNow.AddDays(baseDays);

            await SaveMlScoresAsync(complaintId, score, resolutionProb, predicted, ct);

            return new MlScoreResult
            {
                ComplaintId             = complaintId,
                PriorityScore           = (float)Math.Round(score, 2),
                ResolutionProbability   = (float)Math.Round(resolutionProb, 4),
                PredictedResolutionDate = predicted,
                ModelVersion            = "v2-scoring-dotnet",
            };
        }

        private async Task SaveMlScoresAsync(
            int complaintId, float priority, float prob, DateTime predicted, CancellationToken ct)
        {
            try
            {
                await _db.Database.ExecuteSqlRawAsync(@"
                    MERGE dbo.ComplaintMlScores AS target
                    USING (SELECT @p0 AS ComplaintId) AS source
                        ON target.ComplaintId = source.ComplaintId
                    WHEN MATCHED THEN
                        UPDATE SET
                            PriorityScore           = @p1,
                            ResolutionProbability   = @p2,
                            PredictedResolutionDate = @p3,
                            PredictionModelVersion  = @p4,
                            ScoredAt                = SYSDATETIME()
                    WHEN NOT MATCHED THEN
                        INSERT (ComplaintId, PriorityScore, ResolutionProbability,
                                PredictedResolutionDate, PredictionModelVersion, ScoredAt)
                        VALUES (@p0, @p1, @p2, @p3, @p4, SYSDATETIME());",
                    complaintId,
                    (decimal)priority,
                    (decimal)prob,
                    predicted,
                    "v2-scoring-dotnet",
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AiService] SaveMlScoresAsync failed for #{id}", complaintId);
            }
        }

        /// <summary>
        /// Auto-extracts up to 5 key tags from complaint text via Gemini and
        /// persists them via the existing usp_SaveComplaintTags SP so the
        /// Python-era callback semantics are preserved.
        /// </summary>
        public async Task<List<string>> AutoTagAsync(
            int complaintId,
            string title,
            string description,
            CancellationToken ct = default)
        {
            try
            {
                var prompt = $$"""
                    Extract the 5 most important key phrases from this civic complaint.
                    Each phrase becomes a searchable tag on the FixMyCity platform.

                    TITLE: {{title}}
                    DESCRIPTION: {{description}}

                    Return ONLY a JSON array of 5 strings, no markdown, no commentary.
                    Example: ["pothole", "road damage", "100ft road", "monsoon", "heavy vehicle"]
                    """;

                var raw = await GroqGenerateAsync(
                    systemPrompt: "You are a keyword-extraction assistant. Return ONLY a JSON array of strings — no markdown.",
                    userPrompt:   prompt,
                    model:        GroqFastModel,
                    maxTokens:    100,
                    ct:           ct);

                var tags = JsonSerializer.Deserialize<List<string>>(StripFences(raw))
                           ?? new List<string>();
                tags = tags.Select(t => t?.Trim() ?? "")
                           .Where(t => t.Length > 0)
                           .Take(5)
                           .ToList();

                if (tags.Count > 0 && complaintId > 0)
                    await SaveTagsAsync(complaintId, tags, ct);

                return tags;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AiService] AutoTagAsync failed for #{id}", complaintId);
                return new List<string>();
            }
        }

        private async Task SaveTagsAsync(int complaintId, List<string> tags, CancellationToken ct)
        {
            try
            {
                // Reuse the existing SP so the storage semantics (e.g. score
                // column population, audit row) match what the Python service
                // wrote in prior phases.
                var tagsJson = JsonSerializer.Serialize(tags);
                await _db.Database.ExecuteSqlRawAsync(
                    "EXEC dbo.usp_SaveComplaintTags @ComplaintId = {0}, @TagsJson = {1}",
                    complaintId, tagsJson);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AiService] SaveTagsAsync failed for #{id}", complaintId);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Phase 6 — admin & PWG features
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 30-day complaint trend stats + Gemini-generated narrative for the
        /// admin dashboard. Falls back to a stats-only response when Gemini
        /// is unavailable (R12). Caller may cache by (categoryId, periods).
        /// </summary>
        public async Task<ForecastNarrative> GetForecastAsync(
            int? categoryId = null, int periods = 30, CancellationToken ct = default)
        {
            var stats = await FetchTrendStatsAsync(categoryId, ct);
            if (stats is null)
            {
                return new ForecastNarrative
                {
                    Narrative = "Insufficient data to generate a forecast at this time.",
                    Trend     = "stable",
                    Hotspots  = new List<string>(),
                };
            }

            try
            {
                var json = JsonSerializer.Serialize(stats, new JsonSerializerOptions { WriteIndented = true });

                var prompt = $$"""
                    You are a civic data analyst for Bengaluru, India.

                    Complaint statistics for the last 30 days:
                    {{json}}

                    Write a brief trend analysis (3-4 sentences) covering:
                    1. Overall complaint volume trend (increasing/decreasing/stable)
                    2. The most problematic category or locality
                    3. Resolution performance
                    4. One actionable recommendation

                    Then return ONLY a JSON object (no markdown, no commentary):
                    {
                      "narrative": "<your 3-4 sentence analysis>",
                      "trend": "increasing" | "decreasing" | "stable",
                      "hotspots": ["<locality1>", "<locality2>"]
                    }
                    """;

                var raw = await GroqGenerateAsync(
                    systemPrompt: "You are a civic data analyst for Bengaluru, India. Return ONLY valid JSON — no markdown.",
                    userPrompt:   prompt,
                    model:        GroqModel,
                    maxTokens:    512,
                    ct:           ct);

                using var doc = JsonDocument.Parse(StripFences(raw));
                var root = doc.RootElement;

                var hotspots = new List<string>();
                if (root.TryGetProperty("hotspots", out var hs) && hs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var h in hs.EnumerateArray())
                        if (h.ValueKind == JsonValueKind.String)
                            hotspots.Add(h.GetString() ?? "");
                }

                return new ForecastNarrative
                {
                    Narrative = root.TryGetProperty("narrative", out var n)
                        ? n.GetString() ?? "" : "",
                    Trend = root.TryGetProperty("trend", out var t)
                        ? t.GetString() ?? "stable" : "stable",
                    Hotspots = hotspots,
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AiService] GetForecastAsync Groq call failed");
                return new ForecastNarrative
                {
                    Narrative = "Forecast temporarily unavailable. Stats are visible in the dashboard.",
                    Trend     = "stable",
                    Hotspots  = new List<string>(),
                };
            }
        }

        private async Task<TrendStats?> FetchTrendStatsAsync(
            int? categoryId, CancellationToken ct)
        {
            try
            {
                var cutoff = DateTime.UtcNow.AddDays(-30);

                var totalQuery = _db.Complaints.AsNoTracking()
                    .Where(c => c.SubmittedAt >= cutoff);
                if (categoryId.HasValue)
                    totalQuery = totalQuery.Where(c => c.CategoryId == (short)categoryId.Value);
                var total = await totalQuery.CountAsync(ct);
                if (total == 0) return null;

                var byStatus = await _db.Complaints.AsNoTracking()
                    .Where(c => c.SubmittedAt >= cutoff)
                    .GroupBy(c => c.Status)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToListAsync(ct);

                var byCategory = await _db.Complaints.AsNoTracking()
                    .Where(c => c.SubmittedAt >= cutoff)
                    .GroupBy(c => c.Category!.CategoryName)
                    .Select(g => new { Category = g.Key, Count = g.Count() })
                    .OrderByDescending(g => g.Count)
                    .Take(5)
                    .ToListAsync(ct);

                var byLocality = await _db.Complaints.AsNoTracking()
                    .Where(c => c.SubmittedAt >= cutoff)
                    .GroupBy(c => c.Locality!.LocalityName)
                    .Select(g => new { Locality = g.Key, Count = g.Count() })
                    .OrderByDescending(g => g.Count)
                    .Take(3)
                    .ToListAsync(ct);

                return new TrendStats
                {
                    TotalComplaints = total,
                    ByStatus        = byStatus.ToDictionary(x => x.Status ?? "Unknown", x => x.Count),
                    ByCategory      = byCategory.ToDictionary(x => x.Category ?? "Unknown", x => x.Count),
                    TopLocalities   = byLocality.ToDictionary(x => x.Locality ?? "Unknown", x => x.Count),
                    PeriodDays      = 30,
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AiService] FetchTrendStatsAsync failed");
                return null;
            }
        }

        /// <summary>
        /// Recommends open complaints relevant to the user's declared
        /// interests. Falls back through three tiers:
        ///   1. embedding similarity (preferred)
        ///   2. interest-filter (category match, recency)
        ///   3. plain recency
        /// </summary>
        public async Task<List<int>> GetRecommendationsAsync(
            int userId, int topN = 10, CancellationToken ct = default)
        {
            try
            {
                var interests = await _db.UserInterests
                    .AsNoTracking()
                    .Where(i => i.UserId == userId)
                    .Select(i => new { i.CategoryId, i.PreferredLocalityId })
                    .ToListAsync(ct);

                // No interests → recency only.
                if (interests.Count == 0)
                    return await RecentOpenComplaintIdsAsync(null, null, topN, ct);

                var catIds = interests.Where(i => i.CategoryId.HasValue)
                                      .Select(i => (short)i.CategoryId!.Value)
                                      .Distinct()
                                      .ToList();
                var locIds = interests.Where(i => i.PreferredLocalityId.HasValue)
                                      .Select(i => i.PreferredLocalityId!.Value)
                                      .Distinct()
                                      .ToList();

                var catNames = catIds.Count > 0
                    ? await _db.IssueCategories.AsNoTracking()
                        .Where(c => catIds.Contains(c.CategoryId))
                        .Select(c => c.CategoryName).ToListAsync(ct)
                    : new List<string>();
                var locNames = locIds.Count > 0
                    ? await _db.Localities.AsNoTracking()
                        .Where(l => locIds.Contains(l.LocalityId))
                        .Select(l => l.LocalityName).ToListAsync(ct)
                    : new List<string>();

                var profileText =
                    "civic complaints about: " + string.Join(", ", catNames)
                    + (locNames.Count > 0 ? " in " + string.Join(", ", locNames) : "");

                var profileVec = await GetEmbeddingAsync(profileText, ct);
                if (profileVec.Count == 0)
                    return await RecentOpenComplaintIdsAsync(catIds, locIds, topN, ct);

                var pool = await _db.ComplaintEmbeddings.AsNoTracking()
                    .Where(ce =>
                        ce.ModelVersion == EmbeddingModelVersion &&
                        ce.Complaint.Status != "Resolved" &&
                        ce.Complaint.Status != "Rejected" &&
                        ce.Complaint.Status != "Linked")
                    .OrderByDescending(ce => ce.Complaint.SubmittedAt)
                    .Take(1000)
                    .Select(ce => new { ce.ComplaintId, ce.EmbeddingJson })
                    .ToListAsync(ct);

                var scored = new List<(int Id, float Sim)>();
                foreach (var row in pool)
                {
                    try
                    {
                        var v = JsonSerializer.Deserialize<List<float>>(row.EmbeddingJson ?? "[]");
                        if (v is null || v.Count == 0) continue;
                        scored.Add((row.ComplaintId, CosineSimilarity(profileVec, v)));
                    }
                    catch { /* skip */ }
                }

                var top = scored
                    .OrderByDescending(x => x.Sim)
                    .Take(topN)
                    .Select(x => x.Id)
                    .ToList();

                // If embedding pool was empty for current model, fall back.
                return top.Count > 0
                    ? top
                    : await RecentOpenComplaintIdsAsync(catIds, locIds, topN, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AiService] GetRecommendationsAsync failed for user #{id}", userId);
                return new List<int>();
            }
        }

        private async Task<List<int>> RecentOpenComplaintIdsAsync(
            List<short>? catIds, List<int>? locIds, int topN, CancellationToken ct)
        {
            try
            {
                var q = _db.Complaints.AsNoTracking()
                    .Where(c => c.Status != "Resolved" &&
                                c.Status != "Rejected" &&
                                c.Status != "Linked");

                if (catIds is { Count: > 0 })
                    q = q.Where(c => catIds.Contains(c.CategoryId));
                if (locIds is { Count: > 0 })
                    q = q.Where(c => locIds.Contains(c.LocalityId));

                return await q.OrderByDescending(c => c.SubmittedAt)
                              .Take(topN)
                              .Select(c => c.ComplaintId)
                              .ToListAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AiService] RecentOpenComplaintIdsAsync failed");
                return new List<int>();
            }
        }

        /// <summary>
        /// PWG progress verdict. Reviews the citizen complaint against the
        /// volunteer's progress note and returns APPROVE or REDO with a one-
        /// sentence reason. Defaults to REDO on AI failure so a human still
        /// has to look.
        /// </summary>
        public async Task<PwgVerdictResult> GetPwgVerdictAsync(
            string complaintDescription,
            string progressNote,
            CancellationToken ct = default)
        {
            var safe = new PwgVerdictResult
            {
                Verdict    = "REDO",
                Reason     = "AI unavailable — manual review required.",
                Confidence = 0f,
            };

            try
            {
                if (string.IsNullOrWhiteSpace(complaintDescription) ||
                    string.IsNullOrWhiteSpace(progressNote))
                {
                    return safe;
                }

                var prompt = $$"""
                    You are reviewing a community volunteer's (PWG) work on a civic complaint.

                    ORIGINAL COMPLAINT:
                    {{complaintDescription}}

                    PWG PROGRESS NOTE:
                    {{progressNote}}

                    Does the progress note indicate the complaint has been addressed?

                    Return ONLY this JSON (no markdown, no commentary):
                    {
                      "verdict": "APPROVE" | "REDO",
                      "reason": "<one sentence>",
                      "confidence": <float 0.0 to 1.0>
                    }

                    APPROVE if the note describes completing work relevant to the complaint.
                    REDO if the note is vague, unrelated, or clearly incomplete.
                    """;

                var raw = await GroqGenerateAsync(
                    systemPrompt: "You are a civic work-review assistant. Return ONLY valid JSON — no markdown.",
                    userPrompt:   prompt,
                    model:        GroqFastModel,
                    maxTokens:    150,
                    ct:           ct);

                using var doc = JsonDocument.Parse(StripFences(raw));
                var root = doc.RootElement;

                var verdict = root.TryGetProperty("verdict", out var v) ? v.GetString() ?? "REDO" : "REDO";
                if (verdict != "APPROVE" && verdict != "REDO") verdict = "REDO";

                var reason = root.TryGetProperty("reason", out var r)
                    ? r.GetString() ?? "" : "";
                var conf = root.TryGetProperty("confidence", out var c)
                          && c.ValueKind == JsonValueKind.Number
                    ? c.GetSingle() : 0.7f;

                return new PwgVerdictResult
                {
                    Verdict    = verdict,
                    Reason     = reason,
                    Confidence = (float)Math.Round(Math.Clamp(conf, 0f, 1f), 4),
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AiService] GetPwgVerdictAsync failed");
                return safe;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Phase 3 private helpers
        // ─────────────────────────────────────────────────────────────────────

        private const string ChatSystemPrompt =
            "You are the FixMyCity assistant — a helpful civic engagement bot for Bengaluru, India.\n" +
            "You help citizens check complaint status, understand how the platform works (submissions, PWG, points), " +
            "and learn about civic departments (BBMP, BWSSB, BESCOM).\n\n" +
            "STRICT RULES:\n" +
            "1. You ONLY answer about FixMyCity. Politely refuse unrelated questions.\n" +
            "2. When the database section above contains complaint data, use it verbatim. Never fabricate IDs, statuses, or dates.\n" +
            "3. Be concise but well-structured. Use bullet points for lists, bold (**text**) for key terms like status or dates.\n" +
            "4. If a referenced complaint isn't in the data block, say so plainly.\n\n" +
            "FORMATTING RULES (always follow):\n" +
            "- For complaint status replies: always show the ID, Title, Status, Department on separate lines using **bold** labels.\n" +
            "- For lists (steps, departments, tips): use dash bullet points, one item per line.\n" +
            "- Keep each reply under 120 words unless the user explicitly asks for more detail.\n" +
            "- Never use markdown headers (##) — only bold and bullets.\n" +
            "- End factual replies with a follow-up offer, e.g. 'Want more details on this complaint?'";

        private async Task<List<IssueCategory>> FetchCategoriesAsync(CancellationToken ct)
        {
            try
            {
                return await _db.IssueCategories
                    .AsNoTracking()
                    .OrderBy(c => c.CategoryId)
                    .ToListAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AiService] FetchCategoriesAsync failed");
                return new List<IssueCategory>();
            }
        }

        /// <summary>
        /// Extracts category suggestions from a Gemini JSON response and
        /// validates them against the DB-resident category list. Drops any
        /// categoryId not in the DB to prevent invalid FK references downstream.
        /// </summary>
        private static List<CategorySuggestion> ExtractSuggestions(
            JsonElement root, List<IssueCategory> categories)
        {
            var result = new List<CategorySuggestion>();
            if (!root.TryGetProperty("suggestions", out var suggs) ||
                suggs.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            var byId = categories.ToDictionary(c => (int)c.CategoryId, c => c.CategoryName);

            foreach (var s in suggs.EnumerateArray())
            {
                if (s.ValueKind != JsonValueKind.Object) continue;
                if (!s.TryGetProperty("categoryId", out var idEl)) continue;
                int id = idEl.ValueKind == JsonValueKind.Number ? idEl.GetInt32() : 0;
                if (!byId.TryGetValue(id, out var dbName)) continue;

                float conf = s.TryGetProperty("confidence", out var c) &&
                             c.ValueKind == JsonValueKind.Number
                    ? c.GetSingle() : 0.5f;
                conf = Math.Clamp(conf, 0f, 1f);

                result.Add(new CategorySuggestion
                {
                    CategoryId   = id,
                    CategoryName = dbName,           // canonical DB name, not whatever Gemini said
                    Confidence   = (float)Math.Round(conf, 4),
                });

                if (result.Count >= 3) break;
            }

            return result;
        }

        private async Task<(byte[]? bytes, string mime)> LoadImageBytesAsync(
            string filePath, CancellationToken ct)
        {
            try
            {
                if (filePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    filePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    using var client = _http.CreateClient();
                    client.Timeout   = TimeSpan.FromSeconds(15);
                    using var resp   = await client.GetAsync(filePath, ct);
                    resp.EnsureSuccessStatusCode();
                    var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
                    var mime  = resp.Content.Headers.ContentType?.MediaType ?? GuessMime(filePath);
                    return (bytes, mime);
                }

                // Disk fallback. filePath is a basename under Uploads:BasePath.
                // Use ContentRootPath as the anchor (same as ComplaintController)
                // so relative paths like "../FixMyCityUploads" resolve correctly
                // regardless of the process working directory.
                var basePath = _config["Uploads:BasePath"] ?? "../FixMyCityUploads";
                var baseDir  = Path.IsPathRooted(basePath)
                    ? basePath
                    : Path.GetFullPath(Path.Combine(_env.ContentRootPath, basePath));
                var full = Path.Combine(baseDir, Path.GetFileName(filePath));
                if (!File.Exists(full)) return (null, "");
                var bytesOnDisk = await File.ReadAllBytesAsync(full, ct);
                return (bytesOnDisk, GuessMime(filePath));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AiService] LoadImageBytesAsync failed for {filePath}", filePath);
                return (null, "");
            }
        }

        private static string GuessMime(string path) =>
            Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".png"  => "image/png",
                ".webp" => "image/webp",
                ".gif"  => "image/gif",
                _       => "image/jpeg",
            };

        private static int? ExtractComplaintId(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            // Try the strongest patterns first to avoid grabbing arbitrary
            // digits in unrelated context.
            var patterns = new[]
            {
                @"\bcomplaint\s*#?\s*(\d+)\b",
                @"\bticket\s*#?\s*(\d+)\b",
                @"#\s*(\d+)\b",
            };
            foreach (var p in patterns)
            {
                var m = Regex.Match(text, p, RegexOptions.IgnoreCase);
                if (m.Success && int.TryParse(m.Groups[1].Value, out var id) && id > 0)
                    return id;
            }
            return null;
        }

        private async Task<string> FetchComplaintContextAsync(int complaintId, CancellationToken ct)
        {
            try
            {
                var c = await _db.Complaints
                    .AsNoTracking()
                    .Include(x => x.Category)
                    .Include(x => x.Locality)
                    .Include(x => x.Department)
                    .FirstOrDefaultAsync(x => x.ComplaintId == complaintId, ct);

                if (c is null) return $"No complaint found with ID {complaintId}.";

                var latest = await _db.ComplaintTimelines
                    .AsNoTracking()
                    .Where(t => t.ComplaintId == complaintId)
                    .OrderByDescending(t => t.CreatedAt)
                    .Select(t => new { t.NewStatus, t.CreatedAt })
                    .FirstOrDefaultAsync(ct);

                var sb = new StringBuilder();
                sb.Append($"Complaint #{c.ComplaintId}: {c.Title}\n");
                sb.Append($"Status: {c.Status} | Criticality: {c.Criticality}\n");
                sb.Append($"Category: {c.Category?.CategoryName ?? "—"} | Area: {c.Locality?.LocalityName ?? "—"}\n");
                sb.Append($"Department: {c.Department?.DeptName ?? "Not yet assigned"}\n");
                sb.Append($"Submitted: {c.SubmittedAt:MMM dd, yyyy}\n");
                if (latest != null)
                    sb.Append($"Latest update: {latest.NewStatus} on {latest.CreatedAt:MMM dd, yyyy}");
                else
                    sb.Append("Latest update: no status changes yet");

                return sb.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AiService] FetchComplaintContextAsync failed for #{id}", complaintId);
                return "";
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  AiService result DTOs
    // ─────────────────────────────────────────────────────────────────────────
    //
    // Strongly typed so MLController can pluck individual fields when wrapping
    // the response. Property names use PascalCase; JsonNamingPolicy.CamelCase
    // in Program.cs lowercases them on the wire (suggestions, ocrText, etc.).
    //
    // CategorySuggestion in MLServiceClient.cs is REUSED — same shape
    // (CategoryId / CategoryName / Confidence). Keeping a single class avoids
    // controllers having to translate between two near-identical types during
    // the Phase 4 swap.

    public class CategorizeTextResult
    {
        public int                      ComplaintId          { get; set; }
        public List<CategorySuggestion> Suggestions          { get; set; } = new();
        public string                   SuggestedDescription { get; set; } = "";
    }

    public class ImageAnalysisResult
    {
        public int                      ComplaintId          { get; set; }
        public List<CategorySuggestion> Suggestions          { get; set; } = new();
        public string                   OcrText              { get; set; } = "";
        public double?                  GpsLat               { get; set; }
        public double?                  GpsLon               { get; set; }
        public string                   SuggestedDescription { get; set; } = "";
    }

    public class MlScoreResult
    {
        public int       ComplaintId             { get; set; }
        public float     PriorityScore           { get; set; }
        public float     ResolutionProbability   { get; set; }
        public DateTime  PredictedResolutionDate { get; set; }
        public string    ModelVersion            { get; set; } = "";
    }

    public class ForecastNarrative
    {
        public string       Narrative { get; set; } = "";
        public string       Trend     { get; set; } = "stable";
        public List<string> Hotspots  { get; set; } = new();
    }

    public class TrendStats
    {
        public int                       TotalComplaints { get; set; }
        public Dictionary<string, int>   ByStatus        { get; set; } = new();
        public Dictionary<string, int>   ByCategory      { get; set; } = new();
        public Dictionary<string, int>   TopLocalities   { get; set; } = new();
        public int                       PeriodDays      { get; set; }
    }

    public class PwgVerdictResult
    {
        public string Verdict    { get; set; } = "REDO";
        public string Reason     { get; set; } = "";
        public float  Confidence { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  DTOs previously housed in MLServiceClient.cs (deleted in Phase 8).
    //  These shapes are part of the wire contract with the Angular frontend
    //  (see fmc-interfaces/ml.interface.ts) — never rename without a paired
    //  Angular update.
    // ─────────────────────────────────────────────────────────────────────────

    public class ChatMessage
    {
        public string Role    { get; set; } = "";
        public string Content { get; set; } = "";
    }

    public class CategorySuggestion
    {
        public int    CategoryId   { get; set; }
        public string CategoryName { get; set; } = "";
        public float  Confidence   { get; set; }
    }

    public class DuplicateCandidate
    {
        public int   ComplaintId { get; set; }
        public float Similarity  { get; set; }
        public bool  IsDuplicate { get; set; }
    }

    public class DuplicateCheckResult
    {
        public int                       ComplaintId     { get; set; }
        public List<DuplicateCandidate>  Candidates      { get; set; } = new();
        public bool                      EmbeddingStored { get; set; }
    }

    public class ToxicityResult
    {
        public bool   IsToxic    { get; set; }
        public string Reason     { get; set; } = "";
        public float  Confidence { get; set; }
    }
}

// Phase 8: MLServiceClient.cs has been deleted. Its DTOs (ChatMessage,
// CategorySuggestion, DuplicateCandidate, DuplicateCheckResult, ToxicityResult)
// are now defined alongside AiService above so the controller wire shapes
// stay identical.

