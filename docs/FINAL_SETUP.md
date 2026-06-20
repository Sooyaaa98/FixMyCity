# FixMyCity — Final Setup Guide

**Date:** 2026-05-19 (post Phase-1..4 stabilization)
**Audience:** A fresh operator setting up the platform from scratch on Windows / macOS / Linux.

This is the single canonical setup doc. If anything here conflicts with `README.md`, this file wins — `README.md` predates the audit and was not rewritten in full. See [AUDIT.md](AUDIT.md) for the per-phase change log and [HANDOVER.md](HANDOVER.md) for ongoing operations / troubleshooting.

---

## 0. Prerequisites

| Tool | Version | Why |
|------|---------|-----|
| SQL Server | 2019+ or **LocalDB** (Express Edition includes LocalDB) | Database |
| .NET 8 SDK | 8.0+ | API + DAL |
| Python | **3.11.x** (3.12 also works) | AI microservice |
| Node.js | 18+ | Angular 15 frontend |
| Hugging Face account | free tier is fine | AI features (embeddings, CLIP, chat, toxicity). Token at `https://huggingface.co/settings/tokens` |
| Tesseract OCR (optional) | 5.x | Only if you want OCR on uploaded photos; everything else works without it. |
| Docker Desktop (optional) | — | Only if you want to containerise the AI service |

You do **not** need a Razorpay account for development — the frontend bundles a demo-bypass path so the contribution flow runs end-to-end with a synthetic transaction reference.

---

## 1. Repository layout (post Phase 1–4)

```
FixMyCity/
├── Database/                              ← SQL — run in numeric order
│   ├── 00_Schema_Sprint2.sql              ← schema + 36 SPs + RLS policy (destructive)
│   ├── 01_AI_Tables_Addition.sql          ← 7 AI tables + 5 SPs (idempotent)
│   ├── 02_UserRefreshTokens.sql           ← JWT refresh-token table (idempotent)
│   ├── 03_SeedData.sql                    ← 19 canonical users + 30 complaints
│   ├── 04_DB_Patch.sql                    ← usp_AutoEscalateAll + usp_CreateContribution + OrgType CHECK
│   └── 05_MassiveSeed.sql                 ← +81 users, +170 complaints, +everything (idempotent probe)
├── FixMyCity.DAL/                         ← EF Core data access (35 DbSets)
├── FixMyCity.API/                         ← .NET 8 Web API (JWT, Polly, QuestPDF, rate limiting)
│   └── Services/
│       ├── AutoEscalationService.cs       ← daily — US50
│       ├── AIPendingQueueProcessor.cs     ← every 5 min — AI retry
│       └── WeeklyDigestService.cs         ← weekly — US65 (Phase 2 added)
├── FixMyCity.AI/ml_service/               ← FastAPI AI microservice
│   ├── main.py                            ← loads dotenv BEFORE module imports
│   ├── config.py                          ← cross-platform IMAGE_BASE_PATH
│   └── routers/                           ← 7 routers
├── FixMyCityApp/                          ← Angular 15 frontend
├── FixMyCityUploads/                      ← shared file store (created on first upload)
├── AUDIT.md                               ← per-phase change log
├── HANDOVER.md                            ← operations + troubleshooting
├── SYSTEM_FIX_PLAN.md                     ← Phase-0 forensic plan
└── FINAL_SETUP.md                         ← this file
```

---

## 2. Step-by-step setup

### Step 1 — Database

Open SQL Server Management Studio (SSMS) or `sqlcmd` and run the six SQL files **in numeric order**:

```sql
-- 1. Schema + Sprint-2 seed + RLS policy + 36 SPs
--    DESTRUCTIVE: drops the DB if it already exists. Run only on first install
--    or full reset.
-- File: Database/00_Schema_Sprint2.sql

-- 2. AI tables + 5 AI SPs (idempotent — IF NOT EXISTS guards as of Phase 1)
-- File: Database/01_AI_Tables_Addition.sql

-- 3. UserRefreshTokens table (idempotent — IF NOT EXISTS guard)
-- File: Database/02_UserRefreshTokens.sql

-- 4. 19 canonical seed users + 30 complaints (idempotent — wipe + re-seed)
-- File: Database/03_SeedData.sql

-- 5. usp_AutoEscalateAll + usp_CreateContribution + OrgType CHECK union
--    (idempotent — CREATE OR ALTER + guarded constraint drop+add)
-- File: Database/04_DB_Patch.sql

-- 6. Phase-1 massive seed: +81 users, +170 complaints, +everything
--    (additive, idempotent probe on anita.bbmp2@fixmycity.in)
-- File: Database/05_MassiveSeed.sql
```

