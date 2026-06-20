using FixMyCity.DAL.Models;
using FixMyCity.DAL.Repositories.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Data;

namespace FixMyCity.DAL.Repositories.Implementations;

/// <summary>
/// Implements <see cref="IAuthRepository"/>.
/// All write operations delegate to stored procedures.
/// All read operations use EF Core LINQ with explicit Includes.
///
/// Phase-2 hardening (2026-05-19): all catch blocks now log via ILogger. The
/// silent-by-design RecordFailedLogin/ResetLoginAttempts paths log at Debug level
/// (still recoverable) so they don't spam Error in the happy path. Register*
/// methods log at Error so a CHECK-constraint or duplicate-key failure surfaces
/// in ops logs.
/// </summary>
public class AuthRepository : IAuthRepository
{
    private readonly FixMyCityDbContext _context;
    private readonly ILogger<AuthRepository> _logger;

    public AuthRepository(FixMyCityDbContext context, ILogger<AuthRepository> logger)
    {
        _context = context;
        _logger  = logger ?? NullLogger<AuthRepository>.Instance;
    }

    // Backward-compatible overload. Existing controllers still call
    // `new AuthRepository(_context)` directly — they get a NullLogger.
    // Phase 3+ should migrate controllers to constructor-inject IAuthRepository
    // via DI so log records carry the request scope's logger.
    public AuthRepository(FixMyCityDbContext context)
        : this(context, NullLogger<AuthRepository>.Instance) { }

    // ── ValidateLogin ─────────────────────────────────────────────────────────
    // fn_ValidateLogin in SQL also checks IsSuspended = 0. EF mirror added below
    // for symmetry (Phase 2 added User.IsSuspended).
    public User ValidateLogin(string email, string passwordHash)
    {
        return _context.Users
                       .Include(u => u.Role)
                       .Include(u => u.Locality)
                       .FirstOrDefault(u => u.Email == email
                                         && u.PasswordHash == passwordHash
                                         && u.IsActive == true
                                         && u.IsBanned == false
                                         && u.IsSuspended == false
                                         && u.IsApproved == true
                                         && (u.LockoutUntil == null
                                             || u.LockoutUntil < DateTime.Now));
    }

    // ── RecordFailedLogin ─────────────────────────────────────────────────────

    public void RecordFailedLogin(string email)
    {
        try
        {
            _context.Database.ExecuteSqlRaw(
                "EXEC dbo.usp_RecordFailedLogin @Email",
                new SqlParameter("@Email", email));
        }
        catch (Exception ex)
        {
            // Debug — by design we must not interrupt the login response flow.
            _logger.LogDebug(ex, "RecordFailedLogin SP raised for {Email}", email);
        }
    }

    // ── ResetLoginAttempts ────────────────────────────────────────────────────

    public void ResetLoginAttempts(int userId)
    {
        try
        {
            _context.Database.ExecuteSqlRaw(
                "EXEC dbo.usp_ResetLoginAttempts @UserId",
                new SqlParameter("@UserId", userId));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ResetLoginAttempts SP raised for {UserId}", userId);
        }
    }

    // ── SSOLoginOrCreate ──────────────────────────────────────────────────────

    public (int UserId, int RoleId) SSOLoginOrCreate(string ssoProvider,
        string ssoExternalId, string email, string fullName)
    {
        try
        {
            var outUserId = new SqlParameter
            {
                ParameterName = "@UserId",
                SqlDbType = SqlDbType.Int,
                Direction = ParameterDirection.Output
            };
            var outRoleId = new SqlParameter
            {
                ParameterName = "@RoleId",
                SqlDbType = SqlDbType.TinyInt,
                Direction = ParameterDirection.Output
            };

            _context.Database.ExecuteSqlRaw(
                "EXEC dbo.usp_SSOLoginOrCreate @SSOProvider, @SSOExternalId, @Email, @FullName, @UserId OUTPUT, @RoleId OUTPUT",
                new SqlParameter("@SSOProvider", ssoProvider),
                new SqlParameter("@SSOExternalId", ssoExternalId),
                new SqlParameter("@Email", email),
                new SqlParameter("@FullName", fullName),
                outUserId, outRoleId);

            int userId = outUserId.Value != DBNull.Value ? (int)outUserId.Value : 0;
            int roleId = outRoleId.Value != DBNull.Value ? (int)(byte)outRoleId.Value : 0;
            return (userId, roleId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SSOLoginOrCreate failed: provider {Provider} email {Email}",
                ssoProvider, email);
            return (0, 0);
        }
    }

