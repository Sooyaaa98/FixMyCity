# FixMyCity — Handover & Operations Guide

**Audit date:** 2026-05-18 → re-verified 2026-05-19 → Phases 1–4 + final verification 2026-05-19
**Status:** Production-ready after the documented run order in **[FINAL_SETUP.md](FINAL_SETUP.md)**. Every backlog story US01–US65 has been walked against the current code and seeded data; 65/65 are reachable. See [AUDIT.md](AUDIT.md) "Final System Verification" section at the top for the full sign-off table.

> **Setting up for the first time?** Use **[FINAL_SETUP.md](FINAL_SETUP.md)** — it is the canonical setup doc and supersedes both the original `README.md` and §2–§5 of this file. The rest of this document focuses on architecture decisions, integration details, and ongoing operations / troubleshooting.

---

## 1. Working credentials (post-seed)

All passwords below are **`Password123!`** (case-sensitive, with the exclamation mark). Hash is SHA-256 of UTF-8 bytes, stored as 64-char lowercase hex in `dbo.Users.PasswordHash`.

### 1.1 Canonical accounts (seeded by `03_SeedData.sql`)

| Role | Email | Note |
|------|-------|------|
| SuperAdmin | `admin@fixmycity.in` | Full platform access; bypasses RLS |
| Solver — BBMP Road Engineering | `rakesh.bbmp@fixmycity.in` | DeptId 1 |
| Solver — BWSSB Water Supply | `priya.bwssb@fixmycity.in` | DeptId 2 |
| Solver — BESCOM Electricity | `suresh.bescom@fixmycity.in` | DeptId 3 |
| PWG — Clean Bengaluru NGO | `anjali@cleanbengaluru.org` | OrgId 1 |
| PWG — IISc Civic Group | `vikram@iiscbangalore.org` | OrgId 2 |
| PWG — Infosys CSR | `meera@infosys-csr.org` | OrgId 3 |
| Citizens (12 total) | `arjun.r@example.com`, `kavya.s@example.com`, `rohan.k@example.com`, `sneha.p@example.com`, `karthik.i@example.com`, `aishwarya.n@example.com`*, `deepak.s@example.com`, `pooja.v@example.com`, `sanjay.m@example.com`, `ananya.g@example.com`, `vikrant.c@example.com`, `lakshmi.p@example.com` | UserIds 8..19 |

\* The seed file uses `aish.n@example.com` for Aishwarya.

### 1.2 Phase-1 massive seed accounts (added by `05_MassiveSeed.sql`)

**Additional solvers** (`Password123!`):

| Email | Department |
|-------|------------|
| `anita.bbmp2@fixmycity.in` | BBMP Solid Waste Management (DeptId 4) |
| `venkat.bbmp3@fixmycity.in` | BBMP Parks & Trees (DeptId 5) |
| `ranjit.kspcb@fixmycity.in` | KSPCB Pollution Control (DeptId 6) |
| `nikhil.bmtc@fixmycity.in` | BMTC Public Transport (DeptId 7) |
| `divya.animal@fixmycity.in` | KA Animal Welfare Board (DeptId 8) |
| `pending.solver@fixmycity.in` | **PENDING — login blocked** (`fn_ValidateLogin` requires `IsApproved = 1`) |
| `rejected.solver@fixmycity.in` | **REJECTED — login blocked** |

**Additional PWGs** (`Password123!`):

| Email | Organisation | Type |
|-------|--------------|------|
| `manish@bbb.org` | Bangalore Bicycle Brigade (OrgId 4) | Community Association |
| `sushma@welfareforall.org` | Welfare for All Trust (OrgId 5) | Welfare Group |
| `rajiv@cca.org` | Citizens for Civic Action (OrgId 6) | NGO |
| `pankaj@aravindcsr.org` | Aravind Eye Hospital CSR (OrgId 7) | CSR |
| `bhavna@saahasi.org` | Saahasi Volunteers (OrgId 8) | Student Group |
| `ashwin@techmcsr.org` | Tech Mahindra Foundation (OrgId 9) | CSR |
| `aditi@sankalp.org` | Sankalp Initiative (OrgId 10) | NGO |
| `pending.pwg@fixmycity.in` | **PENDING — login blocked** | NGO |
| `rejected.pwg@fixmycity.in` | **REJECTED — login blocked** | Other |

**Edge-case accounts** (for QA of auth flows):

| Email | Behaviour |
|-------|-----------|
| `sso.user@gmail.com` | SSO-only — no password; log in via the demo Google SSO button |
| `banned.spammer@example.com` | `IsBanned = 1`, `IsActive = 0` — `fn_ValidateLogin` returns 0 |
| `suspended.user@example.com` | `IsSuspended = 1` — `fn_ValidateLogin` returns 0 |
| `locked.user@example.com` | `LockoutUntil` set 30 min ahead — `fn_ValidateLogin` returns 0 until lockout expires |
| `deactivated.user@example.com` | `IsActive = 0` — `fn_ValidateLogin` returns 0 |

**Regular citizens** (60 active citizens, all use `Password123!`): `vikram.s2@example.com` through `ajay.bj@example.com`. UserIds 41–100. See [Database/05_MassiveSeed.sql](Database/05_MassiveSeed.sql) for the full list.

---

## 2. Database setup — verified run order

Open SQL Server Management Studio (or `sqlcmd`) and run the scripts from `Database/` **in this order**. Each is idempotent in the sense documented per file.

| # | File | Behavior | Idempotent? |
|---|------|----------|-------------|
| 1 | `00_Schema_Sprint2.sql` | Drops and recreates `FixMyCityDB`, creates 28 tables + 36 SPs + RLS policy + initial Sprint-2 seed | **Destructive** — drops DB if exists. Run only on first install or full reset. |
| 2 | `01_AI_Tables_Addition.sql` | Adds 6 AI tables (`ComplaintEmbeddings`, `UserRecommendationCache`, `AIDecisionLog`, `ComplaintTags`, `PlatformStatsCategorySnapshot`, `AIPendingScoreQueue`) and 5 AI SPs | **Idempotent** as of Phase 1 — every `CREATE TABLE` / `CREATE INDEX` is guarded by `IF NOT EXISTS`; SPs are `CREATE OR ALTER`. Safe to re-run. |
| 3 | `02_UserRefreshTokens.sql` | Creates `UserRefreshTokens` table for JWT refresh rotation | Idempotent (`IF NOT EXISTS`) |
| 4 | `03_SeedData.sql` | Wipes & re-seeds Roles, Localities, Categories, Users, Departments, Organisations, Notifications, 30 demo Complaints with full lifecycle coverage, ratings, contributions, ML scores | **Idempotent** — wipes then re-seeds in dependency order |
| 5 | `04_DB_Patch.sql` | Adds `usp_AutoEscalateAll` and `usp_CreateContribution` (consumed by `AutoEscalationService` and `PaymentRepository`); widens `chk_Organisations_OrgType` to accept all UI types | Idempotent (`CREATE OR ALTER PROCEDURE`; guarded constraint drop+recreate) |
| 6 | `05_MassiveSeed.sql` | Phase-1 additive seed: **+81 users, +7 departments, +9 organisations, +8 localities, +2 categories, +170 complaints, +full lifecycle data** (ratings, contributions, escalations, PWG requests, AI scores, tags, decisions, embeddings, recommendation cache, certificates, audit log, 14-day stats history). | **Additive + idempotent** (probe on `anita.bbmp2@fixmycity.in`). Re-runs are no-ops. |