After step 4 you'll see `SEED DATA COMPLETE` print. After step 6 you'll see `MASSIVE SEED COMPLETE — Phase 1` print with cumulative totals.

> **Note.** Run order matters. `03_SeedData.sql` recreates only the seed rows; `04_DB_Patch.sql` patches the OrgType CHECK and adds 2 SPs. `05_MassiveSeed.sql` requires the OrgType union from `04` to insert PWG records of type `Welfare Group` and `Community Association` — so run them in this exact order.

#### To re-seed without dropping the schema

```
03_SeedData.sql  →  04_DB_Patch.sql  →  05_MassiveSeed.sql
```

(Skip 00–02 — those are schema-level.)

#### Verifying the seed

```sql
USE FixMyCityDB;
SELECT
  (SELECT COUNT(*) FROM dbo.Users)            AS users,           -- expect 100
  (SELECT COUNT(*) FROM dbo.Departments)      AS departments,     -- expect 10
  (SELECT COUNT(*) FROM dbo.Organisations)    AS organisations,   -- expect 12
  (SELECT COUNT(*) FROM dbo.Complaints)       AS complaints,      -- expect 200
  (SELECT COUNT(*) FROM dbo.PlatformStatsSnapshot) AS stats_days; -- expect 15
```

Admin password hash sanity check:

```sql
SELECT PasswordHash FROM dbo.Users WHERE Email = 'admin@fixmycity.in';
-- expect: a109e36947ad56de1dca1cc49f0ef8ac9ad9a7b1aa0df41fb3c4cb73c1ff01ea
-- (= SHA-256 of UTF-8 bytes of "Password123!")
```

### Step 2 — .NET API + DAL

`FixMyCity.API/appsettings.json` defaults work for local dev. The only knob that matters for first-run:

```jsonc
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=FixMyCityDB;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Jwt": {
    "Secret": "milfuZMkbQHCr0KuJ4MQfAS9Ms83BlKNnbVOPL8msro=",   // dev — generate fresh for prod
    "Issuer": "FixMyCityAPI",
    "Audience": "FixMyCityApp",
    "AccessTokenMinutes": "15",
    "RefreshTokenDays": "7"
  },
  "AIService": {
    "BaseUrl": "http://localhost:8001",
    "ServiceKey": "fixmycity-ai-internal-key-change-me"           // must match Python AI_SERVICE_KEY
  },
  "Uploads": {
    "BasePath": "../FixMyCityUploads"                              // shared with the Python service
  }
}
```

```powershell
cd FixMyCity.API
dotnet restore
dotnet run --launch-profile http
```

Listens on `http://localhost:5065`. Swagger UI at `http://localhost:5065/swagger`.

Three background services start automatically:

- `AutoEscalationService` — every 24 h
- `AIPendingQueueProcessor` — every 5 min
- `WeeklyDigestService` — every 7 days

### Step 3 — Python AI microservice

The codebase uses **Hugging Face Inference API mode** by default (no local model downloads). Set up:

```bash
cd FixMyCity.AI/ml_service

# Create a virtualenv (recommended)
python -m venv .venv
# Windows:
.venv\Scripts\activate
# macOS / Linux:
source .venv/bin/activate

# Install lean HF dependencies (~80 MB total, no torch / transformers)
pip install -r requirements_hf.txt
```

Create `FixMyCity.AI/ml_service/.env` with at minimum:

```env
AI_SERVICE_KEY=fixmycity-ai-internal-key-change-me
DOTNET_API_KEY=fixmycity-ai-internal-key-change-me
DOTNET_API_BASE=http://localhost:5065
HF_API_TOKEN=hf_REPLACE_WITH_YOUR_TOKEN
ALLOWED_ORIGINS=http://localhost:5065,http://localhost:4200
```

Run:

```bash
uvicorn main:app --host 0.0.0.0 --port 8001 --reload
```

A successful startup banner reads:

```
Startup complete. Available: sentence_model=True, clip=False, keybert=True
```

`clip=False` is normal — CLIP loads lazily on `POST /ai/load-clip`. Health check:

```bash
curl http://localhost:8001/health
```

