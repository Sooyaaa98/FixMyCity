using Microsoft.AspNetCore.Authorization;
using FixMyCity.API.Models;
using FixMyCity.API.Services;
using FixMyCity.DAL.Models;
using FixMyCity.DAL.Repositories.Implementations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FixMyCity.API.Controllers
{
    [Route("api/[controller]/[action]")]
    [Authorize]
    [ApiController]
    public class ComplaintController : Controller
    {
        private readonly FixMyCityDbContext _context;
        private readonly AiService _aiService;             // Phase 4 — direct Gemini/OpenAI
        private readonly CloudinaryService _cloudinary;    // Phase 2 — image storage
        private readonly IServiceScopeFactory _scopeFactory; // Phase 5.5 — fire-and-forget AI calls
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<ComplaintController> _logger;

        public ComplaintController(
            FixMyCityDbContext context,
            AiService aiService,
            CloudinaryService cloudinary,
            IServiceScopeFactory scopeFactory,
            IConfiguration config,
            IWebHostEnvironment env,
            ILogger<ComplaintController> logger)
        {
            _context      = context;
            _aiService    = aiService;
            _cloudinary   = cloudinary;
            _scopeFactory = scopeFactory;
            _config       = config;
            _env          = env;
            _logger       = logger;
        }

        // ── POST api/Complaint/UploadComplaintImage ──────────────────────────
        // Multipart upload used by the submit-complaint form's photo picker.
        //
        // Phase-2 migration (2026-05-25): branches on _cloudinary.IsConfigured.
        //
        //   • CONFIGURED: stream is pushed to Cloudinary (private folder).
        //     `filePath` returned to Angular is the full HTTPS secure URL.
        //     NOTE: while the legacy Python AI service is still in the loop
        //     (Phases 2-7), /api/ML/AnalyzeImage expects a basename it can
        //     resolve under IMAGE_BASE_PATH on disk. Enabling Cloudinary
        //     therefore implies AnalyzeImage will be served by AiService
        //     (Phase 3 onward). Either land Phases 2-3 together, or keep
        //     Cloudinary creds blank in dev until Phase 3 is merged.
        //
        //   • NOT CONFIGURED (placeholder/empty creds): existing disk path
        //     under Uploads:BasePath. `filePath` returned is a bare basename;
        //     the Python service's `os.path.basename` lookup still works.
        //
        // Response shape is identical in both branches:
        //   { success, filePath, fileName, fileSizeKB }
        // Angular treats `filePath` as opaque — see ml.interface.ts
        // IPhotoUploadResponse — so the URL/basename swap is invisible.

        [HttpPost]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<JsonResult> UploadComplaintImage(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return Json(new { success = false, message = "No file uploaded." });

                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                if (!allowed.Contains(ext))
                    return Json(new { success = false, message = "Only JPG, PNG and WEBP are accepted." });

                int sizeKb = (int)Math.Max(1, file.Length / 1024);

                // ── Branch 1: Cloudinary upload ───────────────────────────────
                if (_cloudinary.IsConfigured)
                {
                    try
                    {
                        await using var stream = file.OpenReadStream();
                        var secureUrl = await _cloudinary.UploadImageAsync(stream, file.FileName);

                        return Json(new
                        {
                            success    = true,
                            filePath   = secureUrl,
                            fileName   = file.FileName,
                            fileSizeKB = sizeKb,
                        });
                    }
                    catch (Exception cex)
                    {
                        // Don't 500 the citizen — fall through to disk so the
                        // submit form still works while ops debugs Cloudinary.
                        _logger.LogError(cex,
                            "UploadComplaintImage: Cloudinary failed, falling back to disk");
                    }
                }

                // ── Branch 2: legacy disk path (also fallback for Cloudinary errors) ──
                var basePath = _config["Uploads:BasePath"] ?? "../FixMyCityUploads";
                var baseDir  = Path.IsPathRooted(basePath)
                    ? basePath
                    : Path.GetFullPath(Path.Combine(_env.ContentRootPath, basePath));
                Directory.CreateDirectory(baseDir);

                var storedName = $"complaint_{Guid.NewGuid():N}{ext}";
                var fullPath   = Path.Combine(baseDir, storedName);

                await using (var fs = new FileStream(fullPath, FileMode.CreateNew))
                    await file.CopyToAsync(fs);

                return Json(new
                {
                    success    = true,
                    filePath   = storedName,           // basename only; the Python service strips dirs anyway
                    fileName   = file.FileName,
                    fileSizeKB = sizeKb,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UploadComplaintImage failed");
                return Json(new { success = false, message = "Upload failed.", error = ex.Message });
            }
        }

        // ── GET api/Complaint/ServeImage ──────────────────────────────────────
        // Phase-2 migration (2026-05-25). Universal indirection layer so
        // Angular templates can bind to one stable URL regardless of where the
        // image actually lives. Two cases:
        //
        //   • path is a Cloudinary URL → 302 to a freshly signed URL valid 1h.
        //     This keeps private resources renderable in the browser without
        //     baking signed URLs into DB rows.
        //   • path is anything else (a legacy disk basename, an absolute http
        //     URL on some future CDN, etc.) → 302 to the value as-is.
        //
        // [AllowAnonymous] mirrors the public-asset nature of complaint photos
        // once they've been signed. If photo access needs to become
        // authenticated later, this is the single place to add the check.

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ServeImage([FromQuery] string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return BadRequest(new { success = false, message = "path is required" });

            try
            {
                // Heuristic: Cloudinary URLs contain "res.cloudinary.com".
                if (path.Contains("res.cloudinary.com", StringComparison.OrdinalIgnoreCase))
                {
                    var signed = _cloudinary.GetSignedUrl(path);
                    return Redirect(signed);
                }

                // Pass-through for legacy disk basenames or external URLs.
                return Redirect(path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ServeImage failed for {path}", path);
                return NotFound(new { success = false, message = "Image not available" });
            }
        }

        // ── POST api/Complaint/SubmitComplaint ────────────────────────────────
        // CHANGE 2–4: toxicity check + async AI pipeline + retry queue

        [HttpPost]
        public async Task<JsonResult> SubmitComplaint(SubmitComplaintRequest request)
        {
            try
            {
                // Phase 4: toxicity now via AiService → OpenAI moderation (free, fail-open).
                // Falls open identically to the legacy Python path when the
                // API key isn't configured or OpenAI is unreachable.
                var toxicity = await _aiService.CheckToxicityAsync(
                    0, $"{request.Title}. {request.Description}");

                if (toxicity.IsToxic)
                    return Json(new { success = false, message = $"Complaint not submitted: {toxicity.Reason}" });

                // Save to DB
                var repo = new ComplaintRepository(_context);
                int complaintId = repo.SubmitComplaint(
                                      request.CitizenUserId, request.CategoryId,
                                      request.Title, request.Description,
                                      request.LocalityId, request.Address,
                                      request.Criticality,
                                      request.Latitude, request.Longitude,
                                      request.FilePath, request.FileName,
                                      request.FileSizeKB);

                if (complaintId <= 0)
                    return Json(new { success = false, complaintId = 0, message = "Complaint could not be saved." });

                // Phase 5.5: AI enrichment is fire-and-forget through AiService.
                //
                // Why scopeFactory.CreateScope inside Task.Run?
                //   AiService is scoped — it holds the request's DbContext.
                //   The Task.Run lambda outlives the HTTP request scope, so by
                //   the time it actually runs, the original scope is disposed.
                //   We create a fresh scope here so AiService and its DbContext
                //   live for exactly the lifetime of this background work.
                //   See risk_analysis.md R6 for the full incident pattern.
                //
                // The deterministic scorer (ScoreComplaintAsync) doesn't need
                // Gemini and so doesn't degrade if the key is missing. The
                // tag / dedup paths fail open when Gemini is unreachable.
                int catId   = request.CategoryId;
                string crit = request.Criticality;
                int locId   = request.LocalityId;
                string desc = request.Description;
                string title = request.Title;
                int descLen = desc?.Length ?? 0;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(300);   // let the DB transaction commit first
                        using var bgScope = _scopeFactory.CreateScope();
                        var ai = bgScope.ServiceProvider.GetRequiredService<AiService>();

                        await ai.ScoreComplaintAsync(
                            complaintId, catId, crit, locId, deptId: null,
                            descriptionLen: descLen);

                        await ai.CheckDuplicatesAsync(
                            complaintId, title, desc ?? "", catId, locId, excludeId: complaintId);

                        await ai.AutoTagAsync(complaintId, title, desc ?? "");
                    }
                    catch (Exception bgEx)
                    {
                        // Last-ditch logger; AiService methods are themselves fail-open.
                        // If we get here, the DI scope itself failed to build.
                        _logger.LogError(bgEx,
                            "Background AI enrichment crashed for complaint {ComplaintId}", complaintId);

                        // Queue for AIPendingQueueProcessor retry. New scope
                        // because _context is long-disposed by now.
                        try
                        {
                            using var retryScope = _scopeFactory.CreateScope();
                            var db = retryScope.ServiceProvider.GetRequiredService<FixMyCityDbContext>();
                            await db.Database.ExecuteSqlRawAsync(
                                "IF NOT EXISTS (SELECT 1 FROM dbo.AIPendingScoreQueue WHERE ComplaintId = @p0) " +
                                "INSERT INTO dbo.AIPendingScoreQueue (ComplaintId) VALUES (@p0)",
                                complaintId);
                        }
                        catch { /* best-effort */ }
                    }
                });

                return Json(new { success = true, complaintId });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, complaintId = 0, error = ex.Message });
            }
        }
        // ── All other methods below are UNCHANGED from previous ComplaintController ──

        [HttpGet]
        public JsonResult GetComplaintById(int complaintId)
        {
            try
            {
                var repo = new ComplaintRepository(_context);
                var complaint = repo.GetComplaintById(complaintId);
                if (complaint == null)
                    return Json(new { success = false, message = "Complaint not found." });
                return Json(complaint);
            }
            catch (Exception ex) { return Json(new { success = false, error = ex.Message }); }
        }

        [HttpGet]
        public JsonResult GetComplaintsByCitizen(int citizenUserId)
        {
            try
            {
                var repo = new ComplaintRepository(_context);
                var complaints = repo.GetComplaintsByCitizen(citizenUserId);
                return Json(complaints);
            }
            catch (Exception ex) { return Json(new { success = false, error = ex.Message }); }
        }

        [HttpGet]
        public JsonResult FilterComplaints(int citizenUserId, string? status, int? localityId)
        {
            try
            {
                var repo = new ComplaintRepository(_context);
                return Json(repo.FilterComplaintsByCitizen(citizenUserId, status, localityId));
            }
            catch (Exception ex) { return Json(new { success = false, error = ex.Message }); }
        }

        [HttpGet]
        public JsonResult GetComplaintsByDept(int deptId, string? status, int? localityId, string? criticality)
        {
            try
            {
                var repo = new ComplaintRepository(_context);
                return Json(repo.GetComplaintsByDept(deptId, status, localityId, criticality));
            }
            catch (Exception ex) { return Json(new { success = false, error = ex.Message }); }
        }

        [HttpGet]
        public JsonResult GetLocalityFeed(int localityId)
        {
            try
            {
                var repo = new ComplaintRepository(_context);
                return Json(repo.GetLocalityFeed(localityId));
            }
            catch (Exception ex) { return Json(new { success = false, error = ex.Message }); }
        }

        [HttpGet]
        public JsonResult GetMapComplaints(int? localityId)
        {
            try
            {
                var repo = new ComplaintRepository(_context);
                return Json(repo.GetMapComplaints(localityId));
            }
            catch (Exception ex) { return Json(new { success = false, error = ex.Message }); }
        }

        [HttpGet]
        public JsonResult Search(string? keyword, short? categoryId, int? localityId)
        {
            try
            {
                var repo = new ComplaintRepository(_context);
                return Json(repo.SearchComplaints(keyword, categoryId, localityId));
            }
            catch (Exception ex) { return Json(new { success = false, error = ex.Message }); }
        }

        [HttpGet]
        public JsonResult GetTimeline(int complaintId)
        {
            try
            {
                var repo = new ComplaintRepository(_context);
                return Json(repo.GetTimeline(complaintId));
            }
            catch (Exception ex) { return Json(new { success = false, error = ex.Message }); }
        }

        [HttpPut]
        public JsonResult UpdateStatus(UpdateStatusRequest request)
        {
            try
            {
                var repo = new ComplaintRepository(_context);
                bool result = repo.UpdateComplaintStatus(
                    request.ComplaintId, request.SolverUserId, request.NewStatus,
                    request.Remark, request.ResolutionFilePath,
                    request.ResolutionFileName, request.ResolutionFileSizeKB);
                return Json(new { success = result });
            }
            catch (Exception ex) { return Json(new { success = false, error = ex.Message }); }
        }

        [HttpPut]
        public JsonResult SetEstimatedDate(UpdateEstDateRequest request)
        {
            try
            {
                var repo = new ComplaintRepository(_context);
                bool result = repo.SetEstimatedResolutionDate(
                    request.ComplaintId, request.SolverUserId, request.EstDate);
                return Json(new { success = result });
            }
            catch (Exception ex) { return Json(new { success = false, error = ex.Message }); }
        }

        [HttpPost]
        public JsonResult RateComplaint(RateComplaintRequest request)
        {
            try
            {
                var repo = new ComplaintRepository(_context);
                var existing = repo.GetRating(request.ComplaintId, request.CitizenUserId);
                if (existing != null)
                    return Json(new { success = false, message = "You have already rated this complaint." });
                bool result = repo.RateComplaint(
                    request.ComplaintId, request.CitizenUserId, request.Stars, request.Comment);
                return Json(new { success = result });
            }
            catch (Exception ex) { return Json(new { success = false, error = ex.Message }); }
        }

        [HttpPut]
        public JsonResult ReopenComplaint(ReopenComplaintRequest request)
        {
            try
            {
                var repo = new ComplaintRepository(_context);
                var rating = repo.GetRating(request.ComplaintId, request.CitizenUserId);
                if (rating == null || rating.Stars >= 3)
                    return Json(new
                    {
                        success = false,
                        message = "Re-open only available after rating below 3 stars."
                    });
                bool result = repo.ReopenComplaint(
                    request.ComplaintId, request.CitizenUserId, request.Reason);
                return Json(new { success = result });
            }
            catch (Exception ex) { return Json(new { success = false, error = ex.Message }); }
        }

        [HttpGet]
        public JsonResult GetAttachments(int complaintId, string? attachmentType)
        {
            try
            {
                var repo = new ComplaintRepository(_context);
                return Json(repo.GetAttachments(complaintId, attachmentType));
            }
            catch (Exception ex) { return Json(new { success = false, error = ex.Message }); }
        }

        [HttpPost]
        public JsonResult AddAttachment(AddAttachmentRequest request)
        {
            try
            {
                var repo = new ComplaintRepository(_context);
                int attachmentId = repo.AddAttachment(
                    request.ComplaintId, request.UploadedByUserId,
                    request.AttachmentType, request.FilePath,
                    request.FileName, request.FileSizeKB, request.TimelineId);
                return Json(new { success = attachmentId > 0, attachmentId });
            }
            catch (Exception ex) { return Json(new { success = false, error = ex.Message }); }
        }

        [HttpGet]
        public JsonResult GetCandidateDuplicates(int localityId, short categoryId, int excludeComplaintId)
        {
            try
            {
                var repo = new ComplaintRepository(_context);
                return Json(repo.GetCandidateDuplicates(localityId, categoryId, excludeComplaintId));
            }
            catch (Exception ex) { return Json(new { success = false, error = ex.Message }); }
        }

        [HttpPut]
        public JsonResult LinkDuplicate(LinkDuplicateRequest request)
        {
            try
            {
                var repo = new ComplaintRepository(_context);
                bool result = repo.LinkDuplicateComplaint(
                    request.OriginalComplaintId, request.LinkedComplaintId, request.LinkedByUserId);
                return Json(new { success = result });
            }
            catch (Exception ex) { return Json(new { success = false, error = ex.Message }); }
        }

        [HttpGet]
        public JsonResult GetAllCategories()
        {
            try
            {
                var repo = new ComplaintRepository(_context);
                return Json(repo.GetAllCategories());
            }
            catch (Exception ex) { return Json(new { success = false, error = ex.Message }); }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Phase 8 — feature-suggestion endpoints (06_FeatureSuggestions.sql)
        // ═══════════════════════════════════════════════════════════════════

        // ── §1 Upvote ────────────────────────────────────────────────────────

        [HttpPost]
        public JsonResult ToggleUpvote(ToggleUpvoteRequest request)
        {
            try
            {
                var repo   = new FeatureRepository(_context);
                var result = repo.ToggleUpvote(request.ComplaintId, request.CitizenUserId);
                return Json(new
                {
                    success    = true,
                    newCount   = result.NewCount,
                    hasUpvoted = result.HasUpvoted,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ToggleUpvote endpoint failed");
                return Json(new { success = false, message = "Could not register your vote.", error = ex.Message });
            }
        }

        [HttpGet]
        public JsonResult GetUpvoteState(int complaintId, int citizenUserId)
        {
            try
            {
                var repo = new FeatureRepository(_context);
                return Json(new
                {
                    success    = true,
                    count      = repo.GetUpvoteCount(complaintId),
                    hasUpvoted = repo.HasUpvoted(complaintId, citizenUserId),
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetUpvoteState endpoint failed");
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── §7 Comments ──────────────────────────────────────────────────────

        [HttpPost]
        public JsonResult AddComment(AddCommentRequest request)
        {
            try
            {
                var repo = new FeatureRepository(_context);
                int id   = repo.AddComment(request.ComplaintId, request.UserId, request.CommentText);
                return Json(new { success = id > 0, commentId = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AddComment endpoint failed");
                return Json(new { success = false, message = "Could not post your comment.", error = ex.Message });
            }
        }

        [HttpGet]
        public JsonResult GetComments(int complaintId)
        {
            try
            {
                var repo = new FeatureRepository(_context);
                // Flatten so the role-name is visible without traversing nav props on the client.
                var rows = repo.GetComments(complaintId).Select(c => new
                {
                    c.CommentId,
                    c.ComplaintId,
                    c.UserId,
                    c.CommentText,
                    c.IsOfficialReply,
                    c.CreatedAt,
                    authorName = c.User?.FullName,
                    authorRole = c.User?.Role?.RoleName,
                });
                return Json(rows);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetComments endpoint failed");
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpDelete]
        public JsonResult DeleteComment([FromBody] DeleteCommentRequest request)
        {
            try
            {
                var roleClaim = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
                bool isAdmin  = roleClaim == "SuperAdmin";
                var repo      = new FeatureRepository(_context);
                bool ok       = repo.SoftDeleteComment(request.CommentId, request.ActingUserId, isAdmin);
                return Json(new { success = ok });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeleteComment endpoint failed");
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── §6 Appeals (citizen-facing) ──────────────────────────────────────

        [HttpPost]
        public JsonResult SubmitAppeal(SubmitAppealRequest request)
        {
            try
            {
                var repo = new FeatureRepository(_context);
                int id   = repo.SubmitAppeal(request.ComplaintId, request.CitizenUserId, request.Reason);
                return Json(new { success = id > 0, appealId = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SubmitAppeal endpoint failed");
                return Json(new { success = false, message = "Could not file appeal.", error = ex.Message });
            }
        }

        [HttpGet]
        public JsonResult GetMyAppeals(int citizenUserId)
        {
            try
            {
                var repo = new FeatureRepository(_context);
                var rows = repo.GetAppealsByCitizen(citizenUserId).Select(a => new
                {
                    a.AppealId,
                    a.ComplaintId,
                    complaintTitle = a.Complaint?.Title,
                    a.Reason,
                    a.Status,
                    a.Decision,
                    a.AdminNote,
                    a.CreatedAt,
                    a.ResolvedAt,
                });
                return Json(rows);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetMyAppeals endpoint failed");
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── §15 Internal notes (Solver / SuperAdmin only) ────────────────────

        [HttpPost]
        [Authorize(Roles = "Solver,SuperAdmin")]
        public JsonResult AddInternalNote(AddInternalNoteRequest request)
        {
            try
            {
                var repo = new FeatureRepository(_context);
                int id   = repo.AddInternalNote(request.ComplaintId, request.CreatedByUserId, request.NoteText);
                return Json(new { success = id > 0, noteId = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AddInternalNote endpoint failed");
                return Json(new { success = false, message = "Could not save the note.", error = ex.Message });
            }
        }

        [HttpGet]
        [Authorize(Roles = "Solver,SuperAdmin")]
        public JsonResult GetInternalNotes(int complaintId)
        {
            try
            {
                var repo = new FeatureRepository(_context);
                var rows = repo.GetInternalNotes(complaintId).Select(n => new
                {
                    n.NoteId,
                    n.ComplaintId,
                    n.NoteText,
                    n.CreatedAt,
                    authorName = n.Author?.FullName,
                    authorRole = n.Author?.Role?.RoleName,
                });
                return Json(rows);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetInternalNotes endpoint failed");
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── §5 Near-me ───────────────────────────────────────────────────────

        [HttpGet]
        public JsonResult GetNearby(decimal lat, decimal lng,
                                    decimal? radiusKm = null, int? pageSize = null)
        {
            try
            {
                var repo = new FeatureRepository(_context);
                var rows = repo.GetNearbyComplaints(lat, lng,
                                radiusKm  ?? 2.0m,
                                pageSize  ?? 50);
                return Json(rows);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetNearby endpoint failed");
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── Private: enqueue failed AI score for retry ────────────────────────

        private void _EnqueueForRetry(int complaintId)
        {
            try
            {
                _context.Database.ExecuteSqlRaw(
                    /* lang=tsql */ """
                    IF NOT EXISTS (SELECT 1 FROM dbo.AIPendingScoreQueue WHERE ComplaintId = @p0)
                        INSERT INTO dbo.AIPendingScoreQueue (ComplaintId) VALUES (@p0)
                    """,
                    complaintId);
            }
            catch (Exception ex)
            {
                // Non-critical: AI scoring will simply be missed for this complaint
                // until next time it is touched. Log so a backlog is visible in ops.
                _logger.LogWarning(ex,
                    "_EnqueueForRetry failed for complaint {ComplaintId} — AI score backlog row not inserted",
                    complaintId);
            }
        }
    }
}
