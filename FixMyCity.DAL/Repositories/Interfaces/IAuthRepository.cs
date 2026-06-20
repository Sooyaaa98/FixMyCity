using FixMyCity.DAL.Models;

namespace FixMyCity.DAL.Repositories.Interfaces;

/// <summary>
/// Data-access contract for authentication, registration, profile management,
/// SSO, password reset, account lockout, banning, and GDPR anonymization.
/// Sprint 2: LocalityId replaces VARCHAR Locality throughout; SSO, lockout,
/// password-reset, and anonymization methods added.
/// </summary>
public interface IAuthRepository
{
    // ── Login & Lockout ───────────────────────────────────────────────────────

    /// <summary>
    /// Validates email + password credentials.
    /// Returns the active, approved, non-banned, non-locked User; null on any failure.
    /// Does NOT record failed attempts — call RecordFailedLogin separately on failure.
    /// </summary>
    User ValidateLogin(string email, string passwordHash);

    /// <summary>
    /// Increments FailedLoginAttempts; applies a 30-minute lockout after 5 consecutive
    /// failures. Calls usp_RecordFailedLogin. Silent on error — must not crash login flow.
    /// US04.
    /// </summary>
    void RecordFailedLogin(string email);

    /// <summary>
    /// Resets FailedLoginAttempts to 0 and clears LockoutUntil after a successful login.
    /// Calls usp_ResetLoginAttempts.
    /// US04.
    /// </summary>
    void ResetLoginAttempts(int userId);

    // ── SSO ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves SSO identity: returns existing linked user, links to existing email, or
    /// creates a new Citizen. Calls usp_SSOLoginOrCreate.
    /// Returns (UserId, RoleId); both 0 on failure.
    /// US05.
    /// </summary>
    (int UserId, int RoleId) SSOLoginOrCreate(string ssoProvider, string ssoExternalId,
                                               string email, string fullName);

    // ── Registration ──────────────────────────────────────────────────────────

    /// <summary>
    /// Registers a new Citizen. Calls usp_RegisterCitizen.
    /// Returns the new UserId; 0 on failure. US01.
    /// </summary>
    int RegisterCitizen(string fullName, string email, string passwordHash,
                        string phone, string address, int localityId, string aadhaarNo);

    /// <summary>
    /// Registers a new Organisation (PWG) user + Organisation record.
    /// Calls usp_RegisterOrganisation. Returns the new UserId; 0 on failure. US02.
    /// </summary>
    int RegisterOrganisation(string fullName, string email, string passwordHash,
                             string phone, string address, int localityId,
                             string orgName, string orgType, string registrationNo,
                             string contactEmail, string contactPhone);

    /// <summary>
    /// Registers a new Department (Solver) user + Department record.
    /// Calls usp_RegisterDepartment. Returns the new UserId; 0 on failure. US03.
    /// </summary>
    int RegisterDepartment(string fullName, string email, string passwordHash,
                           string phone, string address, int localityId,
                           string deptName, string ministry, short categoryId,
                           string contactEmail, string contactPhone);

    // ── Profile ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a User by PK including Role, Locality, Organisation, Department, UserPoint.
    /// Null if not found.
    /// </summary>
    User GetUserById(int userId);

    /// <summary>Returns a User by email (includes Role, Locality). Null if not found.</summary>
    User GetUserByEmail(string email);

    /// <summary>
    /// Updates editable profile fields (name, phone, address, localityId).
    /// Calls usp_UpdateProfile. Returns true on success. US08.
    /// </summary>
    bool UpdateProfile(int userId, string fullName, string phone,
                       string address, int localityId);

    /// <summary>Returns true if an email address is already registered.</summary>
    bool EmailExists(string email);

    // ── Password Management ───────────────────────────────────────────────────

    /// <summary>
    /// Verifies old password before setting a new one (F8 guard in SP).
    /// Calls usp_ChangePassword with @OldPasswordHash.
    /// Returns true only if old password matched and update succeeded. US07.
    /// </summary>
    bool ChangePassword(int userId, string oldPasswordHash, string newPasswordHash);

    /// <summary>
    /// Stores a hashed reset token for the given email. Invalidates any live tokens first.
    /// Calls usp_RequestPasswordReset. Silent-fails for SSO-only accounts.
    /// Returns true on success. F16 / US07.
    /// </summary>
    bool RequestPasswordReset(string email, string tokenHash, DateTime expiresAt);

    /// <summary>
    /// Consumes a valid, unexpired reset token and sets the new password.
    /// Calls usp_ResetPassword. Returns true on success. F16 / US07.
    /// </summary>
    bool ResetPassword(string tokenHash, string newPasswordHash);

    // ── Account Lifecycle ─────────────────────────────────────────────────────

    /// <summary>
    /// GDPR-anonymizes a user account in place (PII fields overwritten).
    /// Calls usp_AnonymizeUser. Returns true on success. US09.
    /// </summary>
    bool AnonymizeUser(int targetUserId, int? adminUserId = null);

    // ── Lookups ───────────────────────────────────────────────────────────────

    /// <summary>Returns all roles. Used for role resolution and registration dropdowns.</summary>
    List<Role> GetAllRoles();

    /// <summary>Returns all active localities ordered by name. Used for registration dropdowns.</summary>
    List<Locality> GetAllLocalities();

    /// <summary>Returns all issue categories ordered by name. Used for registration dropdowns.</summary>
    List<IssueCategory> GetAllCategories();

    /// <summary>Returns a single locality by PK. Null if not found.</summary>
    Locality GetLocalityById(int localityId);

    /// <summary>
    /// Returns the dept_id for which this user is the registered rep. Null otherwise.
    /// Used to populate the deptId claim in JWT access tokens.
    /// </summary>
    int? GetDeptIdForUser(int userId);

    /// <summary>
    /// Returns the org_id for which this user is the registered rep. Null otherwise.
    /// Used in the Login response so PWG dashboards have a non-zero orgId to
    /// pass to /api/PWG/SubmitParticipationRequest etc.
    /// </summary>
    int? GetOrgIdForUser(int userId);

}
