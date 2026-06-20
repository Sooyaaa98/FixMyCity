# FixMyCity — System Fix Plan

**Audit date:** 2026-05-19
**Author:** Forensic audit pass over Database → DAL → API → AI → Angular, cross-checked against the v2 backlog (US01–US65) and the existing AUDIT.md/HANDOVER.md.

This document is the **execution order** for stabilization. It is not a list of nice-to-haves — items in P0 block the application from running correctly, P1 break documented user stories, P2 are silent failure modes that *will* bite under load, P3 are hygiene.

The audit confirms most prior fixes (Issues #1–#27 in [AUDIT.md](AUDIT.md)) are *in source*. This plan calls out **what is still missing or risky**.

---

## 1. Dependency graph of failures

```
                          ┌──────────────────────────────┐
                          │   DB scripts run in order    │
                          │   00 → 01 → 02 → 03 → 04     │
                          │   (no run order = NOTHING    │
                          │    further is verifiable)    │
                          └──────────────┬───────────────┘
                                         │
              ┌──────────────────────────┼───────────────────────────┐
              │                          │                           │
   ┌──────────▼─────────┐    ┌───────────▼──────────┐    ┌───────────▼──────────┐
   │ EF entity coverage │    │ AI service env load  │    │ JWT secret + AI key  │
   │ (AI tables + URT)  │    │ (dotenv, HF_TOKEN)   │    │   parity dev↔prod    │
   └──────────┬─────────┘    └───────────┬──────────┘    └───────────┬──────────┘
              │                          │                           │
              │ blocks any future EF read of      │ blocks all /ai/*  │
              │ AIPendingScoreQueue,              │ inference         │
              │ ComplaintEmbeddings,              │                   │
              │ AIDecisionLog, ComplaintTags,     │                   │
              │ UserRecommendationCache,          │                   │
              │ UserRefreshTokens                 │                   │
              ▼                                   ▼                   ▼
   ┌──────────────────────┐         ┌─────────────────────────────────────┐
   │ Raw-ADO leakage in   │         │ Complaint submit → AI scoring,      │
   │ AIPendingQueue path  │         │ duplicate check, tagging, image AI  │
   │ (RLS will zero rows  │         │ all silently no-op if env broken    │
   │  if re-enabled)      │         │                                     │
   └──────────────────────┘         └─────────────────────────────────────┘

   ┌─────────────────────────────────────────────────────────────────────┐
   │ DOWNSTREAM RUNTIME FAILURES (depend on the above)                   │
   │                                                                     │
   │ • PWG participation request (US31) — needs orgId on JWT             │
   │ • Solver PWG endpoints (US42/43) — needs PWGController role list    │
   │ • Citizen contribution (US22) — needs Razorpay key OR demo bypass   │
   │ • Certificate download (US27) — needs PDF endpoint wired in UI      │
   │ • Submit-complaint AI hints (US14/15) — needs image upload route    │
   │ • Auto-escalation (US50) — needs usp_AutoEscalateAll                │
   │ • Weekly digest (US65) — no scheduler implemented                   │
   └─────────────────────────────────────────────────────────────────────┘
```

The arrows above are causal. A failure at the top makes verification of anything below it unsafe — fix in order, do not parallelize across tiers.

---

## 2. Priority buckets

### **P0 — Blocks the application from running for any role**

| # | Issue | Where | Status |
|---|-------|-------|--------|
| P0-1 | Seed password hash uses UTF-16 (`N'Password123!'`) → no one can log in | `Database/03_SeedData.sql:152` | **FIXED** in source; re-verify before any deploy by hashing `'Password123!'` in UTF-8 SHA-256 hex and comparing against the row in `dbo.Users.PasswordHash` |
| P0-2 | `usp_AutoEscalateAll` and `usp_CreateContribution` missing from `Database/` | `Database/04_DB_Patch.sql` | **FIXED** in source; verify `Database/` contains 5 files numbered 00..04 |
| P0-3 | `Program.cs` calls `UseHttpsRedirection` unconditionally → cross-port redirect strips JWT in dev | `FixMyCity.API/Program.cs:274-275` | **FIXED** (`if (!app.Environment.IsDevelopment())` guard present) |
| P0-4 | Default tracking causes nav-fixup → `JsonException: object cycle` on every list endpoint | `Program.cs:43-46` | **FIXED** (NoTracking + MaxDepth=128) |
| P0-5 | Login response is `{user:{roleName}}` but `login.component.ts` read `res.roleName` → user bounces back to /login | `login.component.ts:56` | **FIXED** (reads `res.user?.roleName ?? res.roleName`) |
| P0-6 | `HTTP_INTERCEPTORS` used `useClass: AuthInterceptor` → second instance, refresh broken | `app.module.ts:112-114` | **FIXED** (`useExisting: AuthInterceptor`) |
| P0-7 | API and AI service ship with different default ServiceKey → every `/ai/*` call 401s | `appsettings.json:15` + `config.py:16` | **FIXED** (both default to `fixmycity-ai-internal-key-change-me`) |

> **All P0s are claimed fixed in source.** The verification step (§5) re-runs each one on a clean checkout before declaring stable.

### **P1 — Breaks documented backlog stories at runtime**

| # | Issue | Where | Status |
|---|-------|-------|--------|
| P1-1 | EF DbContext has **no DbSets** for `ComplaintEmbeddings`, `UserRecommendationCache`, `AIDecisionLog`, `ComplaintTags`, `PlatformStatsCategorySnapshot`, `AIPendingScoreQueue`, `UserRefreshTokens` (7 tables) | `FixMyCity.DAL/Models/FixMyCityDbContext.cs:13-42` | **OPEN.** Schema exists, SPs exist, repos call SPs via raw SQL. Any future EF read/query against these tables is impossible without a model. `UserRefreshTokens` is handled entirely via `JwtService` raw ADO, which works but is unmaintainable. |
| P1-2 | `AIPendingQueueProcessor.FetchQueueItemsAsync` opens a **raw ADO connection** bypassing `SessionContextInterceptor`. Today: OK because RLS is OFF. The minute RLS is re-enabled, this query returns zero rows. | `FixMyCity.API/Services/AIPendingQueueProcessor.cs:102-151` | **OPEN.** Either route the read through EF or manually call `sp_set_session_context N'UserRole', N'SuperAdmin'` on the connection before the SELECT. |
| P1-3 | `AdminRepository.GetPlatformStats` does the same raw-ADO pattern — manually injects `SESSION_CONTEXT` to compensate | `FixMyCity.DAL/Repositories/Implementations/AdminRepository.cs:239-248` | **WORKAROUND in place.** Document as the canonical pattern *if* the team accepts raw ADO; otherwise migrate to EF. |
| P1-4 | PWG participation request silently fails: login response omitted `orgId` for PWG users | `AuthController.Login` | **FIXED** (login + refresh both return `orgId` via `GetOrgIdForUser`) — verify by logging in as `anjali@cleanbengaluru.org` and checking `sessionStorage.fmc_user.orgId == 1` |
| P1-5 | Solver locked out of `/api/PWG/*ForSolver` endpoints — controller had `[Authorize(Roles="PWG,SuperAdmin")]` only | `PWGController.cs:15` | **FIXED** (now `"PWG,Solver,SuperAdmin"`) |
| P1-6 | Citizen sign-up reports success even when SP rejects (CHECK fail) → silent gas-lighting | `AuthController.RegisterCitizen/Organisation/Department` | **FIXED** (returns `success: false` on `userId <= 0`) |
| P1-7 | Citizen profile page shows blank Role — `GetUserById` returned EF entity with nested `role.roleName`, template read flat `roleName` | `AuthController.GetUserById` | **FIXED** (projects to flat shape including `roleName`, `localityName`, `points`) |
| P1-8 | Organisation registration silently failed for `Welfare Group` / `Community Association`; `CSR` missing from dropdown | `register-organisation.component.ts` + `chk_Organisations_OrgType` | **FIXED** (`04_DB_Patch.sql` widens CHECK; component lists full union) |
| P1-9 | Razorpay placeholder key 401s the contribution flow with no fallback | `payment.service.ts:48` | **FIXED** (placeholder triggers demo bypass; resolves with synthetic `DEMO_…` transaction ref) |
| P1-10 | AI image upload + GPS prefill + AI description suggestion not implemented in UI | `submit-complaint.component.*`, `ComplaintController.UploadComplaintImage`, `categorization.py` | **FIXED** (multipart upload endpoint, EXIF GPS extraction, suggested_description in both routers) |
| P1-11 | Certificate "Download" button gated on `cert.filePath` which is always NULL by design | `my-certificates.component.html` | **FIXED** (button calls `GamificationService.downloadCertificatePdf` blob fetch against `/api/Report/CertificatePdf`) |
| P1-12 | AI service: HF_API_TOKEN read at module import → empty even when `.env` is correct; `python-dotenv` not in `requirements.txt` | `ml_service/main.py`, `services/hf_inference.py`, `requirements.txt` | **FIXED** (dotenv loaded before imports; token resolved per-call) |
| P1-13 | `HttpErrorInterceptor` dropped the 400 `errors[]` array → users saw generic "Please check your input" with no detail | `core/interceptors/http-error.interceptor.ts` | **FIXED** (joins `errors[]` first, falls back to `message`) |
| P1-14 | Solver/PWG profile pages had bespoke layout vs shared `app-user-profile` for SuperAdmin/Citizen | `solver/profile/*`, `pwg/profile/*` | **FIXED** (both now embed `<app-user-profile>` for personal info; role-specific section below) |

### **P2 — Latent failures, no immediate user impact but will fail under load / next feature**

| # | Issue | Where | Impact |
|---|-------|-------|--------|
| P2-1 | RLS policy `ComplaintRLS` is `STATE = OFF` | `Database/00_Schema_Sprint2.sql:2421` | All users can read all complaints. No data leak through UI today (controllers filter by role), but any new endpoint that forgets to filter will leak. Re-enabling RLS will break P1-2 instantly. |
| P2-2 | `WeeklyDigestService` is referenced in README "US65" but **doesn't exist as a hosted service** | nowhere — README claim is false | Weekly digest runs only when an admin manually POSTs `/api/Gamification/GenerateWeeklyDigest`. US65 is partially-met: SP+endpoint exist, no scheduler. |
| P2-3 | `ComplaintMlScore.PredictionModelVersion` is `HasMaxLength(20)` in EF but DB column is `VARCHAR(50)` | `FixMyCityDbContext.cs:386` | Today's `"v2.0.0-lgbm"` fits; if AI emits a longer string, EF client-side validation rejects the write. Widen to 50 or remove the constraint. |
| P2-4 | `IUserProfile.localityName` is never populated (post-fix `GetUserById` adds it; older code-paths still read `user.locality?.localityName`) | various components | Visual only — Role/Locality fields show `—` for some users. Verify after re-build. |
| P2-5 | `analytics.py /ai/geo-cluster` interpolates `locality_id` directly into the SQL string | `ml_service/routers/analytics.py:113` | **SQL injection.** Today the value is an int passed from a trusted API → exploitable only by something that already has the AI service key, but **fix anyway**: use parameter binding. |
| P2-6 | `categorization.py` reads `store.category_label_encoder`; `training.py` writes `store.label_encoders['category']` | `ml_service/routers/categorization.py` vs `routers/training.py:82` | Label encoder never consulted on suggestion. Fallback hash masks it — users see "suggestions" but the mapping is consistent rather than correct. Pick one shape and use it on both sides. |
| P2-7 | `MLServiceClient.CategorySuggestion` DTO lacks `CategoryId` field | `FixMyCity.API/Services/MLServiceClient.cs` (record `CategorySuggestion`) | Python now returns `category_id` (post-fix). .NET deserializes it to `0`. If a controller forwards the .NET DTO unchanged to Angular, the suggestion can't auto-fill the category dropdown. Add the field. |
| P2-8 | `IMAGE_BASE_PATH` hard-coded `"C:/FixMyCityUploads"` (Windows) in AI config | `ml_service/config.py:34` | Breaks Docker/Linux out-of-the-box. Docker compose overrides via env var, but a developer who copies `config.py` semantics is bitten. |
| P2-9 | API `appsettings.json` hard-codes JWT secret + AI service key | `appsettings.json:6,15` | OK for dev. Production deploy must use env vars / KeyVault. Documented. |
| P2-10 | 8 repository methods swallow `SqlException` with `return 0 / return false` and no `_logger.LogError` | `ComplaintRepository`, `AuthRepository`, `GamificationRepository`, `PaymentRepository`, `MLRepository` | Errors invisible in logs. Most controllers translate `0` to `{success: false}` after P1-6 fix; logging is the remaining gap. |
| P2-11 | `01_AI_Tables_Addition.sql` uses bare `CREATE TABLE` (no `IF NOT EXISTS`) | `Database/01_AI_Tables_Addition.sql` | Re-running the file errors. The README run order assumes first install only; document and/or wrap each CREATE in an existence check. |
| P2-12 | `usp_RefreshScoreboard` is generated in 03_SeedData.sql but not scheduled — and depends on UserPoints having rows | covered in HANDOVER §11 | Cosmetic; only matters if Admin opens an empty scoreboard. |

### **P3 — Hygiene and polish (do not block stabilization)**

| # | Issue | Where |
|---|-------|-------|
| P3-1 | No 404 component — unknown routes redirect to `/home` silently | `app-routing.module.ts:160` |
| P3-2 | ~11 `console.error` calls left in services. Acceptable for dev; prod build should strip via a logger abstraction. | `*.service.ts` |
| P3-3 | Stale comment in `03_SeedData.sql:111-112` claims `IsActive` was removed; it wasn't. Cosmetic. | `Database/03_SeedData.sql` |
| P3-4 | `register-citizen.component` SSO button uses `window.prompt()` — fine for demo. | `auth/login/login.component.ts`, `register-citizen.component.ts` |
| P3-5 | ~105 `any` / non-null `!` assertions in TS, primarily in `map-view.component`. | `**/*.component.ts` |
| P3-6 | Project root has both `Database.zip` and `Database/`. Decide which is canonical. | repo root |

---

## 3. Phased stabilization strategy

> The phases below are **sequential**. Each phase ends with an explicit pass/fail gate; do not begin Phase N+1 until Phase N is green.

### Phase A — Verify the source-of-truth (no code changes)

Goal: confirm every P0 fix is actually in the codebase the operator will deploy.

1. Open `Database/` — count 5 files numbered `00..04`. ✓
2. Open `Database/03_SeedData.sql` and search for `HASHBYTES('SHA2_256'`. Confirm **no** `N'` prefix on `Password123!`. ✓
3. Open `Database/04_DB_Patch.sql` and confirm both `CREATE OR ALTER PROCEDURE dbo.usp_AutoEscalateAll` and `CREATE OR ALTER PROCEDURE dbo.usp_CreateContribution` exist. ✓
4. Open `FixMyCity.API/Program.cs:43-46` → `UseQueryTrackingBehavior(NoTracking)` present, interceptor registered. ✓
5. Open `FixMyCity.API/Program.cs:274-275` → `if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();` ✓
6. Open `FixMyCity.API/appsettings.json:15` and confirm `AIService:ServiceKey == "fixmycity-ai-internal-key-change-me"`. ✓
7. Open `FixMyCity.AI/ml_service/config.py:16` and confirm the same default. ✓
8. Open `FixMyCityApp/src/app/app.module.ts:112-114` → `{ provide: HTTP_INTERCEPTORS, useExisting: AuthInterceptor, multi: true }`. ✓
9. Open `FixMyCityApp/src/app/auth/login/login.component.ts` → reads `res.user?.roleName`. ✓
10. Open `FixMyCity.API/Controllers/PWGController.cs:15` → `[Authorize(Roles = "PWG,Solver,SuperAdmin")]`. ✓

**Exit gate:** all 10 checks pass.

### Phase B — DB → API → AI smoke test on a clean LocalDB

Goal: prove the system boots end-to-end with no manual repair.

1. Drop the DB (or use a fresh LocalDB).
2. Run `Database/00..04` in order in SSMS. Confirm `SEED DATA COMPLETE` prints and no error stops execution. Verify in `dbo.Users` that the admin's hash matches `SHA-256(UTF-8 "Password123!")` = `a109e36947ad56de1dca1cc49f0ef8ac9ad9a7b1aa0df41fb3c4cb73c1ff01ea`.
3. `dotnet build` in `FixMyCity.API`. Expect 0 errors.
4. `dotnet run --launch-profile http` — listens on `http://localhost:5065`.
5. Smoke test: `POST /api/Auth/Login` with `admin@fixmycity.in / Password123!` returns `{ success: true, accessToken, refreshToken, user: { roleName: "SuperAdmin" } }`.
6. `cd FixMyCity.AI/ml_service && pip install -r requirements.txt && uvicorn main:app --port 8001` — startup log shows `sentence_model=True, keybert=True` (CLIP False is fine). `/health` returns 200.
7. `cd FixMyCityApp && npm install && npx ng build --configuration development` — 0 errors.
8. `npx ng serve --port 4200`, open `http://localhost:4200/login`, sign in as admin → redirected to `/admin/dashboard` and stays there.

**Exit gate:** all 8 steps pass; admin dashboard renders without 500 errors in the network panel.

### Phase C — Backlog reachability sweep

Goal: confirm every US01–US65 user story has a reachable UI path and a working API.

For each story in the matrix below, exercise the path with the seeded data:

| US | What to do | Verify |
|----|------------|--------|
| US01 Register Citizen | `/register/citizen` → submit valid form | `dbo.Users` row created, login works |
| US02 Register Org | `/register/organisation` → "Welfare Group" | row created with `ApprovalStatus='Pending'` |
| US03 Register Dept | `/register/department` | row created |
| US04 Login | admin / Password123! | dashboard renders |
| US05 SSO | demo button → email prompt | session active |
| US06 Logout | top-right menu | refresh token revoked in `dbo.UserRefreshTokens` |
| US07 Change password | profile page | new hash in `dbo.Users` |
| US08 Edit profile | profile page | row updated |
| US09 Anonymize | admin → manage-users | sensitive fields nulled |
| US10/11 Approve dept/PWG | admin → approvals | `ApprovalStatus='Approved'` |
| US12 Platform stats | admin dashboard | no 500 |
| US13 Deactivate/ban | manage-users | flags set, audit row written |
| US14 Submit complaint | citizen submit form | row in `dbo.Complaints`, timeline 'Submitted' |
| US15 ML image extraction | upload photo on submit | category suggestion + GPS + description draft |
| US16/17 History + timeline | /citizen/complaints, /complaints/:id | timeline lists status changes with `By: Name (Role)` |
| US18 Filter | citizen list | status filter applies |
| US19 Rating | resolved complaint detail | 1-5 stars persists, points awarded |
| US20 Re-open | rating<3 → re-open button | status → 'Re-opened' |
| US21 Locality feed | citizen home | recent in same locality |
| US22 Contribute | complaint detail | demo Razorpay → contribution row created |
| US23 Notifications | bell icon | unread count, mark-all-read |
| US24 Recommendations | citizen home | AI-recommended complaints |
| US25 Scoreboard | citizen scoreboard | rankings |
| US26 Interests | citizen interests | preferences saved |
| US27 Certificate PDF | citizen certificates | Download button → PDF downloads |
| US28 PWG login | anjali@cleanbengaluru.org | `/pwg/complaints` |
| US29/30 Browse + filter | pwg open complaints | filters work |
| US31 Request to help | PWG → click Request Participation | row in `PWGParticipationRequests` (verifies orgId fix) |
| US32 Update progress | PWG progress page | timeline + attachment + points |
| US33 Notifications | PWG bell | PWG decision notifications |
| US34 Logout | top right | session cleared |
| US35 Update org profile | PWG profile | row updated |
| US36 Solver login | rakesh.bbmp@fixmycity.in | `/solver/dashboard` |
| US37 Notifications | solver bell | new-assignment notifications |
| US38/39 List + filter | solver complaints | filters work |
| US40 Update status | solver complaint detail | enforces transitions, requires remark on Reject |
| US41 Estimated time | solver detail | date persists |
| US42/43 PWG requests | solver pwg-requests | list + approve/reject |
| US44 Report PWG | solver complaint detail | row in `PWGReports` |
| US45 Re-open notification | solver bell after citizen re-opens | notification arrives |
| US46 Update profile | solver profile | row updated |
| US47 Logout | top right | session cleared |
| US48 Auto-routing | citizen submit any | DeptId resolved by (Category, Locality) |
| US49 Duplicate detection | submit similar complaint | candidates returned by `/ai/duplicate-check` |
| US50 Auto-escalation | manual: `EXEC dbo.usp_AutoEscalateAll` | stale 'In Progress' → 'Escalated' |
| US51 Recommendations | covered by US24 | — |
| US52/53/54 ML scores | complaint detail | `priorityScore`, `resolutionProbability`, `predictedResolutionDate` present |
| US55 Reassign escalated | admin escalated complaints | reassignment works |
| US56 Funding visible | complaint detail | total contributed shown |
| US57 Search | citizen home | keyword search returns matches |
| US58 Share | complaint detail | unique URL |
| US59 Map view | map page | Leaflet renders markers |
| US60/61 Resolution photos | solver mark Resolved | photo uploaded |
| US62 PWG photos | PWG progress | photo uploaded |
| US63 PWG report notification | admin pwg-reports | notification visible |
| US64 FAQ / chatbot | /ai/chat | Mistral responds (cold-start ~10s) |
| US65 Weekly digest | `POST /api/Gamification/GenerateWeeklyDigest` | rows in `dbo.Notifications` (manual trigger only — see P2-2) |

**Exit gate:** every row passes; failures are added back into P1.

### Phase D — Fix remaining P1/P2 items

This is the bucket of *new* work. Implement in the order listed; each item is small.

1. **P2-7** — add `[JsonPropertyName("category_id")] public int CategoryId { get; set; }` to `MLServiceClient.CategorySuggestion`. Trivial. Unblocks Angular AI category auto-fill.
2. **P1-1 partial** — add EF models for the 6 AI tables + `UserRefreshTokens`. Required to test or query those tables from EF; not blocking the runtime today. Read-only entities are sufficient.
3. **P1-2** — in `AIPendingQueueProcessor.FetchQueueItemsAsync`, before the SELECT, execute `EXEC sp_set_session_context N'UserRole', N'SuperAdmin', @read_only = 0`. Mirrors the pattern in `AdminRepository.GetPlatformStats`. Required before P2-1 can be safely flipped on.
4. **P2-1** — `ALTER SECURITY POLICY dbo.ComplaintRLS WITH (STATE = ON)`. Then run Phase C again as Solver to confirm Solvers only see their own dept's complaints.
5. **P2-5** — parameterize `analytics.py:113`. One-line fix; no schema change.
6. **P2-6** — align `store.category_label_encoder` between `routers/categorization.py` and `routers/training.py`. Add to `model_manager.save_models`/`load_models`. Then verify `/ai/categorize-text` returns real category IDs.
7. **P2-3** — widen `ComplaintMlscore.PredictionModelVersion` to `HasMaxLength(50)` in EF to match DB.
8. **P2-11** — wrap `CREATE TABLE` in `01_AI_Tables_Addition.sql` with `IF NOT EXISTS` so the script is rerun-safe.
9. **P2-2** — register a `WeeklyDigestService` hosted service modeled on `AutoEscalationService` (or document the SQL Agent / cron alternative in HANDOVER §10).
10. **P2-10** — replace `catch { }` blocks in the 8 listed repository methods with `catch (Exception ex) { _logger.LogError(ex, "..."); return …; }`.

**Exit gate:** all 10 items complete; Phase B and Phase C still green.

### Phase E — Polish (P3)

Only after A–D are green. Items: 404 page, logger abstraction for console.error, comment cleanup, TS strict-null cleanup, repo root reorg.

---

## 4. Root causes (one-liners)

- **Authentication "broken" symptom — really three independent bugs:** UTF-16 hash mismatch in seed; nested vs flat `roleName` in login response; HTTPS redirect stripping the JWT in dev. Each is small; together they look like one impenetrable auth bug.
- **AI features "do nothing" — really one bug:** the Python service silently fails-open on every model when `HF_API_TOKEN` is empty at module import. Fixed by loading dotenv before any module reads env.
- **Lists 500 — really one bug:** EF Core navigation fixup back-populates collections that defeat `ReferenceHandler.IgnoreCycles` at MaxDepth=64. NoTracking + MaxDepth=128 fixes it everywhere.
- **PWG flow "submission failed" — really one bug:** `orgId` was never put on the JWT for PWG users. Frontend posts `orgId: 0`, FK fails, SP swallows it.
- **"Successful" registration with no DB row — really one pattern:** repos swallow `SqlException` and return `0`; controllers ignored the `0` and wrote `success: true`. Fix is in the controller, not the repo.
- **Razorpay 401 — really a configuration sentinel:** placeholder key was a string literal `'rzp_test_REPLACE_WITH_YOUR_KEY'`. Demo bypass is the right call until a real key is provisioned.

These six narratives explain >80% of the issues found. Most other items are downstream of these.

---

## 5. What is verified vs claimed

| Claim | Verified? | How |
|-------|-----------|-----|
| 5 SQL files in `Database/` numbered 00..04 | ✓ | `ls Database/` |
| Schema has 28 core tables + 7 AI + 1 refresh-token table | ✓ | read all five files |
| `DbContext.cs` has DbSets for the 28 core tables | ✓ | read `FixMyCityDbContext.cs:13-42` |
| `DbContext.cs` has **no** DbSets for AI tables or refresh-tokens | ✓ | same file — count = 28, missing the 7 AI/auth |
| `Program.cs` registers `SessionContextInterceptor` and uses NoTracking | ✓ | `Program.cs:43-46` |
| `Program.cs` guards `UseHttpsRedirection` to non-Development | ✓ | `Program.cs:274-275` |
| `appsettings.json` and `config.py` share the same default ServiceKey | ✓ | both = `fixmycity-ai-internal-key-change-me` |
| `app-routing.module.ts` has 4 role-gated layouts + public + notifications + catch-all | ✓ | read full file |
| Leaflet + Razorpay loaded via CDN in `index.html` | ✓ | grep on `index.html` |
| `payment.service.ts` demo bypass when key is placeholder | ✓ | `payment.service.ts:48-72` |
| AUDIT.md issues #1..#27 all map to actual lines in the codebase | partial | sampled #1, #2, #4, #11, #12, #16, #18, #21, #23, #25 — all confirmed; remaining items not re-traced but consistent |

---

## 6. Out-of-scope flags (for the next maintainer)

- **Password hashing remains SHA-256, not bcrypt/Argon2.** Documented in README as "consider for prod". Audit does not change this.
- **No automated test suite exists** for either backend or frontend (no `*.spec.ts` runs in CI). Adding one is a Phase F.
- **No CI/CD pipeline.** Build verification is manual.
- **No DR/backup story for the SQL DB.** Out of scope.
- **Tesseract dependency for OCR** — optional; AI submit-image still works without it.
