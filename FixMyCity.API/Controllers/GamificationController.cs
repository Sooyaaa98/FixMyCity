using Microsoft.AspNetCore.Authorization;
using FixMyCity.API.Models;
using FixMyCity.DAL.Models;
using FixMyCity.DAL.Repositories.Implementations;
using Microsoft.AspNetCore.Mvc;

namespace FixMyCity.API.Controllers
{
    [Route("api/[controller]/[action]")]
    [Authorize]
    [ApiController]
    public class GamificationController : Controller
    {
        private readonly FixMyCityDbContext _context;

        public GamificationController(FixMyCityDbContext context)
        {
            _context = context;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Notifications
        // ═══════════════════════════════════════════════════════════════════

        // ── GET api/Gamification/GetUnreadNotifications ───────────────────────

        [HttpGet]
        public JsonResult GetUnreadNotifications(int userId)
        {
            try
            {
                var repo          = new GamificationRepository(_context);
                var notifications = repo.GetUnreadNotifications(userId);
                return Json(notifications);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── GET api/Gamification/GetAllNotifications ──────────────────────────

        [HttpGet]
        public JsonResult GetAllNotifications(int userId)
        {
            try
            {
                var repo          = new GamificationRepository(_context);
                var notifications = repo.GetAllNotifications(userId);
                return Json(notifications);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── PUT api/Gamification/MarkOneRead ──────────────────────────────────

        [HttpPut]
        public JsonResult MarkOneRead(int notificationId)
        {
            try
            {
                var repo   = new GamificationRepository(_context);
                bool result = repo.MarkOneRead(notificationId);
                return Json(new { success = result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── PUT api/Gamification/MarkAllRead ──────────────────────────────────

        [HttpPut]
        public JsonResult MarkAllRead(int userId)
        {
            try
            {
                var repo   = new GamificationRepository(_context);
                bool result = repo.MarkAllRead(userId);
                return Json(new { success = result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── PUT api/Gamification/ArchiveNotification ──────────────────────────

        [HttpPut]
        public JsonResult ArchiveNotification(ArchiveNotificationRequest request)
        {
            try
            {
                var repo   = new GamificationRepository(_context);
                bool result = repo.ArchiveNotification(request.UserId, request.NotificationId);
                return Json(new { success = result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Archive failed.", error = ex.Message });
            }
        }

        // ── POST api/Gamification/SendNotification ────────────────────────────

        [HttpPost]
        public JsonResult SendNotification(SendNotificationRequest request)
        {
            try
            {
                var repo           = new GamificationRepository(_context);
                int notificationId = repo.SendNotification(
                                         request.UserId, request.ComplaintId,
                                         request.Message, request.NotificationType,
                                         request.Channel ?? "InApp");
                return Json(new { success = notificationId > 0, notificationId });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, notificationId = 0, error = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Notification Preferences
        // ═══════════════════════════════════════════════════════════════════

        // ── GET api/Gamification/GetNotificationPreferences ───────────────────

        [HttpGet]
        public JsonResult GetNotificationPreferences(int userId)
        {
            try
            {
                var repo  = new GamificationRepository(_context);
                var prefs = repo.GetNotificationPreferences(userId);

                if (prefs == null)
                    return Json(new { success = false, message = "Preferences not found." });

                return Json(prefs);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── PUT api/Gamification/UpdateNotificationPreferences ────────────────

        [HttpPut]
        public JsonResult UpdateNotificationPreferences(UpdateNotificationPrefsRequest request)
        {
            try
            {
                var repo   = new GamificationRepository(_context);
                bool result = repo.UpdateNotificationPreferences(
                                  request.UserId, request.InAppEnabled,
                                  request.PushEnabled, request.EmailDigestEnabled,
                                  request.DigestFrequencyDays);
                return Json(new { success = result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Preferences update failed.", error = ex.Message });
            }
        }

        // ── POST api/Gamification/GenerateWeeklyDigest ────────────────────────

        [HttpPost]
        public JsonResult GenerateWeeklyDigest()
        {
            try
            {
                var repo   = new GamificationRepository(_context);
                bool result = repo.GenerateWeeklyDigest();
                return Json(new { success = result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Weekly digest generation failed.", error = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Points
        // ═══════════════════════════════════════════════════════════════════

        // ── GET api/Gamification/GetUserPoints ────────────────────────────────

        [HttpGet]
        public JsonResult GetUserPoints(int userId)
        {
            try
            {
                var repo       = new GamificationRepository(_context);
                var userPoints = repo.GetUserPoints(userId);
                return Json(userPoints);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── POST api/Gamification/AwardPoints ────────────────────────────────

        [HttpPost]
        public JsonResult AwardPoints(AwardPointsRequest request)
        {
            try
            {
                var repo        = new GamificationRepository(_context);
                int updatedTotal = repo.AwardPoints(
                                       request.UserId, request.Points,
                                       request.Reason ?? "ManualAward",
                                       request.RefComplaintId, request.RefMilestoneId);
                return Json(new { success = updatedTotal > 0, updatedTotal });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── GET api/Gamification/GetPointsLedger ──────────────────────────────

        [HttpGet]
        public JsonResult GetPointsLedger(int userId)
        {
            try
            {
                var repo   = new GamificationRepository(_context);
                var ledger = repo.GetPointsLedger(userId);
                return Json(ledger);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Milestones
        // ═══════════════════════════════════════════════════════════════════

        // ── GET api/Gamification/GetActiveMilestones ──────────────────────────

        [HttpGet]
        public JsonResult GetActiveMilestones()
        {
            try
            {
                var repo       = new GamificationRepository(_context);
                var milestones = repo.GetActiveMilestones();
                return Json(milestones);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Certificates
        // ═══════════════════════════════════════════════════════════════════

        // ── POST api/Gamification/IssueCertificate ────────────────────────────

        [HttpPost]
        public JsonResult IssueCertificate(IssueCertificateRequest request)
        {
            try
            {
                var repo   = new GamificationRepository(_context);
                var result = repo.IssueCertificate(
                                  request.UserId, request.Milestone,
                                  request.FilePath, request.MilestoneId);

                if (result.NewCertId == -1)
                    return Json(new { success = false, message = "Certificate for this milestone already issued." });

                return Json(new
                {
                    success          = result.NewCertId > 0,
                    certId           = result.NewCertId,
                    verificationCode = result.VerificationCode
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, certId = 0, error = ex.Message });
            }
        }

        // ── GET api/Gamification/GetCertificates ──────────────────────────────

        [HttpGet]
        public JsonResult GetCertificates(int userId)
        {
            try
            {
                var repo         = new GamificationRepository(_context);
                var certificates = repo.GetCertificatesByUser(userId);
                return Json(certificates);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Scoreboard
        // ═══════════════════════════════════════════════════════════════════

        // ── GET api/Gamification/GetScoreboard ────────────────────────────────

        [HttpGet]
        public JsonResult GetScoreboard(int? localityId)
        {
            try
            {
                var repo       = new GamificationRepository(_context);
                var scoreboard = repo.GetScoreboard(localityId);
                return Json(scoreboard);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── POST api/Gamification/RefreshScoreboard ───────────────────────────

        [HttpPost]
        public JsonResult RefreshScoreboard()
        {
            try
            {
                var repo   = new GamificationRepository(_context);
                bool result = repo.RefreshScoreboard();
                return Json(new { success = result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Scoreboard refresh failed.", error = ex.Message });
            }
        }
    }
}