    // ── RegisterCitizen ───────────────────────────────────────────────────────

    public int RegisterCitizen(string fullName, string email, string passwordHash,
                               string phone, string address, int localityId,
                               string aadhaarNo)
    {
        try
        {
            var outId = new SqlParameter
            {
                ParameterName = "@NewUserId",
                SqlDbType = SqlDbType.Int,
                Direction = ParameterDirection.Output
            };

            _context.Database.ExecuteSqlRaw(
                "EXEC dbo.usp_RegisterCitizen @FullName, @Email, @PasswordHash, @Phone, @Address, @LocalityId, @AadhaarNo, @NewUserId OUTPUT",
                new SqlParameter("@FullName", fullName),
                new SqlParameter("@Email", email),
                new SqlParameter("@PasswordHash", passwordHash),
                new SqlParameter("@Phone", phone),
                new SqlParameter("@Address", address),
                new SqlParameter("@LocalityId", localityId),
                new SqlParameter("@AadhaarNo", (object)aadhaarNo ?? DBNull.Value),
                outId);

            return outId.Value != DBNull.Value ? (int)outId.Value : 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "RegisterCitizen failed: email {Email} (CHECK constraint or unique-key violation likely)",
                email);
            return 0;
        }
    }

    // ── RegisterOrganisation ──────────────────────────────────────────────────

    public int RegisterOrganisation(string fullName, string email, string passwordHash,
                                    string phone, string address, int localityId,
                                    string orgName, string orgType, string registrationNo,
                                    string contactEmail, string contactPhone)
    {
        try
        {
            var outUserId = new SqlParameter
            {
                ParameterName = "@UserId",
                SqlDbType = SqlDbType.Int,
                Direction = ParameterDirection.Output
            };

            _context.Database.ExecuteSqlRaw(
                "EXEC dbo.usp_RegisterOrganisation @FullName, @Email, @PasswordHash, @Phone, @Address, @LocalityId, @OrgName, @OrgType, @RegistrationNo, @ContactEmail, @ContactPhone, @UserId OUTPUT",
                new SqlParameter("@FullName", fullName),
                new SqlParameter("@Email", email),
                new SqlParameter("@PasswordHash", passwordHash),
                new SqlParameter("@Phone", phone),
                new SqlParameter("@Address", address),
                new SqlParameter("@LocalityId", localityId),
                new SqlParameter("@OrgName", orgName),
                new SqlParameter("@OrgType", orgType),
                new SqlParameter("@RegistrationNo", registrationNo),
                new SqlParameter("@ContactEmail", contactEmail),
                new SqlParameter("@ContactPhone", contactPhone),
                outUserId);

            return outUserId.Value != DBNull.Value ? (int)outUserId.Value : 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "RegisterOrganisation failed: email {Email} orgType {OrgType} regNo {RegNo}",
                email, orgType, registrationNo);
            return 0;
        }
    }

    // ── RegisterDepartment ────────────────────────────────────────────────────

    public int RegisterDepartment(string fullName, string email, string passwordHash,
                                  string phone, string address, int localityId,
                                  string deptName, string ministry, short categoryId,
                                  string contactEmail, string contactPhone)
    {
        try
        {
            var outUserId = new SqlParameter
            {
                ParameterName = "@UserId",
                SqlDbType = SqlDbType.Int,
                Direction = ParameterDirection.Output
            };

            _context.Database.ExecuteSqlRaw(
                "EXEC dbo.usp_RegisterDepartment @FullName, @Email, @PasswordHash, @Phone, @Address, @LocalityId, @DeptName, @Ministry, @CategoryId, @ContactEmail, @ContactPhone, @UserId OUTPUT",
                new SqlParameter("@FullName", fullName),
                new SqlParameter("@Email", email),
                new SqlParameter("@PasswordHash", passwordHash),
                new SqlParameter("@Phone", phone),
                new SqlParameter("@Address", address),
                new SqlParameter("@LocalityId", localityId),
                new SqlParameter("@DeptName", deptName),
                new SqlParameter("@Ministry", ministry),
                new SqlParameter("@CategoryId", categoryId),
                new SqlParameter("@ContactEmail", contactEmail),
                new SqlParameter("@ContactPhone", contactPhone),
                outUserId);

            return outUserId.Value != DBNull.Value ? (int)outUserId.Value : 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "RegisterDepartment failed: email {Email} dept {DeptName} categoryId {CategoryId}",
                email, deptName, categoryId);
            return 0;
        }
    }

    // ── GetUserById ───────────────────────────────────────────────────────────

    public User GetUserById(int userId)
    {
        return _context.Users
                       .Include(u => u.Role)
                       .Include(u => u.Locality)
                       .Include(u => u.Organisation)
                       .Include(u => u.Department)
                       .Include(u => u.UserPoint)
                       .FirstOrDefault(u => u.UserId == userId);
    }

    // ── GetUserByEmail ────────────────────────────────────────────────────────

    public User GetUserByEmail(string email)
    {
        return _context.Users
                       .Include(u => u.Role)
                       .Include(u => u.Locality)
                       .FirstOrDefault(u => u.Email == email);
    }

    // ── EmailExists ───────────────────────────────────────────────────────────

    public bool EmailExists(string email)
        => _context.Users.Any(u => u.Email == email);

    // ── UpdateProfile ─────────────────────────────────────────────────────────

    public bool UpdateProfile(int userId, string fullName, string phone,
                              string address, int localityId)
    {
        try
        {
            _context.Database.ExecuteSqlRaw(
                "EXEC dbo.usp_UpdateProfile @UserId, @FullName, @Phone, @Address, @LocalityId",
                new SqlParameter("@UserId", userId),
                new SqlParameter("@FullName", fullName),
                new SqlParameter("@Phone", phone),
                new SqlParameter("@Address", address),
                new SqlParameter("@LocalityId", localityId));
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateProfile failed for user {UserId}", userId);
            return false;
        }
    }

    // ── ChangePassword ────────────────────────────────────────────────────────

    public bool ChangePassword(int userId, string oldPasswordHash, string newPasswordHash)
    {
        try
        {
            var outSuccess = new SqlParameter
            {
                ParameterName = "@IsSuccess",
                SqlDbType = SqlDbType.Bit,
                Direction = ParameterDirection.Output
            };

            // F8: SP validates @OldPasswordHash before updating — do not bypass.
            _context.Database.ExecuteSqlRaw(
                "EXEC dbo.usp_ChangePassword @UserId, @OldPasswordHash, @NewPasswordHash, @IsSuccess OUTPUT",
                new SqlParameter("@UserId", userId),
                new SqlParameter("@OldPasswordHash", oldPasswordHash),
                new SqlParameter("@NewPasswordHash", newPasswordHash),
                outSuccess);

            return outSuccess.Value != DBNull.Value && (bool)outSuccess.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ChangePassword failed for user {UserId}", userId);
            return false;
        }
    }

    // ── RequestPasswordReset ──────────────────────────────────────────────────

    public bool RequestPasswordReset(string email, string tokenHash, DateTime expiresAt)
    {
        try
        {
            var outSuccess = new SqlParameter
            {
                ParameterName = "@IsSuccess",
                SqlDbType = SqlDbType.Bit,
                Direction = ParameterDirection.Output
            };

            _context.Database.ExecuteSqlRaw(
                "EXEC dbo.usp_RequestPasswordReset @Email, @TokenHash, @ExpiresAt, @IsSuccess OUTPUT",
                new SqlParameter("@Email", email),
                new SqlParameter("@TokenHash", tokenHash),
                new SqlParameter("@ExpiresAt", expiresAt),
                outSuccess);

            return outSuccess.Value != DBNull.Value && (bool)outSuccess.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RequestPasswordReset failed for {Email}", email);
            return false;
        }
    }

    // ── ResetPassword ─────────────────────────────────────────────────────────

    public bool ResetPassword(string tokenHash, string newPasswordHash)
    {
        try
        {
            var outSuccess = new SqlParameter
            {
                ParameterName = "@IsSuccess",
                SqlDbType = SqlDbType.Bit,
                Direction = ParameterDirection.Output
            };

            _context.Database.ExecuteSqlRaw(
                "EXEC dbo.usp_ResetPassword @TokenHash, @NewPasswordHash, @IsSuccess OUTPUT",
                new SqlParameter("@TokenHash", tokenHash),
                new SqlParameter("@NewPasswordHash", newPasswordHash),
                outSuccess);

            return outSuccess.Value != DBNull.Value && (bool)outSuccess.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ResetPassword failed (tokenHash redacted)");
            return false;
        }
    }

    // ── AnonymizeUser ─────────────────────────────────────────────────────────

    public bool AnonymizeUser(int targetUserId, int? adminUserId = null)
    {
        try
        {
            _context.Database.ExecuteSqlRaw(
                "EXEC dbo.usp_AnonymizeUser @TargetUserId, @AdminUserId",
                new SqlParameter("@TargetUserId", targetUserId),
                new SqlParameter("@AdminUserId", (object)adminUserId ?? DBNull.Value));
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AnonymizeUser failed for target {TargetUserId}", targetUserId);
            return false;
        }
    }

    // ── GetAllRoles ───────────────────────────────────────────────────────────

    public List<Role> GetAllRoles()
        => _context.Roles.OrderBy(r => r.RoleName).ToList();

    // ── GetAllLocalities ──────────────────────────────────────────────────────

    public List<Locality> GetAllLocalities()
        => _context.Localities
                   .Where(l => l.IsActive)
                   .OrderBy(l => l.LocalityName)
                   .ToList();

    // ── GetAllCategories ──────────────────────────────────────────────────────

    public List<IssueCategory> GetAllCategories()
        => _context.IssueCategories
                   .OrderBy(c => c.CategoryName)
                   .ToList();

    // ── GetLocalityById ───────────────────────────────────────────────────────

    public Locality GetLocalityById(int localityId)
        => _context.Localities.FirstOrDefault(l => l.LocalityId == localityId);

    // ── GetDeptIdForUser ──────────────────────────────────────────────────────
    // Returns the dept_id where this user is the dept rep. Used for JWT claims.
    public int? GetDeptIdForUser(int userId)
    {
        return _context.Departments
            .Where(d => d.UserId == userId)
            .Select(d => (int?)d.DeptId)
            .FirstOrDefault();
    }

    // ── GetOrgIdForUser ──────────────────────────────────────────────────────
    // Returns the org_id where this user is the org rep. Used in the Login
    // response so PWG dashboards (Open complaints → Request Participation, etc.)
    // can submit with the correct OrgId — previously the response omitted it
    // and the frontend sent orgId=0 which fails the FK on PWGParticipationRequests.
    public int? GetOrgIdForUser(int userId)
    {
        return _context.Organisations
            .Where(o => o.UserId == userId)
            .Select(o => (int?)o.OrgId)
            .FirstOrDefault();
    }

}
