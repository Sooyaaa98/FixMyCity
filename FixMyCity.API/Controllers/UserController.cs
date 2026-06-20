using FixMyCity.DAL.Models;
using FixMyCity.DAL.Repositories.Implementations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FixMyCity.API.Controllers
{
    /// <summary>
    /// Phase 8 (§20) — authenticated user-scoped reads that don't belong on any
    /// existing controller. Currently only the activity feed lives here, but
    /// any future "self-service" endpoint (notification preferences, profile
    /// timeline, etc.) should land here too.
    /// </summary>
    [Route("api/[controller]/[action]")]
    [Authorize]
    [ApiController]
    public class UserController : Controller
    {
        private readonly FixMyCityDbContext _context;
        private readonly ILogger<UserController> _logger;

        public UserController(FixMyCityDbContext context, ILogger<UserController> logger)
        {
            _context = context;
            _logger  = logger;
        }

        // ── §20 GET api/User/GetActivityFeed ─────────────────────────────────
        // The SP returns a unified feed: complaint events + points + certificates
        // + comments. Paginated server-side.
        [HttpGet]
        public JsonResult GetActivityFeed(int userId, int pageSize = 20, int pageNum = 1)
        {
            try
            {
                var repo = new FeatureRepository(_context);
                var rows = repo.GetActivityFeed(userId, pageSize, pageNum);
                return Json(rows);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetActivityFeed failed for user {UserId}", userId);
                return Json(new { success = false, error = ex.Message });
            }
        }
    }
}
