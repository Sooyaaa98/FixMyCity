using FixMyCity.DAL.Models;
using FixMyCity.DAL.Repositories.Implementations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FixMyCity.API.Controllers
{
    /// <summary>
    /// Phase 8 (§17) — public transparency portal.
    ///
    /// Anonymous, read-only surface. Lets citizens, journalists and oversight
    /// bodies browse complaints (with truncated descriptions to limit PII
    /// exposure) without creating an account.
    ///
    /// Hardening notes:
    ///   • Descriptions are LEFT()-truncated to 200 chars at the DB tier inside
    ///     usp_GetPublicFeed — long-form details with PII never leave SQL.
    ///   • Citizen names / phone numbers / addresses are NOT projected here;
    ///     usp_GetPublicFeed deliberately omits them.
    ///   • Page-size is clamped to 100 in the SP — a hostile client cannot
    ///     ask for 1 000 000 rows.
    ///   • [AllowAnonymous] overrides the default `Authorize` middleware
    ///     fall-through in Program.cs.
    /// </summary>
    [Route("api/[controller]/[action]")]
    [AllowAnonymous]
    [ApiController]
    public class PublicController : Controller
    {
        private readonly FixMyCityDbContext _context;

        public PublicController(FixMyCityDbContext context)
        {
            _context = context;
        }

        // ── GET api/Public/GetFeed ───────────────────────────────────────────
        // Optional query filters: localityId, categoryId, status, keyword.
        [HttpGet]
        public JsonResult GetFeed(int? localityId, short? categoryId,
                                  string? status, string? keyword,
                                  int pageNum = 1, int pageSize = 20)
        {
            try
            {
                var repo = new FeatureRepository(_context);
                var rows = repo.GetPublicFeed(localityId, categoryId,
                                              string.IsNullOrWhiteSpace(status)  ? null : status,
                                              string.IsNullOrWhiteSpace(keyword) ? null : keyword,
                                              pageNum, pageSize);
                return Json(rows);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── GET api/Public/GetCategories ─────────────────────────────────────
        // Used to populate the transparency portal's filter dropdown.
        [HttpGet]
        public JsonResult GetCategories()
        {
            try
            {
                var repo = new ComplaintRepository(_context);
                return Json(repo.GetAllCategories());
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── GET api/Public/GetLocalities ─────────────────────────────────────
        [HttpGet]
        public JsonResult GetLocalities()
        {
            try
            {
                return Json(_context.Localities
                                    .Where(l => l.IsActive)
                                    .OrderBy(l => l.LocalityName)
                                    .Select(l => new
                                    {
                                        l.LocalityId,
                                        l.LocalityName,
                                        l.City,
                                    })
                                    .ToList());
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }
    }
}
