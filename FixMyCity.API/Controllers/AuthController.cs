// FixMyCity.API/Controllers/AuthController.cs
// Full replacement — adds JWT access + refresh token flow on top of existing auth logic.
// Endpoints:
//   POST /api/Auth/Login          → returns accessToken + refreshToken + user profile
//   POST /api/Auth/RefreshToken   → rotates refresh token, issues new access token
//   POST /api/Auth/Logout         → revokes refresh token (client must discard access token)
//   POST /api/Auth/LogoutAll      → revokes ALL user sessions (e.g. from another device)
//   All other endpoints unchanged.

using FixMyCity.API.Models;
using FixMyCity.API.Services;
using FixMyCity.DAL.Models;
using FixMyCity.DAL.Repositories.Implementations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace FixMyCity.API.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class AuthController : Controller
    {
        private readonly FixMyCityDbContext _context;
        private readonly IJwtService        _jwt;

        public AuthController(FixMyCityDbContext context, IJwtService jwt)
        {
            _context = context;
            _jwt     = jwt;
        }

        // ── POST api/Auth/Login ────────────────────────────────────────────────
        // Rate-limited to 10 requests/minute per IP (see Program.cs)

        [HttpPost]
        [EnableRateLimiting("login")]
        public async Task<JsonResult> Login(LoginRequest request)
        {
            try
            {
                var repo = new AuthRepository(_context);
                var user = repo.ValidateLogin(request.Email, HashPassword(request.Password));

                if (user == null)
                {
                    repo.RecordFailedLogin(request.Email);
                    return Json(new { success = false, message = "Invalid credentials or account locked." });
                }

                repo.ResetLoginAttempts(user.UserId);

                int?   deptId    = null;
                int?   orgId     = null;
                string roleName  = user.Role?.RoleName ?? "Citizen";

                // Resolve deptId for Solvers (needed for RLS session context)
                // and orgId for PWGs (needed by the participation-request form etc.)
                if (roleName == "Solver")
                    deptId = repo.GetDeptIdForUser(user.UserId);
                else if (roleName == "PWG")
                    orgId  = repo.GetOrgIdForUser(user.UserId);

                var accessToken  = _jwt.GenerateAccessToken(
                    user.UserId, user.Email!, roleName,
                    user.LocalityId ?? 0, deptId);

                var rawRefresh   = _jwt.GenerateRefreshToken();
                await _jwt.SaveRefreshTokenAsync(user.UserId, rawRefresh);

                return Json(new
                {
                    success      = true,
                    accessToken,
                    refreshToken = rawRefresh,
                    expiresIn    = 15 * 60, // seconds
                    user = new
                    {
                        userId     = user.UserId,
                        fullName   = user.FullName,
                        email      = user.Email,
                        roleId     = user.RoleId,
                        roleName,
                        localityId = user.LocalityId ?? 0,
                        deptId,
                        orgId,
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Login failed.", error = ex.Message });
            }
        }

        // ── POST api/Auth/RefreshToken ─────────────────────────────────────────
        // Rotate: old refresh token is revoked, new pair issued.

        [HttpPost]
        public async Task<JsonResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
                return Json(new { success = false, message = "Refresh token required." });

            var userId = await _jwt.ValidateRefreshTokenAsync(request.RefreshToken);

            if (userId is null)
                return Json(new { success = false, message = "Refresh token is invalid or expired." });

            // Revoke the used token (rotation — prevents reuse)
            await _jwt.RevokeRefreshTokenAsync(request.RefreshToken);

            var repo    = new AuthRepository(_context);
            var user    = repo.GetUserById(userId.Value);

            if (user is null || !user.IsActive)
                return Json(new { success = false, message = "Account not found or inactive." });

            string roleName = user.Role?.RoleName ?? "Citizen";
            int?   deptId   = roleName == "Solver" ? repo.GetDeptIdForUser(user.UserId) : null;

            var newAccess  = _jwt.GenerateAccessToken(
                user.UserId, user.Email!, roleName, user.LocalityId ?? 0, deptId);
            var newRefresh = _jwt.GenerateRefreshToken();
            await _jwt.SaveRefreshTokenAsync(user.UserId, newRefresh);

            return Json(new
            {
                success      = true,
                accessToken  = newAccess,
                refreshToken = newRefresh,
                expiresIn    = 15 * 60,
            });
        }

        // ── POST api/Auth/Logout ──────────────────────────────────────────────

        [HttpPost]
        [Authorize]
        public async Task<JsonResult> Logout([FromBody] RefreshTokenRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.RefreshToken))
                await _jwt.RevokeRefreshTokenAsync(request.RefreshToken);

            return Json(new { success = true, message = "Logged out." });
        }

        // ── POST api/Auth/LogoutAll ───────────────────────────────────────────
        // Revokes ALL sessions for this user (e.g. "sign out from all devices")

        [HttpPost]
        [Authorize]
        public async Task<JsonResult> LogoutAll()
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Json(new { success = false, message = "Not authenticated." });

            await _jwt.RevokeAllUserTokensAsync(userId);
            return Json(new { success = true, message = "All sessions revoked." });
        }

        // ── POST api/Auth/SSOLogin ────────────────────────────────────────────

        [HttpPost]
        public async Task<JsonResult> SSOLogin(SSOLoginRequest request)
        {
            try
            {
                var repo = new AuthRepository(_context);
                var (userId, roleId) = repo.SSOLoginOrCreate(
                    request.SSOProvider, request.SSOExternalId,
                    request.Email, request.FullName);

                if (userId == 0)
                    return Json(new { success = false, message = "SSO login failed." });

                var user    = repo.GetUserById(userId);
                string role = user?.Role?.RoleName ?? "Citizen";

                var accessToken = _jwt.GenerateAccessToken(userId, request.Email, role, 0, null);
                var rawRefresh  = _jwt.GenerateRefreshToken();
                await _jwt.SaveRefreshTokenAsync(userId, rawRefresh);

                return Json(new { success = true, userId, roleId, accessToken, refreshToken = rawRefresh });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "SSO login failed.", error = ex.Message });
            }
        }

        // ── POST api/Auth/RegisterCitizen ─────────────────────────────────────

        [HttpPost]
        public JsonResult RegisterCitizen(RegisterCitizenRequest request)
        {
            try
            {
                var repo = new AuthRepository(_context);
                if (repo.EmailExists(request.Email))
                    return Json(new { success = false, message = "Email already registered." });

                int userId = repo.RegisterCitizen(
                    request.FullName, request.Email, HashPassword(request.Password),
                    request.Phone, request.Address, request.LocalityId, request.AadhaarNo);

                // The repository swallows SqlExceptions and returns 0, so a check-constraint
                // failure (Aadhaar != 12 digits, Phone < 10 digits, unique-email race, etc.)
                // surfaces here. Without this guard the API would report success=true with
                // userId=0 and the UI would say "Registration successful" while the DB has no row.
                if (userId <= 0)
                    return Json(new { success = false, message = "Registration failed. Please verify your inputs — Aadhaar must be exactly 12 digits and Phone must be at least 10 digits." });

                return Json(new { success = true, userId });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Registration failed.", error = ex.Message });
            }
        }

        // ── POST api/Auth/RegisterOrganisation ──────────────────────────────

        [HttpPost]
        public JsonResult RegisterOrganisation(RegisterOrgRequest request)
        {
            try
            {
                var repo = new AuthRepository(_context);
                if (repo.EmailExists(request.Email))
                    return Json(new { success = false, message = "Email already registered." });

                int userId = repo.RegisterOrganisation(
                    request.FullName, request.Email, HashPassword(request.Password),
                    request.Phone, request.Address, request.LocalityId,
                    request.OrgName, request.OrgType, request.RegistrationNo,
                    request.ContactEmail, request.ContactPhone);

                if (userId <= 0)
                    return Json(new { success = false, message = "Registration failed. Common causes: phone < 10 digits, duplicate registration number, or unsupported organisation type." });

                return Json(new { success = true, userId });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Registration failed.", error = ex.Message });
            }
        }

        // ── POST api/Auth/RegisterDepartment ─────────────────────────────────

        [HttpPost]
        public JsonResult RegisterDepartment(RegisterDeptRequest request)
        {
            try
            {
                var repo = new AuthRepository(_context);
                if (repo.EmailExists(request.Email))
                    return Json(new { success = false, message = "Email already registered." });

                int userId = repo.RegisterDepartment(
                    request.FullName, request.Email, HashPassword(request.Password),
                    request.Phone, request.Address, request.LocalityId,
                    request.DeptName, request.Ministry, request.CategoryId,
                    request.ContactEmail, request.ContactPhone);

                if (userId <= 0)
                    return Json(new { success = false, message = "Registration failed. Please verify your inputs (phone must be at least 10 digits)." });

                return Json(new { success = true, userId });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Registration failed.", error = ex.Message });
            }
        }

        // ── GET api/Auth/GetUserById ──────────────────────────────────────────

        [HttpGet]
        [Authorize]
        public JsonResult GetUserById(int userId)
        {
            try
            {
                var repo = new AuthRepository(_context);
                var user = repo.GetUserById(userId);
                if (user == null)
                    return Json(new { success = false, message = "User not found." });

                // Project to a flat shape — the User entity exposes Role/Locality/UserPoint
                // as navigation properties, but the Angular IUserProfile expects flat
                // roleName / localityName / points fields. Without this projection the
                // profile page shows blank "Role" and "Locality" cells.
                return Json(new
                {
                    userId       = user.UserId,
                    fullName     = user.FullName,
                    email        = user.Email,
                    phone        = user.Phone,
                    address      = user.Address,
                    localityId   = user.LocalityId ?? 0,
                    localityName = user.Locality?.LocalityName,
                    isActive     = user.IsActive,
                    isApproved   = user.IsApproved,
                    isBanned     = user.IsBanned,
                    roleId       = user.RoleId,
                    roleName     = user.Role?.RoleName ?? "",
                    orgId        = user.Organisation?.OrgId,
                    deptId       = user.Department?.DeptId,
                    points       = user.UserPoint?.Points ?? 0,
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── GET api/Auth/GetAllCategories / GetAllLocalities ──────────────────
        // Public — called from registration form before user is logged in.

        [HttpGet]
        public JsonResult GetAllCategories()
        {
            try
            {
                var repo = new AuthRepository(_context);
                return Json(repo.GetAllCategories());
            }
            catch (Exception ex) { return Json(new { success = false, error = ex.Message }); }
        }

        [HttpGet]
        public JsonResult GetAllLocalities()
        {
            try
            {
                var repo = new AuthRepository(_context);
                return Json(repo.GetAllLocalities());
            }
            catch (Exception ex) { return Json(new { success = false, error = ex.Message }); }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Phase 8 (§18) — Forgot / Reset password endpoints.
        // ═══════════════════════════════════════════════════════════════════
        //
        // Design:
        //   - The raw token NEVER touches the database — we only persist a
        //     SHA-256 hash (HASHBYTES('SHA2_256', …) on the SQL side; the
        //     same fixed-time hex hash on the C# side).
        //   - ForgotPassword ALWAYS returns success: true (regardless of
        //     whether the email exists) to avoid leaking which accounts
        //     belong to citizens — classic anti-enumeration.
        //   - In dev we surface `devToken` in the response so QA can test the
        //     flow without a working SMTP service. The `Email:Enabled = false`
        //     toggle in appsettings.Development.json controls this.
        //
        // Future: replace `_TrySendResetEmail` with a real ISmtpService.

        // ── POST api/Auth/ForgotPassword ───────────────────────────────────
        // Public — no [Authorize].
        [HttpPost]
        public async Task<JsonResult> ForgotPassword([FromBody] RequestPasswordResetRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request?.Email))
                    return Json(new { success = true });   // anti-enumeration: respond OK either way

                // Generate a 32-byte cryptographically-random token, encode as hex.
                var tokenBytes = RandomNumberGenerator.GetBytes(32);
                var rawToken   = Convert.ToHexString(tokenBytes).ToLower();
                var tokenHash  = HashPassword(rawToken);       // re-use existing SHA-256 hasher
                var expiresAt  = DateTime.UtcNow.AddMinutes(30);

                var repo = new AuthRepository(_context);
                bool ok  = repo.RequestPasswordReset(request.Email, tokenHash, expiresAt);

                // No-op email transport in dev: echo the token so QA can paste
                // it into /reset-password manually. Production builds set the
                // `Email:Enabled` flag in appsettings to true and route through
                // an SMTP service — we just return success here.
                bool devEcho = !HttpContext.RequestServices
                    .GetRequiredService<IConfiguration>()
                    .GetSection("Email")
                    .GetValue<bool>("Enabled", false);

                await Task.Yield();   // keeps the async signature meaningful when SMTP is added.

                if (devEcho && ok)
                {
                    return Json(new {
                        success = true,
                        message = "If an account matches, a reset link has been sent.",
                        devToken = rawToken,
                        devExpiresAt = expiresAt,
                    });
                }

                return Json(new {
                    success = true,
                    message = "If an account matches, a reset link has been sent.",
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── POST api/Auth/VerifyResetToken ─────────────────────────────────
        // Lightweight check used by the Angular reset page to fail early
        // if a user pasted a stale / wrong token.
        [HttpPost]
        public JsonResult VerifyResetToken([FromBody] ResetPasswordRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request?.Token))
                    return Json(new { success = false, valid = false });
                var tokenHash = HashPassword(request.Token);
                // Reuse the same EF entity — we already have a DbSet<PasswordResetToken>.
                var live = _context.PasswordResetTokens
                                   .Any(t => t.TokenHash == tokenHash
                                          && !t.IsUsed
                                          && t.ExpiresAt > DateTime.UtcNow);
                return Json(new { success = true, valid = live });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── POST api/Auth/ResetPassword ────────────────────────────────────
        [HttpPost]
        public JsonResult ResetPassword([FromBody] ResetPasswordRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request?.Token)
                 || string.IsNullOrWhiteSpace(request?.NewPassword)
                 || request.NewPassword.Length < 8)
                {
                    return Json(new { success = false, message = "Invalid request." });
                }
                var tokenHash    = HashPassword(request.Token);
                var newPwdHash   = HashPassword(request.NewPassword);
                var repo         = new AuthRepository(_context);
                bool ok          = repo.ResetPassword(tokenHash, newPwdHash);
                return Json(new {
                    success = ok,
                    message = ok ? "Password updated. You can now log in."
                                  : "Reset link is invalid or expired."
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── POST api/Auth/ChangePassword ──────────────────────────────────────

        [HttpPost]
        [Authorize]
        public JsonResult ChangePassword(ChangePasswordRequest request)
        {
            try
            {
                var repo = new AuthRepository(_context);
                bool ok  = repo.ChangePassword(request.UserId,
                    HashPassword(request.CurrentPassword),
                    HashPassword(request.NewPassword));
                return Json(new { success = ok, message = ok ? "Password changed." : "Current password incorrect." });
            }
            catch (Exception ex) { return Json(new { success = false, error = ex.Message }); }
        }

        // ── POST api/Auth/UpdateProfile ───────────────────────────────────────

        [HttpPost]
        [Authorize]
        public JsonResult UpdateProfile(UpdateProfileRequest request)
        {
            try
            {
                var repo = new AuthRepository(_context);
                bool ok  = repo.UpdateProfile(request.UserId, request.FullName,
                    request.Phone, request.Address, request.LocalityId);
                return Json(new { success = ok });
            }
            catch (Exception ex) { return Json(new { success = false, error = ex.Message }); }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private int GetCurrentUserId()
        {
            var raw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                   ?? User.FindFirst("sub")?.Value;
            return int.TryParse(raw, out var id) ? id : 0;
        }

        private static string HashPassword(string password)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToHexString(bytes).ToLower();
        }
    }
}