(Windows users can double-click `ml_service/setup_ml_service.bat` instead — it handles venv creation, dependency install (including `python-dotenv` + `huggingface_hub`), and uvicorn launch.)

### Step 4 — Angular 15 frontend

`FixMyCityApp/src/environments/environment.ts` defaults point at `http://localhost:5065`. No changes needed for local dev.

```bash
cd FixMyCityApp
npm install
npx ng serve --port 4200
```

Open `http://localhost:4200`.

---

## 2c. Frontend upgrades — Phase 6 (2026-05-20)

### Icon system

All UI icons render via **Bootstrap Icons** (`bi bi-*`). The CDN is loaded in
`src/index.html` alongside Font Awesome 4 (kept only as a fallback safety net
for any HTML snippet we don't control). New code should always reach for
`bi bi-…` classes; the available glyph set is at
<https://icons.getbootstrap.com/>.

Size and animation utilities defined in `src/styles.css`:

| Class | Effect |
|-------|--------|
| `bi-2x`, `bi-3x`, `bi-1-5x` | Size scale (analogue of FA's `fa-2x`/`fa-3x`) |
| `bi-spin` | 0.85 s linear rotation — drop on top of any `bi bi-…` icon to spin it |

### Shared directives (registered in `AppModule`)

| Directive | Usage | Effect |
|-----------|-------|--------|
| `[fmcReveal]` (optional `[revealDelay]="1..4"`) | Any element. | Fade-up into view via IntersectionObserver. Group delays via `revealDelay`. No-op for `prefers-reduced-motion`. |
| `[fmcTilt]` | Marketing cards (step / benefit cards on landing). | Subtle 3D mouse-tracking tilt. Auto-disabled on touch + reduced motion. |
| `[fmcRipple]` | Primary CTAs (`<button>`, `<a class="btn-primary">`, `<a class="lp-btn-primary">`). | Material-style click ripple. No-op for reduced motion. |

### Shared components

- `<app-skeleton-card>` — drop into any list while data loads:
  ```html
  <ng-container *ngIf="isLoading">
    <app-skeleton-card *ngFor="let _ of [1,2,3,4,5]"></app-skeleton-card>
  </ng-container>
  ```
- `<app-empty-state>` now accepts a full Bootstrap Icons class string
  via `icon="bi bi-inbox"`. The `message` input is an alias for `subtitle`.

### Animations

- **Route transitions**: every navigation fades+slides via the
  `routeAnimations` trigger wired in `app.component.ts`. Uses
  `BrowserAnimationsModule` (added in `AppModule`).
- **Stat counters**, **typing headline**, **mouse-spotlight**, and
  **particle background** live on the landing page hero.
- **Navbar glassmorphism**: kicks in once the page is scrolled past 24 px.
- **Toast slide-in / slide-out**: cubic-bezier overshoot in, ease-in out.
- **Timeline stagger**: 120 ms per row.
- **Chatbot FAB pulse**: when unread bot messages exist.
- **Scoreboard rank-1 pulse**: gold ring around the first row's badge.

Every Phase-6 animation respects `@media (prefers-reduced-motion: reduce)`.

### Photo upload (Submit Complaint)

The photo dropzone is now the **first** field in the form, with full
drag-and-drop support. The component handles `dragenter / dragover /
dragleave / drop` and shows an "is-dragging" highlight on the dropzone.
Preview tile shows the chosen image, the original filename, and live
status text ("Uploading…", "Analysing with AI…", success check). The
AI hint panel mounts directly below the photo so suggestions arrive
before the user starts typing.

### Routing — 404

Wildcard routes render `NotFoundComponent` inside the public layout
(no silent redirect to `/home`). The "Take me home" button is
role-aware.

---

## 2b. Theme system + Dark Mode (Phase 5)

The Angular app ships with a complete light/dark theme system built on CSS custom properties.

### Architecture

- **`src/styles.css`** defines every visual property as a `--fmc-*` CSS custom property. The base block (`:root`) is the light palette; the `[data-theme="dark"]` block overrides surfaces, text, borders, brand tone, and shadows for dark mode.
- **`src/app/core/services/theme.service.ts`** is the single source of truth. It writes `data-theme="dark"` onto `<html>`, persists the user's choice in `localStorage['fmc_theme']`, and respects the OS-level `prefers-color-scheme` on first visit.
- **`src/app/shared/components/theme-toggle/theme-toggle.component.ts`** is the sun/moon button in every navbar (public + role layouts).
- **Pre-paint bootstrap** in `src/index.html` reads the saved theme synchronously *before* Angular boots, eliminating the "flash of light theme" on first load for dark-mode users.

### Using the theme tokens in new components

You almost never need to hardcode colors. Use the existing `--fmc-*` variables and your component automatically supports both themes:

```css
.my-card {
  background: var(--fmc-surface);
  color:      var(--fmc-text);
  border:     1px solid var(--fmc-border);
  box-shadow: var(--fmc-elev-1);
  border-radius: var(--fmc-radius-lg);
}
.my-card:hover { box-shadow: var(--fmc-elev-2); }
```

Available token families:

| Family | Examples |
|--------|----------|
| Surfaces | `--fmc-bg`, `--fmc-surface`, `--fmc-surface-2`, `--fmc-border`, `--fmc-border-strong` |
| Text | `--fmc-text`, `--fmc-text-muted`, `--fmc-text-light` |
| Brand | `--fmc-primary`, `--fmc-primary-dark`, `--fmc-primary-light`, `--fmc-primary-50…700` |
| Semantic | `--fmc-success`, `--fmc-warning`, `--fmc-danger`, `--fmc-info`, plus `*-light` variants |
| Effects | `--fmc-radius`, `--fmc-radius-lg`, `--fmc-shadow`/`-md`/`-lg`, `--fmc-elev-1`/`-2`/`-3` |
| Motion | `--fmc-motion`, `--fmc-motion-fast`, `--fmc-transition` |
| Layout | `--fmc-navbar-h`, `--fmc-sidebar-w`, `--fmc-content-max` |
| Composite | `--fmc-gradient-brand`, `--fmc-gradient-soft`, `--fmc-glass-bg`, `--fmc-glass-border` |

For the few component-scoped overrides that need to target dark mode explicitly (e.g. an inverted gradient), use the `:host-context` selector — there's an example in `login.component.css`:

```css
:host-context([data-theme="dark"]) .login-wrapper {
  background: transparent;
}
```

### Switching themes programmatically

```ts
import { ThemeService } from '../../core/services/theme.service';
// inject…
this.themeService.toggle();              // light ↔ dark
this.themeService.setTheme('dark');      // force dark, persist
this.themeService.clearOverride();       // revert to OS preference
this.themeService.theme$.subscribe(m => /* react */);
```

### Reduced motion

The Phase-5 CSS respects `prefers-reduced-motion: reduce` — every animation and transition is shortened to ~0 ms. Users who set that preference in their OS see the polished theme without animations.

---

## 3. External integration setup

### Hugging Face API token

The AI service uses HF's free serverless Inference API. Without a token, all HF-backed calls return empty results and rule-based fallbacks kick in — the platform still works, but recommendations / duplicate-detection / chat are degraded.

1. Sign up at `https://huggingface.co/join` (free).
2. Create a read-only access token at `https://huggingface.co/settings/tokens`.
3. Paste it into `FixMyCity.AI/ml_service/.env`:
   ```
   HF_API_TOKEN=hf_xxxxxxxxxxxxxxxxxxxx
   ```
4. Restart uvicorn. The startup banner should show `sentence_model=True, keybert=True`.

### AI ↔ .NET shared secret

Both services compare the `X-AI-Service-Key` header against the same value:

- .NET: `AIService:ServiceKey` in `appsettings.json`
- Python: `AI_SERVICE_KEY` in `.env`

Default for both is `fixmycity-ai-internal-key-change-me` — works out of the box. For production, generate one shared secret:

```bash
openssl rand -base64 32
```

…and set it on both sides via env vars (`AIService__ServiceKey` and `AI_SERVICE_KEY` / `DOTNET_API_KEY`). Never commit the production key.

### Razorpay (payment gateway) — Phase 5 server-orchestrated flow

The platform integrates Razorpay via the **server-orchestrated Standard Checkout** pattern from the official Razorpay .NET docs:

```
Browser              .NET API                        Razorpay
───────              ────────                        ────────
contributeViaRazorpay
                  POST /api/Payment/CreateRazorpayOrder
                                        →  POST /v1/orders
                                        ←  {id: "order_…", amount, currency, …}
                  ←  {orderId, keyId, amountPaise, …}
new Razorpay({...}).open()  →  modal …
                              ←  handler({order_id, payment_id, signature})
                  POST /api/Payment/VerifyRazorpayPayment
                  (server recomputes HMAC-SHA256(order|payment, key_secret)
                   and persists the Contribution row)
                  ←  {success: true, contributionId}
```

**Test-mode keys are committed to source (`appsettings.json:Razorpay`):**

```jsonc
"Razorpay": {
  "KeyId":      "rzp_test_SrIcMAOjaHklls",
  "KeySecret":  "81Myk1iP7uezDm1o7u9BiVwB",
  "Currency":   "INR",
  "CompanyName":"FixMyCity"
}
```

These are Razorpay sandbox credentials — **no real money moves**. The KeySecret stays on the server (HMAC verification + Basic-auth header to `https://api.razorpay.com/v1/orders`). The frontend never sees it; it learns the public KeyId via `GET /api/Payment/GetRazorpayConfig`.

#### Switching to production / your own test account

1. Sign up at `https://razorpay.com` and create a sandbox project (free).
2. Copy the test key pair from Settings → API Keys.
3. Override in environment variables (preferred over editing `appsettings.json`):

```powershell
# Windows PowerShell — set per session for dotnet run
$env:Razorpay__KeyId='rzp_test_YOUR_ID'
$env:Razorpay__KeySecret='YOUR_SECRET'
dotnet run
```
```bash
# macOS / Linux
Razorpay__KeyId='rzp_test_YOUR_ID' \
Razorpay__KeySecret='YOUR_SECRET' \
dotnet run
```

For production deploys, set the same two env vars (with `rzp_live_…` IDs) on the API host. Move them to your secret manager (Azure Key Vault, AWS Secrets Manager, etc.) — never bake live keys into a Docker image.

#### Test a real Razorpay payment

1. Sign in as a citizen (e.g. `arjun.r@example.com / Password123!`).
2. Open any complaint detail page → click **Contribute** → enter ₹100 → Pay.
3. The Razorpay test modal opens. Use any of the documented test cards:
   - **Card:** `4111 1111 1111 1111`
   - **CVV:** any 3 digits
   - **Expiry:** any future date
   - **OTP:** `1234` (or whatever the modal shows)
4. After success the modal closes; you'll see "₹100 contributed successfully" and the contribution shows up in the list with `PaymentStatus = 'Success'`.

To test failure flows: enter the failure-trigger card `5104 0600 0000 0008` (declined) — the modal shows the failure and the verify endpoint is never called, so no Contribution row is created.

#### Demo bypass (no keys at all)

If the `Razorpay:KeyId` is blank, contains `REPLACE_WITH_YOUR_KEY`, or isn't a `rzp_test_/rzp_live_` prefix, the server reports `demoMode: true`. The frontend skips the Razorpay SDK entirely and submits a synthetic `DEMO_…` order_id to the verify endpoint, which records the contribution without HMAC checks (it's still gated by JWT auth). This is the same path the original implementation used; it remains so test environments without Razorpay access still exercise the contribution flow end-to-end.

#### Webhook setup (optional)

The Phase-5 verify endpoint is the authoritative path — every successful payment is persisted server-side after HMAC verification, so webhooks are **not required** for basic operation. They are useful for:

- **Refunds** — Razorpay → POST `/api/Payment/UpdatePaymentStatus` with `newStatus: 'Refunded'`.
- **Async reconciliation** when the browser drops the connection between modal-success and verify.

To enable: in Razorpay Dashboard → Settings → Webhooks → Add Webhook URL = `https://your-api.example.com/api/Payment/UpdatePaymentStatus`. The current `PUT` endpoint expects `{ transactionRef, newStatus, failureReason }`; you'll need a small payload translator (Razorpay sends a richer shape) — implement that when you wire webhooks for production.

### Leaflet + OpenStreetMap (maps)

Loaded entirely via CDN in `FixMyCityApp/src/index.html:17-25`. No setup required.

- Tiles: `https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png` (public; attribution rendered).
- The `MapViewComponent` gracefully no-ops if Leaflet failed to load.

For high traffic or offline operation, swap the tile URL to Mapbox / a self-hosted tile server.

### Tesseract OCR (optional)

Only needed if you want OCR text extraction from uploaded complaint photos. The AI service handles its absence silently — `pytesseract.image_to_string` raises an `EnvironmentError`, caught with `logger.warning`, and `ocr_text` returns `null`.

- macOS: `brew install tesseract`
- Ubuntu: `sudo apt install tesseract-ocr`
- Windows: install from `https://github.com/UB-Mannheim/tesseract/wiki`, add to PATH.

### File uploads / shared `FixMyCityUploads` directory

Both the .NET API (`appsettings.json:Uploads:BasePath`) and the AI service (`ml_service/config.py:IMAGE_BASE_PATH`) default to `<repo>/FixMyCityUploads`. The Phase-3 fix made both paths cross-platform.

When deploying the two services on **different hosts**, mount a shared filesystem (NFS, S3-fuse, Azure Files) and point both env vars at it. Otherwise `/ai/analyze-image` can't see what the API just wrote.

---

## 4. Verified test credentials

All passwords below are **`Password123!`** (case-sensitive, with exclamation mark).
Hash = SHA-256 of UTF-8 bytes, stored as 64-char lowercase hex in `dbo.Users.PasswordHash`.

### SuperAdmin
- `admin@fixmycity.in`

### Solvers (Departments) — all approved
| Email | Department | DeptId |
|-------|------------|--------|
| `rakesh.bbmp@fixmycity.in`   | BBMP Road Engineering          | 1 |
| `priya.bwssb@fixmycity.in`   | BWSSB Water Supply             | 2 |
| `suresh.bescom@fixmycity.in` | BESCOM Electricity             | 3 |
| `anita.bbmp2@fixmycity.in`   | BBMP Solid Waste Management    | 4 |
| `venkat.bbmp3@fixmycity.in`  | BBMP Parks & Trees             | 5 |
| `ranjit.kspcb@fixmycity.in`  | KSPCB Pollution Control        | 6 |
| `nikhil.bmtc@fixmycity.in`   | BMTC Public Transport          | 7 |
| `divya.animal@fixmycity.in`  | KA Animal Welfare Board        | 8 |

Edge-case solver accounts (login blocked by design):
- `pending.solver@fixmycity.in` — `IsApproved = 0` (pending registration)
- `rejected.solver@fixmycity.in` — `ApprovalStatus = 'Rejected'`

### PWG (Public Working Groups) — all approved
| Email | Organisation | Type | OrgId |
|-------|--------------|------|-------|
| `anjali@cleanbengaluru.org`  | Clean Bengaluru NGO        | NGO                   | 1  |
| `vikram@iiscbangalore.org`   | IISc Civic Volunteer Group | Student Group         | 2  |
| `meera@infosys-csr.org`      | Infosys CSR Foundation     | CSR                   | 3  |
| `manish@bbb.org`             | Bangalore Bicycle Brigade  | Community Association | 4  |
| `sushma@welfareforall.org`   | Welfare for All Trust      | Welfare Group         | 5  |
| `rajiv@cca.org`              | Citizens for Civic Action  | NGO                   | 6  |
| `pankaj@aravindcsr.org`      | Aravind Eye Hospital CSR   | CSR                   | 7  |
| `bhavna@saahasi.org`         | Saahasi Volunteers         | Student Group         | 8  |
| `ashwin@techmcsr.org`        | Tech Mahindra Foundation   | CSR                   | 9  |
| `aditi@sankalp.org`          | Sankalp Initiative         | NGO                   | 10 |

Edge-case PWG accounts (login blocked):
- `pending.pwg@fixmycity.in` — `IsApproved = 0`
- `rejected.pwg@fixmycity.in` — `ApprovalStatus = 'Rejected'`

### Citizens

**Canonical (12, UserIds 8–19):** `arjun.r@example.com`, `kavya.s@example.com`, `rohan.k@example.com`, `sneha.p@example.com`, `karthik.i@example.com`, `aish.n@example.com`, `deepak.s@example.com`, `pooja.v@example.com`, `sanjay.m@example.com`, `ananya.g@example.com`, `vikrant.c@example.com`, `lakshmi.p@example.com`

**Massive-seed batch (60, UserIds 41–100):** `vikram.s2@example.com` through `ajay.bj@example.com` — full list in `Database/05_MassiveSeed.sql` §5b.

**Edge-case citizens (5, UserIds 36–40):**
- `sso.user@gmail.com` — SSO-only, no password. Log in via the demo Google button on the login page.
- `banned.spammer@example.com` — `IsBanned = 1`, `IsActive = 0` → login refused.
- `suspended.user@example.com` — `IsSuspended = 1` → login refused.
- `locked.user@example.com` — `LockoutUntil` set 30 min ahead → login refused.
- `deactivated.user@example.com` — `IsActive = 0` → login refused.

### How login validation works

`fn_ValidateLogin` (`Database/00_Schema_Sprint2.sql:835`) returns `0` (rejected) if **any** of these are true:

- `IsActive = 0` (deactivated or banned)
- `IsBanned = 1`
- `IsSuspended = 1`
- `LockoutUntil > now` (locked after 5 failed attempts)
- `IsApproved = 0` for Solver/PWG roles

…or if the SHA-256(UTF-8) hash doesn't match. SuperAdmin and Citizen bypass the `IsApproved` gate.

---

## 5. Startup order (single quick-reference)

```
1. SQL Server / LocalDB
2. Database/00 → 01 → 02 → 03 → 04 → 05   (only once on first install)
3. dotnet run        (FixMyCity.API → http://localhost:5065)
4. uvicorn main:app  (FixMyCity.AI/ml_service → http://localhost:8001)
5. ng serve          (FixMyCityApp → http://localhost:4200)
```

The API works without the AI service running — complaint submission still succeeds via `AIPendingScoreQueue` (Polly retry + background processor). The AI service depends on a reachable DB for `/ai/categorize-text` (KNN training queries) and `/ai/duplicate-check` (embedding lookups), but not for `/health` or `/ai/chat`.

---

## 6. Smoke test (post-deploy)

After all four services are running:

1. **Admin login** — go to `http://localhost:4200/login`, sign in as `admin@fixmycity.in / Password123!`. Should land on `/admin/dashboard` with stat cards populated from the massive seed.
2. **Admin → Approvals** — should list 4 pending (1 dept, 1 PWG, plus the 2 rejected). Approve one to verify the flow.
3. **Citizen flow** — sign in as `arjun.r@example.com / Password123!`. Go to "Submit Complaint", type 20+ chars of description and confirm the AI hint panel appears within ~1s. Optional: upload a photo and click "Use my current location".
4. **Solver flow** — sign in as `rakesh.bbmp@fixmycity.in / Password123!`. Go to dashboard → complaints list → pick an `In Progress` complaint → mark `Resolved` with a remark. Citizen gets a notification.
5. **PWG flow** — sign in as `anjali@cleanbengaluru.org / Password123!`. Go to "Open Complaints" → "Request Participation" → submit. A row appears in `dbo.PWGParticipationRequests`.
6. **AI health** — Admin dashboard → "AI Health" pill should be green. If red, check `uvicorn` is running on 8001 and `HF_API_TOKEN` is set.
7. **Contribution** — Citizen → complaint detail → "Contribute" → ₹500 → confirms demo bypass works (synthetic `DEMO_…` transaction ref logged to console).

---

## 6b. Phase 8 — Feature suggestions (2026-05-20)

Twenty civic-tech features from `fixmycity-feature-suggestions.md` are now
delivered end-to-end. Apply the new DB script after the existing run order:

```sh
sqlcmd -S localhost -d FixMyCityDB -i Database/06_FeatureSuggestions.sql
```

The script is idempotent. It adds four tables (`ComplaintUpvotes`,
`ComplaintComments`, `ComplaintAppeals`, `ComplaintInternalNotes`) and 11 new
stored procedures. No data backfill needed; everything is opt-in (citizens
have to actually vote / comment / appeal for rows to appear).

**New routes added:**

- `/transparency` (anonymous public read-only complaint feed)
- `/forgot-password`, `/reset-password?token=…` (password reset flow)
- `/admin/appeals` (citizen-appeal review queue)
- Citizen home → "My Activity" tab (unified personal feed)

**New citizen affordances:**

- Upvote button on every complaint card + detail page
- Share / QR code / SLA chip on the complaint detail
- CSV export on `/citizen/complaints`
- "Save draft" auto-restore on `/citizen/submit` (7-day TTL, localStorage)
- "Issues near me" GPS-driven feed on the citizen home

**New admin affordances:**

- Bulk status update (`POST /api/Admin/BulkUpdateStatus`)
- Manual dept reassignment (`PUT /api/Admin/ReassignDept`)
- 7/30/90-day trend chart on `/admin/dashboard`

**Solver / Admin private affordances:**

- Internal notes panel on every complaint detail (hidden for citizens)

**PWA install:**

- `manifest.webmanifest` is wired; `fmc-sw.js` registers from
  `AppComponent` via `PwaService.init()`. Citizens on Chrome / Edge will
  see "Install FixMyCity" once they've spent ~30 seconds on the app.
- **TODO before App Store / Play Store submission:** drop real
  `assets/icons/icon-192.png` + `icon-512.png` files in. The browser PWA
  install still works today using the favicon.

**Forgot-password development mode:**

- `appsettings.json` has no SMTP transport configured (Phase 8 ships the
  endpoints; real mailer is future work). With `Email:Enabled = false`
  (the default in dev), `POST /api/Auth/ForgotPassword` returns the raw
  reset token in the response body. The forgot-password page surfaces it
  as a dev banner with a one-click "Open reset page" button so QA can
  exercise the flow end-to-end without a working mailer.
- For production, set `"Email": { "Enabled": true }` in
  `appsettings.json` and replace the `_TrySendResetEmail` stub in
  `AuthController.ForgotPassword` with a real `ISmtpService` call.

---

## 7. Known limitations

(Full list in [HANDOVER.md §10](HANDOVER.md). The short version of what is **not** wired:)

1. **Push / SMS / Email notification dispatch.** `WeeklyDigestService` writes `Channel='Email'` notification rows but no SMTP backend dispatches them. `NotificationPreferences.PushEnabled` is stored but unused.
2. **`appsettings.json` ships with dev-only JWT secret + AI service key.** Both **must** be replaced (env vars or KeyVault) before any production deploy.
3. **RLS is OFF** at the schema level (`dbo.ComplaintRLS WITH STATE = OFF`). The interceptor is wired; flipping the switch is safe with respect to `AIPendingQueueProcessor` after Phase 2, but the cutover itself needs a dedicated test phase.
4. **Repositories instantiated via `new`, not DI.** Controllers do `new XRepository(_context)`. Phase 2 added backward-compatible overload constructors so the new `ILogger<T>` records are silenced by `NullLogger` until controllers migrate.
5. **Razorpay placeholder key is in source**, not env. For dev that's fine; for production move it to `environment.prod.ts` or fetch from a config endpoint.
6. **`any` / `!` non-null assertions** — ~105 across TS files. None observed to cause runtime failures; tech debt.
7. **PWA icons are placeholders.** `manifest.webmanifest` references `assets/icons/icon-192.png` + `icon-512.png` which are not in the repo. Browsers fall back to the favicon for the install card and home-screen icon. Drop in real PNGs before any App Store / Play Store submission (Phase 8 §19).
8. **Forgot-password mailer is a no-op.** `appsettings.Email:Enabled` defaults to false; in this mode the reset token is echoed back in the response (visible to the user on `/forgot-password`) so QA can exercise the flow. Replace `_TrySendResetEmail` with an SMTP integration before production (Phase 8 §18).

---

## 8. Packaging into a ZIP

This guide cannot produce a binary ZIP itself, but the following one-liner (run from the parent directory) creates a clean archive that excludes large generated folders:

**Windows (PowerShell):**
```powershell
Compress-Archive `
  -Path "FixMyCity" `
  -DestinationPath "FixMyCity-final.zip" `
  -CompressionLevel Optimal
```

(Add `-Force` to overwrite. Note: PowerShell `Compress-Archive` includes everything by default — you may want to delete `node_modules/`, `obj/`, `bin/`, `.angular/cache/`, `ml_service/venv/`, `ml_service/__pycache__/` before zipping.)

**macOS / Linux:**
```bash
zip -r FixMyCity-final.zip FixMyCity \
  -x '*/node_modules/*' '*/obj/*' '*/bin/*' '*/.angular/cache/*' \
     '*/ml_service/venv/*' '*/ml_service/__pycache__/*' \
     '*/.git/*' '*/.vs/*'
```

The clean tree (post-exclusions) is roughly:

```
FixMyCity/
├── Database/           ~150 KB
├── FixMyCity.DAL/      ~250 KB
├── FixMyCity.API/      ~200 KB
├── FixMyCity.AI/       ~80 KB  (ml_service code only)
├── FixMyCityApp/       ~5 MB   (src + package.json — npm install rebuilds node_modules)
├── AUDIT.md
├── HANDOVER.md
├── SYSTEM_FIX_PLAN.md
├── FINAL_SETUP.md
└── README.md
```

Total ~5–6 MB. A fresh recipient runs `npm install` (frontend), `pip install -r requirements_hf.txt` (AI), `dotnet restore` (API), then follows §2 of this file.