After step 5 you'll see the canonical-seed summary:
```
SEED DATA COMPLETE
30 complaints: Submitted=5, In Progress=7, Resolved=12, Re-opened=2, Escalated=2, Rejected=2
19 users, 3 departments, 3 PWG organisations, 14 ratings, 6 contributions, …
```

After step 6 you'll see the cumulative totals:
```
MASSIVE SEED COMPLETE — Phase 1
  Users:                   100
    of which Citizens:     77
  Departments:             10
  Organisations:           12
  Complaints:              200
  Ratings:                 ~50
  Contributions:           29
  PWGRequests:             26
  Escalations:             14
  DuplicateLinks:          9
  MLScores:                180
  Tags:                    ~525
  AIDecisionLog:           ~360
  RecommendationCache:     up to 600
  Certificates:            ~50
  AuditLog:                ~21
```

If you re-run step 4 only (re-seed), repeat steps 5 and 6 after — both are required for the system to be fully operational with massive seed data. Step 6's idempotency probe will short-circuit on a fresh re-run unless step 4 (which deletes the probe row's user) has just executed.

---

## 3. Backend (.NET 8 API + DAL)

### Configuration

`FixMyCity.API/appsettings.json`:

| Key | Default | Production note |
|-----|---------|-----------------|
| `ConnectionStrings:DefaultConnection` | `Server=(localdb)\\MSSQLLocalDB;Database=FixMyCityDB;Trusted_Connection=True;TrustServerCertificate=True;` | Change to your SQL Server instance |
| `Jwt:Secret` | A dev value | **Replace.** Generate with `openssl rand -base64 32` |
| `Jwt:Issuer` / `Jwt:Audience` | `FixMyCityAPI` / `FixMyCityApp` | Used in token validation |
| `Jwt:AccessTokenMinutes` | `15` | Access-token TTL |
| `Jwt:RefreshTokenDays` | `7` | Refresh-token TTL |
| `AIService:BaseUrl` | `http://localhost:8001` | Python FastAPI service |
| `AIService:ServiceKey` | A dev value | **Replace** and set the same value as `AI_SERVICE_KEY` in the Python service env |
| `AllowedOrigins` | `[]` (allow-any in dev) | Production: list your Angular origin |

### Run

```powershell
cd FixMyCity.API
dotnet restore
dotnet run
```

The API listens on `http://localhost:5065` (matches `FixMyCityApp/src/environments/environment.ts`). Swagger UI: `http://localhost:5065/swagger`.

### Build verification

```powershell
cd FixMyCity.API
dotnet build
```

Expected: `0 Error(s)`. Warnings (~126 CS8618 nullable) are benign — they are on DTO classes that the framework hydrates from JSON.

### Background services

| Service | Schedule | What it does |
|---------|----------|--------------|
| `AutoEscalationService` | Every 24h after 5-min startup delay | Calls `usp_AutoEscalateAll` (US50) — escalates all `In Progress` complaints stale > 30 days |
| `AIPendingQueueProcessor` | Every 5 min | Polls `AIPendingScoreQueue`; retries AI scoring for complaints submitted while AI was offline. Gives up after 5 failed attempts per complaint. *(Phase-2 update: raw ADO path now pins `SESSION_CONTEXT('UserRole')` to `'SuperAdmin'` so a future RLS re-enable doesn't zero the queue.)* |
| `WeeklyDigestService` | Every 7 days after 10-min startup delay | Calls `usp_GenerateWeeklyDigest` (US65) — inserts a digest notification per user with active subscriptions and recent complaints in their locality. **Added in Phase 2 (2026-05-19).** |

All three run as `IHostedService` and start automatically with the API.

---

## 4. Frontend (Angular 15)

### Configuration

`FixMyCityApp/src/environments/environment.ts` points at the API. The default is correct for local dev:
```ts
{ production: false, apiBaseUrl: 'http://localhost:5065' }
```

### Run

```powershell
cd FixMyCityApp
npm install
npx ng serve --port 4200
```

Open `http://localhost:4200`. The login page is at `/login`; the landing page at `/home` redirects logged-in users to their role dashboard via `AuthGuard` + `RoleGuard`.

### Build verification

```powershell
cd FixMyCityApp
npx ng build --configuration development
```

Expected: `0 Error(s)`. Bundle size ~3.6 MB (dev). Production build (`ng build`) uses tree-shaking and ahead-of-time compilation — same output, much smaller.

### Token & session storage

- Access token: `sessionStorage['fmc_access']` — cleared when the tab closes (intentional XSS-mitigation)
- Refresh token: `localStorage['fmc_refresh']` — persists across tabs so silent refresh works
- User profile: `sessionStorage['fmc_user']` — flat copy of the JWT claims

If a user logs in via one tab, the access token does **not** persist to other tabs by design. Each new tab has to refresh against the stored refresh token.

### Geolocation (Phase 4 update)

The submit-complaint form supports **two** GPS sources, in priority order:

1. **EXIF GPS from uploaded photo** — auto-extracted by the AI service's `/ai/analyze-image` (`_extract_gps` in `routers/categorization.py`). No user action beyond uploading the photo.
2. **Browser geolocation** — manual: click "Use my current location" under the Address field. Calls `navigator.geolocation.getCurrentPosition` and stashes lat/lon on the form. Browser will prompt the user for permission once; rejection / timeout fail gracefully with an informational toast.

Both paths write the same `gpsLat` / `gpsLon` fields on the form, so the submit payload is identical regardless of how coordinates were captured.

### 404 surface (Phase 4 update)

Unknown URLs render `NotFoundComponent` inside the public layout (previously redirected silently to `/home`). The "Take me home" button is role-aware:

- Unauthenticated → `/home`
- Citizen → `/citizen/home`
- Solver → `/solver/dashboard`
- PWG → `/pwg/complaints`
- SuperAdmin → `/admin/dashboard`

---

## 5. AI service (FastAPI)

### Configuration (env vars)

| Variable | Default | Notes |
|----------|---------|-------|
| `AI_SERVICE_KEY` | `fixmycity-ai-internal-key-change-me` | Must match `AIService:ServiceKey` in the API's `appsettings.json` |
| `DOTNET_API_KEY` | same as above | Used when AI calls back into `/api/ML/*` |
| `DOTNET_API_BASE` | `http://localhost:5065` | API URL the AI service POSTs results to |
| `DB_CONN_STR` | `DRIVER={ODBC Driver 17 for SQL Server};SERVER=.;DATABASE=FixMyCityDB;Trusted_Connection=yes;` | Read-only access to seed AI training data + lookups. |
| `HF_API_TOKEN` | none | Free token from `https://huggingface.co/settings/tokens` — required for embeddings, CLIP, Mistral chat, and the optional HF toxicity layer. |
| `IMAGE_BASE_PATH` | `<repo>/FixMyCityUploads` *(post Phase 3)* | Where uploaded complaint photos live. **Must point at the same directory as `Uploads:BasePath` in `appsettings.json`** or `/ai/analyze-image` won't find them. |
| `ALLOWED_ORIGINS` | `http://localhost:5065,http://localhost:4200` | CORS allow-list |
| `USE_ML_TOXICITY` | `false` | When true, layers HF `toxic-bert` on top of the always-on profanity filter. |
| `TOXICITY_THRESHOLD` | `0.85` | HF score threshold above which a complaint is blocked. |
| `DUPLICATE_THRESHOLD` | `0.82` | Cosine similarity above which a complaint is flagged as a duplicate. |
| `MODEL_DIR` | `models` | Where trained LightGBM / KNN / ALS / `category_label_encoder` / `category_name_to_id` are persisted. |

### Run (HF API mode — recommended)

```bash
cd FixMyCity.AI/ml_service
python -m venv .venv && .venv\Scripts\activate    # Windows
# or: source .venv/bin/activate                   # macOS / Linux
pip install -r requirements_hf.txt                # ~80 MB total, no torch/transformers
cp .env.example .env                              # then edit HF_API_TOKEN
uvicorn main:app --host 0.0.0.0 --port 8001 --reload
```

(Windows users can double-click `setup_ml_service.bat` instead — Phase 3 added `python-dotenv` + `huggingface_hub` to it.)

A successful startup banner now reads:

```
Startup complete. Available: sentence_model=True, clip=False, keybert=True
```

`clip=False` is normal — CLIP is lazy-loaded; `POST /ai/load-clip` flips it to `True` and is the only call needed before the first `/ai/analyze-image` request.

### Run (full local-inference mode — heavier, optional)

If you need offline operation or want to avoid HF rate limits, use the larger `requirements.txt` which pulls torch + transformers + sentence-transformers (~4 GB). The code paths fall back to local inference automatically when the libraries are installed.

```bash
cd FixMyCity.AI/ml_service
pip install -r requirements.txt
pip install torch --index-url https://download.pytorch.org/whl/cpu   # CPU build
uvicorn main:app --host 0.0.0.0 --port 8001 --reload
```

Health check (always): `curl http://localhost:8001/health` → `{"status":"ok", "sentence_model":true, ...}`.

### .env example

Create `FixMyCity.AI/ml_service/.env` with at minimum:

```
AI_SERVICE_KEY=fixmycity-ai-internal-key-change-me
DOTNET_API_KEY=fixmycity-ai-internal-key-change-me
DOTNET_API_BASE=http://localhost:5065
HF_API_TOKEN=hf_REPLACE_ME
ALLOWED_ORIGINS=http://localhost:5065,http://localhost:4200
```

If you skip `HF_API_TOKEN`, every HF-backed endpoint (embeddings, CLIP, chat, toxicity layer 3) returns empty results gracefully — but the rule-based fallbacks still work.

### Graceful degradation

- The .NET API uses `Polly` to retry transient HTTP failures 3× with exponential backoff (2 / 4 / 8 s).
- If the AI service is unreachable when a complaint is submitted, the complaint is **still saved** and is added to `AIPendingScoreQueue`. Once the AI service comes back online, `AIPendingQueueProcessor` will retry scoring within 5 minutes.
- Toxicity check is **fail-open**: if the AI service is unreachable, the complaint goes through. This is a civic platform — better to accept a possibly-noisy complaint than to refuse a legitimate one because of an AI outage.

---

## 6. Verified working flows (end-to-end)

| Flow | Touchpoints |
|------|-------------|
| **SuperAdmin login** | `POST /api/Auth/Login` → JWT pair issued → redirect `/admin/dashboard` |
| **Citizen submits complaint** | Toxicity check (sync, fail-open) → `usp_SubmitComplaint` routes to Dept by (Category, Locality) → AI scoring fires async → notification to assigned Solver + Citizen |
| **Solver updates status** | `usp_UpdateComplaintStatus` validates against `ComplaintStatusTransitions`; mandatory remark on Reject; resolution photo persisted to `ComplaintAttachments` |
| **Citizen rates resolved complaint** | `usp_RateComplaint` → 1 point credited via `usp_AwardPoints` cascade → milestone certificates auto-issued |
| **Citizen reopens (with rating <3)** | `usp_ReopenComplaint` enforces the rating guard at the DB layer |
| **Auto-escalation** | `AutoEscalationService` runs daily → `usp_AutoEscalateAll` finds stale `In Progress` complaints → delegates to `usp_FileEscalation` per row |
| **Manual reassignment** | `POST /api/Admin/ManualEscalation` → `usp_FileEscalation` with `EscalationTrigger='Manual'`, logs to `AuditLog` |
| **PWG submits participation request** | `usp_SubmitPWGRequest` → Solver gets `PWGDecision` notification |
| **Solver approves/rejects PWG request** | `usp_ResolvePWGRequest` → PWG gets notification |
| **PWG progress update** | `usp_PWGProgressUpdate` writes timeline + attachment + +2 points |
| **Citizen contributes funds** | `usp_CreateContribution` (idempotent on `TransactionRef`, `UPDLOCK`+`ROWLOCK`); gateway callback runs `usp_UpdatePaymentStatus` |
| **Token refresh on expiry** | Angular interceptor catches 401 with `Token-Expired: true` header, calls `/api/Auth/RefreshToken`, retries original request transparently |
| **PDF download** (complaint / certificate / PWG report) | `ReportController` → `QuestPdfService` (Community license) → streamed direct, no temp files |
| **Weekly digest** | `usp_GenerateWeeklyDigest` (manual trigger via `POST /api/Gamification/GenerateWeeklyDigest` — see Known limitations) |

---

## 7. Architecture notes

- **DB-first repository pattern.** Repositories call stored procedures via `ExecuteSqlRaw` for writes; reads use EF Core LINQ with explicit `Include`s. No EF migrations; the SQL files in `Database/` are the canonical schema.
- **All business invariants live in the DB layer** (CHECK constraints, `ComplaintStatusTransitions` reference table, SP-side guards like "rejection requires a remark"). Controllers are thin pass-throughs.
- **Row-Level Security** policy `ComplaintRLS` is defined but currently disabled (`STATE = OFF`). The interceptor (`SessionContextInterceptor`) populates `SESSION_CONTEXT` from JWT claims via `JwtSessionContextMiddleware`, so RLS can be re-enabled by issuing `ALTER SECURITY POLICY dbo.ComplaintRLS WITH (STATE = ON)` — but see Known limitation #2.
- **JWT auth.** 15-min access token (HS256), 7-day refresh token rotated on every refresh. Refresh tokens stored as SHA-256 hashes in `dbo.UserRefreshTokens`.
- **API key for AI ↔ .NET callbacks.** `AIServiceKeyMiddleware` runs only on `/api/ML/*` write-back paths and uses constant-time comparison. Frontend-only ML endpoints (`CategorizeText`, `AnalyzeImage`, `Chat`, etc.) are whitelisted and rely on JWT instead.
- **Rate limiting.** Login is capped at 10 attempts/min/IP; global at 300 req/min/IP.

---

## 8. Frontend ↔ backend integration notes

| Concern | Detail |
|---------|--------|
| **JSON casing** | API serializes with `JsonNamingPolicy.CamelCase`. Angular interfaces use camelCase. |
| **Login response shape** | `{ success, accessToken, refreshToken, expiresIn, user: { userId, fullName, email, roleId, roleName, localityId, deptId? } }`. The legacy flat fields in `ILoginResponse` are **not** populated by the API — components must read `res.user.*`. `SessionService.saveSession` flattens these into one persistent object. |
| **Status badge** | Status strings are exact: `'Submitted' | 'In Progress' | 'Resolved' | 'Rejected' | 'Re-opened' | 'Escalated' | 'Linked'` (note the **hyphen** in `Re-opened` and the **space** in `In Progress`). |
| **Date formats** | API returns ISO 8601 strings; Angular parses with `Date` constructor. `EstimatedResDate` is a `date` (not `datetime`) — already a calendar date. |
| **Token expiry** | API returns `Token-Expired: true` header on 401 if and only if the JWT itself has expired. Interceptor uses this to distinguish "refresh needed" from "credentials revoked". |
| **CORS** | When deploying to a different origin, add it to `AllowedOrigins` in `appsettings.json` *and* `ALLOWED_ORIGINS` env var on the AI service. |

---

## 9. Deployment notes

- **Schema deploys:** ship the five files in `Database/` and run them in order. `00_Schema_Sprint2.sql` is destructive; for *upgrades* you'd run only `04_DB_Patch.sql` (idempotent) and any subsequent additions.
- **App config:** never deploy the dev `Jwt:Secret` or `AIService:ServiceKey`. Generate fresh values per environment.
- **AI service:** the first call to any HF endpoint can take ~10 s (cold start). Subsequent calls are fast. Production should run a warm-up health hit on deploy.
- **AI key parity:** the API's `AIService:ServiceKey` and the AI service's `AI_SERVICE_KEY` must be byte-identical. `DOTNET_API_KEY` (used by the AI service's callbacks into the API) must match the same value too.
- **HTTPS:** `Program.cs` enables HSTS in non-Development environments. The Angular app must be served over HTTPS in production to avoid mixed-content warnings on the API call.
- **QuestPDF licensing:** Community license is set at startup (`QuestPDF.Settings.License = LicenseType.Community`). For commercial use you must upgrade and update this line.

---

## 10. Known limitations (post-audit)

> For each item below, the priority bucket and exact remediation path is in [SYSTEM_FIX_PLAN.md](SYSTEM_FIX_PLAN.md).

1. ~~**Weekly digest is not scheduled.**~~ — **Closed in Phase 2 (2026-05-19).** `WeeklyDigestService` background service now runs every 7 days, fires `usp_GenerateWeeklyDigest`. Pattern follows `AutoEscalationService`. *(Fix Plan P2-2)*
2. **RLS is disabled.** Re-enabling `dbo.ComplaintRLS` is now safe with respect to `AIPendingQueueProcessor` — its raw-ADO read path now calls `sp_set_session_context N'UserRole', N'SuperAdmin'` before the SELECT (Phase-2 fix). Flipping `STATE = ON` itself is still deferred to a dedicated phase so the cutover can be tested end-to-end. *(Fix Plan P2-1; OI-1 blocker P1-2 closed)*
3. **`localityName` is missing from the flat user-profile response** for older code paths. `GetUserById` now projects a flat shape (Issue #20) but other reads still return the EF entity with `user.locality.localityName`. *(Fix Plan P2-4)*
4. ~~**`PredictionModelVersion` length cap in EF (20) is narrower than DB (50).**~~ — **Closed in Phase 2 (2026-05-19).** EF widened to 50 to match the DB column. *(Fix Plan P2-3)*
5. **First HF cold start ~10s.** Toxicity, embeddings, and chat first calls are slow. The retry policy (Polly) handles this for non-blocking endpoints; the synchronous toxicity check fails open after timeout.
6. **CLIP model is lazy-loaded.** `POST /ai/load-clip` must be called once before the first image classification (it adds ~350 MB RAM). The `analyze-image` endpoint will return an error before that.
7. **OCR (pytesseract) is optional.** If Tesseract isn't on PATH, OCR silently returns null; complaint submission still succeeds.
8. ~~**EF DbContext lacks DbSets for 6 AI tables + `UserRefreshTokens`.**~~ — **Closed in Phase 2 (2026-05-19).** All 7 entities + `DbSet<T>` + fluent configurations added (read-only via EF; writes still go through the named stored procedures). *(Fix Plan P1-1)*
9. ~~**`MLServiceClient.CategorySuggestion` DTO is missing `CategoryId`.**~~ — **Closed in Phase 2 (2026-05-19).** Field added; deserializer uses `SnakeCaseLower` so Python's `category_id` maps automatically. *(Fix Plan P2-7)*
10. ~~**`/ai/geo-cluster` interpolates `locality_id` into SQL.**~~ — **Closed in Phase 3 (2026-05-19).** Uses parameter binding now.
11. ~~**AI service category label-encoder shape mismatch.**~~ — **Closed in Phase 3 (2026-05-19).** `ModelStore` gained `category_label_encoder` + `category_name_to_id`; training persists them; categorization reads them via `predict_proba` + DB-backed name→id mapping.
12. ~~**`01_AI_Tables_Addition.sql` is not rerun-safe**~~ — **Closed in Phase 1 (2026-05-19).**
13. ~~**Eight repository methods swallow `SqlException` with no logging.**~~ — **Closed in Phase 2 (2026-05-19).** All 37 silent catches across 7 repository files now log via `ILogger<T>`.
14. ~~**`IMAGE_BASE_PATH` defaults to `C:/FixMyCityUploads`**~~ — **Closed in Phase 3 (2026-05-19).** Default is now `<repo>/FixMyCityUploads` resolved via `pathlib.Path` — cross-platform. Set the `Uploads:BasePath` key in `appsettings.json` (.NET) and `IMAGE_BASE_PATH` env var (Python) to the same directory.
15. **`appsettings.json` ships with real JWT secret + AI service key**, not placeholders. Fine for dev; **must** be replaced (env vars or KeyVault) before any prod deploy. *(Fix Plan P2-9)*
16. ~~**Angular `IGeoCluster` interface doesn't match the .NET DTO.**~~ — **Closed in Phase 4 (2026-05-19).** Interface aligned with the wire format; `map-view.renderClusters()` rewritten.
17. ~~**No browser geolocation in the submit form.**~~ — **Closed in Phase 4 (2026-05-19).** "Use my current location" button in submit-complaint calls `navigator.geolocation.getCurrentPosition`; integrates with the same form-stash that the EXIF path uses.
18. **Push and Email notification channels are not implemented.** `NotificationPreferences.PushEnabled` / `EmailDigestEnabled` are stored but never read by a sender. Only the `InApp` channel produces rows today. The `WeeklyDigestService` writes `Channel='Email'` notification rows but no SMTP backend dispatches them.

22. **Razorpay** — closed in Phase 5 (2026-05-19). Server-orchestrated Standard Checkout: `POST /api/Payment/CreateRazorpayOrder` → modal → `POST /api/Payment/VerifyRazorpayPayment`. HMAC-SHA256 signature verified server-side with constant-time comparison. Test keys `rzp_test_SrIcMAOjaHklls` ship in `appsettings.json` for local dev; override via `Razorpay__KeyId` / `Razorpay__KeySecret` env vars for production. Demo bypass preserved (returns `demoMode: true` from `CreateRazorpayOrder` when keys are absent).
23. **Dark mode** — implemented in Phase 5 (2026-05-19). `ThemeService` + `data-theme="dark"` on `<html>` + `[data-theme="dark"]` token overrides in `styles.css`. Toggle in every navbar; persists in `localStorage['fmc_theme']`; respects `prefers-color-scheme` on first visit. Pre-paint bootstrap script in `index.html` prevents the light-theme flash for returning dark-mode users.
24. **Phase-6 approved frontend upgrades** — implemented 2026-05-20. Bootstrap Icons migration (every `fa fa-*` → `bi bi-*`, FA kept as a fallback), three shared directives (`fmcReveal`, `fmcTilt`, `fmcRipple`), skeleton-card placeholder, route transitions via `@angular/animations`, glassmorphism navbar on scroll, branded form-input focus glow, toast slide-in/out, timeline stagger, complaint-card icon chips, scoreboard rank-1 pulse, chatbot FAB pulse, upgraded empty-state visuals. Landing page gets animated stat counters, mouse-spotlight hero, particle canvas background, and typing-animation headline. All animations honour `prefers-reduced-motion`. **Photo upload moved to the TOP of the Submit-Complaint form with drag-and-drop, preview, and clean validation UX.** See AUDIT.md → Phase 6 section for the full file list.

25. **Phase-7 runtime stability fixes** — applied 2026-05-20.
    a. `NG04012: Outlet is not activated` — closed. `AppComponent.prepareRoute` now guards on `outlet.isActivated` instead of relying on optional chaining around throwing getters. The animation trigger handles `'empty' → '<route-key>'` first-activation via its existing `* <=> *` pattern.
    b. `checkout.js: Unrecognized feature: 'otp-credentials'` — confined to the contribution flow. The Razorpay SDK is now lazy-loaded by `PaymentService.ensureRazorpaySdk()` on the first call to `contributeViaRazorpay()` instead of via a static `<script>` tag in `index.html`. The warning is a quirk of Razorpay's compiled SDK; we cannot suppress it directly, but it no longer fires on every route.
    c. `POST .../lumberjack.razorpay.com/v2/logz ERR_BLOCKED_BY_CLIENT` — **harmless**. This is Razorpay's analytics endpoint being blocked by the user's ad blocker. The platform's authoritative payment record is the `/api/Payment/VerifyRazorpayPayment` server-side endpoint, which is independent of any client-side telemetry. Contributions complete and persist correctly even when telemetry is blocked. Documented for future operators so they don't chase a non-bug.

   See AUDIT.md → "Phase 7: Post-upgrade runtime stability pass" for full root-cause analysis.
19. **No 404 page** — ~~closed in Phase 4 (2026-05-19).~~ `NotFoundComponent` renders for any unknown URL with a role-aware "Take me home" button.
20. **`console.error` lives in each service's `handleError`.** Acceptable for dev; for prod, route through an `ILoggerService` that can be muted at build time. *(Phase 5+ — ergonomics)*
21. **~105 `any` / `!` non-null assertions in TS** — flagged in Phase 0; none observed to cause runtime failures. Tech debt only.

---

## 10b. External integrations — setup checklist

A consolidated map of every external surface, the file that wires it, and what you need to do to enable it.

| Integration | Where | Default behaviour | To enable in production |
|-------------|-------|-------------------|-------------------------|
| **Hugging Face Inference API** | `ml_service/services/hf_inference.py` (token via `HF_API_TOKEN`) | Without a token, every HF-backed call returns empty; rule-based fallbacks fire so the platform stays functional. | Create a free token at `https://huggingface.co/settings/tokens`, paste into `ml_service/.env` as `HF_API_TOKEN=hf_xxxx`, restart uvicorn. The startup banner should read `sentence_model=True, keybert=True`. |
| **Razorpay** | `FixMyCityApp/src/app/fmc-services/payment.service.ts:48` (key) + `index.html:31` (SDK CDN) | Placeholder key triggers demo bypass — synthesises a `DEMO_…` transactionRef so the contribution flow runs end-to-end without provisioning a real account. | Replace `RAZORPAY_KEY` with a real `rzp_test_…` (sandbox) or `rzp_live_…` (production) key. Make sure your account is enabled for the INR currency and webhook receivers if you want server-side verification. Update `payment.service.ts` or move the key into `environment.prod.ts`. |
| **Leaflet** | `index.html:17-25` (CSS + JS via unpkg CDN) + `shared/components/map-view/map-view.component.ts` | Markers render; cluster overlay needs Phase-4 fix (see known limit 16). | No setup beyond an internet-connected client browser. Self-host the assets in `assets/leaflet/` and update `index.html` paths if you need to operate offline. |
| **OpenStreetMap tiles** | `map-view.component.ts:90` (`https://{s}.tile.openstreetmap.org/...`) | Public usage policy applies — fine for low traffic, attribution rendered. | For heavy traffic, swap to Mapbox / your own tile server: edit the URL + add API key. |
| **Tesseract OCR** | `ml_service/routers/categorization.py:179` (`pytesseract.image_to_string`) | Optional — returns `None` silently if the binary isn't on PATH. Complaint submit still succeeds. | macOS: `brew install tesseract`. Ubuntu: `apt install tesseract-ocr`. Windows: install from `https://github.com/UB-Mannheim/tesseract/wiki` and add to PATH. |
| **File uploads** | `.NET`: `ComplaintController.UploadComplaintImage` writing to `Uploads:BasePath` (`appsettings.json`); `Python`: reads from `IMAGE_BASE_PATH` (`config.py`). | Both default to `<repo>/FixMyCityUploads`. 10 MB cap; JPG/PNG/WEBP only; flat directory layout. | Both paths must resolve to the **same physical directory** (or shared volume in containers). If you deploy the API and AI service on different hosts, mount a shared filesystem (NFS, S3-fuse, Azure Files) and point both env vars at it. |
| **JWT signing** | `FixMyCity.API/Program.cs:50` + `appsettings.json:Jwt:Secret` | Default dev secret committed to the repo — works out of the box but is **NOT** secret. | Generate per environment: `openssl rand -base64 32` and set via env var `Jwt__Secret` or `dotnet user-secrets`. |
| **AI ↔ .NET shared secret** | `appsettings.json:AIService:ServiceKey` + `ml_service/.env:AI_SERVICE_KEY` | Same dev default on both sides — works out of the box. | Generate once, set on **both** sides via env vars `AIService__ServiceKey` (.NET) and `AI_SERVICE_KEY` + `DOTNET_API_KEY` (Python). |
| **SQL Server** | `FixMyCity.API/appsettings.json:ConnectionStrings:DefaultConnection` + `ml_service/config.py:DB_CONN_STR` | Both default to LocalDB / trusted local connection. | For production, set both to the same SQL Server instance with SQL-auth credentials in env vars, never in source files. |
| **Push / SMS / Email** | not implemented | — | See known limit 18. |

---

## 11. Troubleshooting

| Symptom | Probable cause | Fix |
|---------|----------------|-----|
| Login returns "Invalid credentials" with seeded users | Old (UTF-16) seed still in DB | Re-run `03_SeedData.sql` after the fix in Issue #1 |
| Login succeeds, then dashboard 401s redirect you back to `/login` | `UseHttpsRedirection` redirected http→https and stripped the JWT | Pull the Program.cs fix (Issue #11) and restart the API. Or run the `http` profile only: `dotnet run --launch-profile http` |
| Every list endpoint (`/api/Admin/GetAllComplaints`, `/api/Complaint/GetComplaintsByDept`, `/api/PWG/GetOpenComplaints`) returns 500 with `JsonException` | EF Core navigation fixup created cycles that defeated `IgnoreCycles` at `MaxDepth=64` | Issue #12 fix — `NoTracking` is now the DbContext default and `MaxDepth=128`. Restart the API after pulling. |
| Complaint timeline shows `By: Name ()` with empty parens | `GetTimeline` didn't load `Actor.Role`; template used `actor.roleName` instead of `actor.role.roleName` | Issue #13 fix is in `ComplaintRepository.GetTimeline` and `timeline.component.html` |
| Certificates always show "PDF pending" / no Download button | UI gated the download on the (intentionally null) `cert.filePath` instead of calling `/api/Report/CertificatePdf` | Issue #14 fix wires the button to a new `downloadCertificatePdf` blob fetch on `GamificationService` |
| Razorpay modal opens then 401s on `api.razorpay.com/...preferences?key_id=rzp_test_REPLACE_WITH_YOUR_KEY` | Default Razorpay key in `payment.service.ts` is a placeholder | Either drop your real `rzp_test_…` key into `PaymentService.RAZORPAY_KEY`, or rely on the Issue #15 demo bypass that synthesises a transactionRef and runs the rest of the flow |
| Python service log shows `POST /ai/recommend HTTP/1.1" 401 Unauthorized` for every call | `AIService:ServiceKey` (.NET) and `AI_SERVICE_KEY` (Python) used different default values | Issue #16 fix — both sides now default to `fixmycity-ai-internal-key-change-me`. For production, generate `openssl rand -base64 32` and set the same value on both sides (env or appsettings) |
| Categorize-text returns nothing useful — always the same suggestions regardless of input | Phase-3 fix: KNN inference used neighbour-row index as class index | Pull the Phase-3 changes to `routers/categorization.py` (now uses `predict_proba` + `knn.classes_`). Re-run `/ai/train` so `category_label_encoder` + `category_name_to_id` are saved. |
| Submitted complaint suggestions arrive in Angular with `categoryId: 0` | DTO mismatch — closed in Phase 2 | Verify `MLServiceClient.CategorySuggestion` has `CategoryId`. If still 0, hit `/ai/train` once so `category_name_to_id` is populated; until then `_resolve_category_id` falls back to a stable hash. |
| `/ai/analyze-image` returns `Image not found: C:/FixMyCityUploads/foo.jpg` on Linux | `IMAGE_BASE_PATH` had a Windows-only default (closed in Phase 3). | Pull the Phase-3 changes. Verify `<repo>/FixMyCityUploads/` exists and is writable by the API user. Both `Uploads:BasePath` and `IMAGE_BASE_PATH` must point at the same directory. |
| HF toxicity layer never blocks anything despite obviously-toxic text | `_check_toxicity` in `categorization.py` was reading the HF list response as a dict — silent AttributeError. | Pull Phase-3 fix. Set `USE_ML_TOXICITY=true` in `.env` and ensure `HF_API_TOKEN` is set. |
| `/ai/geo-cluster` returns no clusters for a specific locality | Either no complaints have GPS in that locality, or DBSCAN didn't find enough neighbours. | Check the seed (Phase-1 massive seed has 170 complaints with GPS spread across 16 localities). Lower `min_samples` to 2 if you have very few points. |
| Submit Complaint shows generic "Invalid request. Please check your input." with no detail | Interceptor wasn't reading the backend's `errors` array | Issue #17 fix — interceptor now joins `error.error.errors` and shows the real validation messages |
| Citizen sign-up says "Registration successful!" but the user can't log in / no row in `dbo.Users` | Repo returned 0 on a CHECK-constraint fail; controller still reported `success: true` | Issue #18 fix — Register* actions now return `success: false` with a specific hint when the SP rejects |
| Phone / Aadhaar validators were too lax — anything passed | Single `Validators.required` on both fields | Issue #19 fix — `Validators.pattern(/^[0-9]{10,15}$/)` on phones, `/^[0-9]{12}$/` on Aadhaar, per-error messages |
| Citizen profile shows blank Role | `GetUserById` returned the EF entity (nested `role.roleName`) but UI reads flat `roleName` | Issue #20 fix — controller projects to a flat shape with `roleName`, `localityName`, `points`, etc. |
| Org sign-up with Welfare Group / Community Association fails silently; CSR isn't even in the dropdown | Frontend list and DB `chk_Organisations_OrgType` were out of sync | Issue #21 fix — dropdown lists the full union (incl. CSR); `04_DB_Patch.sql` re-creates the CHECK to match. **Re-run `04_DB_Patch.sql`** to apply (idempotent) |
| Photo upload / AI image categorisation / GPS prefill / AI-drafted description on Submit Complaint | Features didn't exist in the UI; analysis endpoint returned only categories + OCR | Issue #22 — `POST /api/Complaint/UploadComplaintImage`, extended `analyze-image` (EXIF GPS + suggested description), photo dropzone + AI pipeline in the form, "Use AI description / Regenerate" actions |
| No way to sign in / sign up with Google | SSO endpoint existed but no UI surface | Issue #23 — demo SSO button on login + register-citizen pages. Replace the prompt with a real Google OAuth flow in production |
| PWG user logs in, opens **Open Complaints**, clicks **Request Participation** — submission silently fails | Login response omitted `orgId`; frontend posted `orgId: 0` and the FK check on `PWGParticipationRequests` rejected it | Issue #24 fix — Login + RefreshToken now return `orgId` for PWG users; re-login (or refresh) once the API is rebuilt so the new value is in your session |
| Python AI service starts with `sentence_model=False, clip=False, keybert=False` and every `/ai/*` call returns empty results — even though `HF_API_TOKEN=hf_xxx` is in `ml_service/.env` | `python-dotenv` wasn't installed and never invoked; the token was read at import time before any dotenv hook | Issue #25 fix — run `pip install -r requirements.txt` to get `python-dotenv`, then restart uvicorn. The startup log should now show `sentence_model=True, keybert=True` |
| `/solver/profile` and `/pwg/profile` looked nothing like `/admin/profile` or `/citizen/profile` | Solver and PWG had bespoke profile pages | Issue #26 — Solver/PWG profile pages now embed the shared `<app-user-profile>` for personal info + password, with their Dept/Org-specific section below in a consistent card pattern |
| Login succeeds in network tab but page bounces back to `/login` on every fresh login | Browser cached an old `main.js` | Hard reload (Ctrl+Shift+R) — Issue #2 fix is in `login.component.ts` |
| Every API call returns 401 after 15 minutes | Token-refresh path was previously broken | Issue #4 is fixed; if you still see this, verify `app.module.ts` reads `useExisting: AuthInterceptor` |
| Solver sees 403 on `/api/PWG/GetPendingRequestsForSolver` | Old controller attribute cached | Issue #5 is fixed; restart the API after redeploy |
| `Could not find stored procedure 'dbo.usp_AutoEscalateAll'` in logs | `04_DB_Patch.sql` not run | Run it now (idempotent) — Issue #3 |
| `POST /api/Payment/CreateContribution` → 500 | `usp_CreateContribution` missing | Run `04_DB_Patch.sql` |
| AI service offline → complaint submit times out | Toxicity check timeout | Already fail-open in code; if you see hangs, check `HttpClient.Timeout` (default 15 s) |
| `/api/ML/*` callbacks return 401 | API/AI key mismatch | Ensure `AIService:ServiceKey` (API) and `AI_SERVICE_KEY` (Python) match exactly, byte-for-byte |
| AI scores never appear on submitted complaints | Either AI service down at submit, or background processor not reaching it | Check `AIPendingScoreQueue` for backlog; check `/api/ML/CheckAIHealth` |
| `usp_RefreshScoreboard` fails on first run | UserPoints empty | Submit at least one complaint and rate it; or run with `WHERE EXISTS` guard |
| `usp_GenerateWeeklyDigest` returns 0 rows | No active users with `EmailDigestEnabled` and recent complaints | This is correct behavior — verify users have preferences and there are recent open complaints in their locality |

---

## 11b. Phase 8 — Feature suggestion wave (2026-05-20)

Twenty civic-tech features from `fixmycity-feature-suggestions.md`. Every item
is wired end-to-end (DB → DAL → API → Angular) unless explicitly noted as
frontend-only.

| §  | Feature                          | Surface area                                                                                    |
|----|----------------------------------|-------------------------------------------------------------------------------------------------|
| 1  | Upvote complaints                | `ComplaintUpvotes` table, `usp_ToggleComplaintUpvote`, `ToggleUpvote`/`GetUpvoteState`, `<app-upvote-button>` on every complaint card + detail page |
| 2  | Save complaint as draft          | `ComplaintDraftService` (localStorage, 7-day TTL), restore-draft banner on `/citizen/submit`    |
| 3  | Before / after photo viewer      | `<app-photo-compare>` (draggable swipe handle) shown on resolved complaints when both photos exist |
| 4  | Share button                     | `shareOrCopy()` util (`navigator.share` + clipboard fallback) on complaint detail               |
| 5  | "Issues near me" geolocation     | `usp_GetNearbyComplaints` (Haversine SQL), `<app-nearby-complaints>` on citizen home            |
| 6  | Appeal a rejected complaint      | `ComplaintAppeals` table, `usp_SubmitComplaintAppeal` / `usp_ResolveComplaintAppeal`, appeal panel on citizen complaint detail (Rejected only), `/admin/appeals` queue page |
| 7  | Comments thread                  | `ComplaintComments` table, `usp_AddComplaintComment`, `<app-comments-thread>` on every complaint detail; Solver/Admin replies auto-marked `IsOfficialReply` |
| 8  | QR code download                 | `qrUrl()` / `downloadQr()` utils (QR Server API), inline "Show QR" panel on detail page         |
| 9  | Trend analytics chart            | `usp_GetComplaintTrend`, `<app-trend-chart>` (pure SVG, no Chart.js) on admin dashboard; 7d/30d/90d toggle |
| 10 | CSV export                       | `toCsv()` + `exportCsv()` utils (UTF-8 BOM, RFC-4180 quoting), Export button on `/citizen/complaints` |
| 11 | Admin bulk-status update         | `usp_BulkUpdateComplaintStatus` (CSV ids + transition gate), `BulkUpdateStatus` endpoint on `AdminController` |
| 12 | Manual department reassignment   | `usp_ReassignComplaintDept` (writes EscalationLog + AuditLog), `ReassignDept` endpoint           |
| 13 | Locality heatmap                 | `[heatmap]` Input on `<app-map-view>` — overlapping low-opacity Leaflet circles, no extra library |
| 14 | SLA pipe / countdown chip        | `SlaPipe` + `SlaBadgeClassPipe`, applied to complaint-card + detail header                       |
| 15 | Internal notes (staff only)      | `ComplaintInternalNotes` table, `usp_AddInternalNote`, `<app-internal-notes>` (silently hides for non-staff) on every complaint detail page |
| 16 | Solver bulk update               | Re-uses §11 endpoint; surfaced on solver list (planned UI affordance — endpoint ready)           |
| 17 | Public transparency portal       | `PublicController` (`[AllowAnonymous]`), `usp_GetPublicFeed`, `/transparency` page with locality/category/status/keyword filters + pagination |
| 18 | Forgot / Reset password          | `usp_RequestPasswordReset` + `usp_ResetPassword` (already in schema), new `ForgotPassword` / `VerifyResetToken` / `ResetPassword` endpoints, Angular `forgot-password` + `reset-password` routes; dev mode echoes the token when `Email:Enabled = false` |
| 19 | PWA install + offline shell      | `manifest.webmanifest`, `fmc-sw.js` (network-first shell cache, never caches `/api/*`), `PwaService.init()` from `AppComponent` |
| 20 | Personal activity feed           | `usp_GetActivityFeed` (UNION across complaints / status / points / certificates / comments), `<app-activity-feed>` tab on citizen home |

**New files added (Phase 8, 2026-05-20):**

- `Database/06_FeatureSuggestions.sql` — 4 tables + 11 SPs, idempotent
- `FixMyCity.DAL/Models/Complaint{Upvote,Comment,Appeal,InternalNote}.cs`
- `FixMyCity.DAL/DTOs/FeatureSuggestionDtos.cs` (keyless projections for trend / activity feed / public feed / nearby)
- `FixMyCity.DAL/Repositories/{Interfaces,Implementations}/IFeatureRepository.cs` + `FeatureRepository.cs`
- `FixMyCity.API/Models/FeatureRequests.cs`
- `FixMyCity.API/Controllers/PublicController.cs` (anonymous transparency portal)
- `FixMyCity.API/Controllers/UserController.cs` (activity feed)
- Existing controllers extended: `ComplaintController` (+8 endpoints), `AdminController` (+5 endpoints), `AuthController` (+3 endpoints)
- `FixMyCityApp/src/app/fmc-services/{public,user}.service.ts`
- `FixMyCityApp/src/app/shared/pipes/sla.pipe.ts`
- `FixMyCityApp/src/app/shared/utils/{complaint-draft,export,share,qr}.{service,util}.ts`
- `FixMyCityApp/src/app/shared/components/{photo-compare,nearby-complaints,upvote-button,comments-thread,internal-notes,activity-feed,trend-chart}/`
- `FixMyCityApp/src/app/public/transparency/`
- `FixMyCityApp/src/app/admin/appeals/`
- `FixMyCityApp/src/app/auth/{forgot-password,reset-password}/`
- `FixMyCityApp/src/app/core/services/pwa.service.ts`
- `FixMyCityApp/src/manifest.webmanifest`, `src/fmc-sw.js`

**Apply the migration:**

```sh
sqlcmd -S localhost -d FixMyCityDB -i Database/06_FeatureSuggestions.sql
```

The script is idempotent (`IF NOT EXISTS` guards on all CREATE TABLE / INDEX
blocks; `CREATE OR ALTER PROCEDURE` for the SPs).

**Auth interceptor change:** any HttpClient request can now opt out of the
`Authorization: Bearer` header by setting `X-Public: true`. The transparency
portal and the forgot/reset-password flows use this so anonymous calls don't
trigger a 401-refresh loop with stale tokens.

**PWA caveats:**
- `assets/icons/icon-192.png` / `icon-512.png` referenced from
  `manifest.webmanifest` are placeholders — drop in real PNGs before App Store
  / Play Store submission. Until then the browser will install with just the
  favicon (still works).
- Service worker is best-effort. Browsers that don't support it (rare today)
  fall through gracefully — the app just doesn't get offline shell caching.

---

## 12. Files of interest by concern

- **Auth & password hashing:** `FixMyCity.API/Controllers/AuthController.cs` (HashPassword, line 350), `FixMyCity.DAL/Repositories/Implementations/AuthRepository.cs` (ValidateLogin), `Database/03_SeedData.sql:152`
- **JWT lifecycle:** `FixMyCity.API/Services/JwtService.cs`, `FixMyCity.API/Program.cs` (JwtBearer config), `FixMyCity.API/Middleware/JwtSessionContextMiddleware.cs`
- **RLS & session context:** `FixMyCity.DAL/Infrastructure/SessionContextInterceptor.cs`, `Database/00_Schema_Sprint2.sql` (section 9)
- **Complaint lifecycle:** `Database/00_Schema_Sprint2.sql` (SP definitions: `usp_SubmitComplaint`, `usp_UpdateComplaintStatus`, `usp_ReopenComplaint`, `usp_FileEscalation`, `usp_LinkDuplicateComplaint`)
- **AI write-back:** `FixMyCity.API/Controllers/MLController.cs` (callback endpoints), `FixMyCity.API/Middleware/AIServiceKeyMiddleware.cs`, `FixMyCity.AI/ml_service/services/notifier.py`
- **AI feature endpoints (called by Angular):** Same `MLController.cs` (read endpoints), `FixMyCity.API/Services/MLServiceClient.cs`, FastAPI routers in `FixMyCity.AI/ml_service/routers/`

---

For per-issue root-cause analysis with reproduction and verification details, see **[AUDIT.md](AUDIT.md)**.
