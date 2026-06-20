# risk_analysis.md — Phase 1 Audit

Ranked risk register for the planned migration. Each row lists the trigger,
the blast radius if it fires, the mitigation we will apply, and the rollback
recipe if mitigation fails.

Severity scale: 🔴 critical (data/auth/payments loss possible) ·
🟠 high (user-facing feature breaks for everyone) ·
🟡 medium (feature degrades for some users) ·
🟢 low (developer-only / cosmetic).

---

## 1. Top critical risks

### R1 — 🔴 Image-link breakage during Cloudinary swap

**Trigger.** Phase 2 changes `filePath` from a bare filename written to disk
into a Cloudinary HTTPS URL. Eight Angular files reference `filePath` either
as `[href]`, `<img src>`, or as a value forwarded to `AnalyzeImage`. If the
serving mechanism is not also updated, every photo on the platform 404s.

**Currently unverified.** No `ServeImage` endpoint exists in
`ComplaintController.cs`. No `UseStaticFiles` call exists in `Program.cs`.
The current image serving path is therefore implicit (likely the deployed
reverse proxy or a missing endpoint that returns 404 today). This must be
re-discovered before Phase 2.

**Mitigation.**
1. Before any Cloudinary upload code lands, audit how images are served TODAY.
   Add a regression test (manual is fine): submit a complaint with photo on
   the current build, then open the complaint detail and confirm the image
   actually renders.
2. Implement Cloudinary upload as `Type = "private"` AND add a new
   `[HttpGet] GET /api/Complaint/ServeImage?path=<url>` that returns a 302 to
   a freshly signed Cloudinary URL.
3. If Angular already uses `filePath` as a direct `<img src>` somewhere, swap
   to either the new `ServeImage` URL or upload as `Type = "upload"` (public).
   Pick one strategy and apply uniformly.
4. Roll out behind a feature flag in `appsettings`: `Cloudinary:Enabled = false`
   keeps the disk pipeline alive until the swap is verified.

**Rollback.** Restore `ComplaintController.UploadComplaintImage` from git and
remove `CloudinaryService` registration in `Program.cs`. Pre-existing
complaints stay readable because their disk-relative `filePath` values were
never overwritten.

---

### R2 — 🔴 RLS (`SESSION_CONTEXT`) silent failure when `AiService` runs outside an HTTP scope

**Trigger.** `AiService` calls `_db.Complaints…ToListAsync()` to build chatbot
context, forecast stats, and recommendations. In HTTP-driven paths
`JwtSessionContextMiddleware` already populates `SESSION_CONTEXT('UserId')`.
But when `AiService` is invoked from `AIPendingQueueProcessor` (singleton,
no HTTP context), no session context is set, and any future RLS-enabled SP
or `WHERE UserId = SESSION_CONTEXT(...)` filter quietly returns zero rows.

**Today.** RLS is `STATE = OFF` per a comment in `AIPendingQueueProcessor.cs:117`,
so this is latent — but the existing worker already mitigates by setting
`UserRole = SuperAdmin` manually. The hazard is that we forget to replicate
the same belt-and-braces in `AiService` when called from background paths.

**Mitigation.** In `AIPendingQueueProcessor`, before invoking `aiService`, run
the same `EXEC sp_set_session_context N'UserRole', N'SuperAdmin', @read_only = 0;`
against the scoped DbContext's connection. Add a private helper
`AiService.EnsureBackgroundSessionAsync()` that callers from non-HTTP scopes
explicitly invoke. Document it in code.

**Rollback.** Worker degrades to zero-result reads → recommendations and
scoring silently fail. We notice via the AIPendingScoreQueue attempt-count
growing.

---

### R3 — 🔴 Secret leakage via `appsettings.json`

**Trigger.** Adding the new `Gemini`, `OpenAI`, `Cloudinary` sections to the
*committed* `appsettings.json` with real keys would publish those keys to git
history.

**Mitigation.** Committed `appsettings.json` contains **placeholders only**
(`"YOUR_..."`). Real keys go into `appsettings.Development.json` (already
gitignored — verify before Phase 1 commit) or environment variables. Add a
pre-commit grep check during Phase 1 (manual is OK): `git diff --cached |
grep -E "(AIza|sk-[A-Za-z0-9]{20}|cloudinary://)"` and abort if any hit.

**Rollback.** If keys leak: rotate them at the provider immediately, then
force-push history rewrite — only practical because the repo is private and
small.

---

## 2. High risks

### R4 — 🟠 Gemini free-tier rate limit (15 RPM / 1M tokens/day)

**Trigger.** Mass complaint submission (or running the AIPendingScoreQueue
retry loop right after the queue accumulates) bursts past 15 calls/minute.
Gemini returns 429. Currently `MLServiceClient`'s Polly retry covers the
Python service, not Gemini directly.

**Mitigation.** Register a named `HttpClient` for Gemini in `Program.cs` with
a Polly retry policy (`HandleTransientHttpError().Or<HttpRequestException>()`
plus a custom predicate for `HttpStatusCode == TooManyRequests`), exponential
backoff 1s → 2s → 4s, max 3 retries. `AiService` resolves that named client.
Background batch methods (`AutoTagAsync`, `GetRecommendationsAsync` when it
processes many users) insert `Task.Delay(1000)` between calls. Document the
limits in code so this isn't forgotten.

**Rollback.** Affected complaints fall back to deterministic defaults (empty
tags, no description suggestion). User-visible degradation, not breakage.

---

### R5 — 🟠 Wire-contract drift between `AiService` output and Angular `IDuplicateResult`

**Trigger.** `MlService.checkDuplicates` (Angular) does extensive shape-mapping
(`ml.service.ts:112-142`) that depends on the response being
`{ success, result: { complaintId, candidates: [{ complaintId, similarity,
isDuplicate }], embeddingStored } }`. If `AiService.CheckDuplicatesAsync`
returns `candidates` with snake_case (`complaint_id`, `is_duplicate`) — which
is what the CLAUDE_CODE_GUIDE template literally writes — the camelCase
serializer in `Program.cs` will *not* fix it because anonymous object property
names are taken as-is for `System.Text.Json`. Result: Angular `candidates[]`
loses every field, `hasDuplicates` is false, duplicate warning silently gone.

**Mitigation.** In `AiService`, build candidate items as anonymous objects
with **camelCase property names** (`complaintId`, `similarity`, `isDuplicate`)
— NOT the snake_case form shown in the guide. Same applies to `AnalyzeImage`
suggestions (`categoryId`, `categoryName`, `confidence`). The Angular
interfaces in `fmc-interfaces/ml.interface.ts` are the contract; verify the
shape of every `JsonResult` before merging the phase.

**Rollback.** Easy — fix the property name and redeploy. But the bug is silent
in unit tests; only end-to-end browser verification catches it.

---

### R6 — 🟠 Background worker captures `_context` in a fire-and-forget lambda

**Trigger.** `ComplaintController.SubmitComplaint` already kicks off a
`Task.Run(async () => { await _mlService.ScoreComplaintAsync(...); ... })`
*outside* the HTTP scope. Today this works because `MLServiceClient` is
self-contained. If during Phase 5 we replace those calls with
`_aiService.X(...)`, the scoped `AiService` (with its `FixMyCityDbContext`)
gets captured into the lambda — but by the time the lambda runs, the request
scope has been disposed and the DbContext throws `ObjectDisposedException`.

**Mitigation.** In `SubmitComplaint`, do **not** call `_aiService` directly
from inside the `Task.Run`. Either:
(a) only enqueue into `AIPendingScoreQueue` and let the worker process it
synchronously (simpler, slower); or
(b) inside the `Task.Run`, create a new scope via `IServiceScopeFactory`
(inject it into `ComplaintController`) and resolve a fresh `AiService` from
that scope.

Phase-5 implementation MUST follow one of these patterns. Document in code.

**Rollback.** First sign is a 500 burst with `ObjectDisposedException` traces
right after submission; revert the controller change.

---

### R7 — 🟠 `MLServiceClient` removed prematurely

**Trigger.** Phase 8 deletes `MLServiceClient.cs`. If any Phase-4-6 controller
change accidentally left a `_mlService.X` reference behind, the build breaks
on Phase 8 — or, worse, a runtime branch we forgot to test.

**Mitigation.** Phase 8 starts with `grep -rn "MLServiceClient\|_mlService"
FixMyCity.API/` returning **zero hits** before the delete. Build + run a full
smoke test in `testing_checklist.md` after the delete.

**Rollback.** `git revert` the deletion commit — `MLServiceClient.cs` was
left untouched through Phases 1-7 by design.

---

### R8 — 🟠 OpenAI moderation 401 / billing change kills toxicity check

**Trigger.** OpenAI tightens free moderation access, or the API key is
invalidated. `CheckToxicityAsync` returns 401, the fail-open path returns
`isToxic = false`, and toxic submissions slip through silently.

**Mitigation.** Today's Python service also fails open
(`MLServiceClient.CheckToxicityAsync` line 145-148). Behaviour is unchanged.
We accept this as the documented policy. Health-check endpoint surfaces
a degraded state; admin dashboard can alert.

**Rollback.** n/a — accepted.

---

## 3. Medium risks

### R9 — 🟡 Cloudinary signed-URL expiry leaks into PDF certificates

**Trigger.** `QuestPdfService` embeds `complaint.AttachmentUrl` into a
generated PDF. If the URL is a 1-hour signed Cloudinary URL, the embedded
image goes dead after the user downloads the PDF.

**Mitigation.** When generating a certificate or complaint PDF, regenerate
the signed URL just before render. Alternatively, embed the image bytes
directly into the PDF (already supported by QuestPDF) — preferable for
permanence.

**Rollback.** Re-render the PDF.

---

### R10 — 🟡 `Newtonsoft.Json` ↔ `System.Text.Json` clash via `CloudinaryDotNet`

**Trigger.** `dotnet build` emits NU1605 or runtime serialization mismatch.

**Mitigation.** After adding the NuGet, run `dotnet build`. If warnings
surface, add an explicit `<PackageReference Include="Newtonsoft.Json"
Version="13.0.3" />` to `FixMyCity.API.csproj` to pin a single version.

**Rollback.** Trivial — pin or remove.

---

### R11 — 🟡 Leaflet CDN unreachable during demo

**Trigger.** The deployment / demo environment blocks `unpkg.com`. Map fails
to load with `L is undefined` (already gracefully handled in
`map-view.component.ts:91-93` with a console warning, but the map is blank).

**Mitigation.** Pre-deployment checklist: vendor Leaflet into `src/assets/`
and self-host. Optional for dev. Tracked as a separate ticket (out of the
current migration scope).

**Rollback.** Re-enable CDN script tags.

---

### R12 — 🟡 Forecast / chatbot performance regression

**Trigger.** `AiService.GetForecastAsync` runs 4 separate EF queries against
`Complaints` for the last 30 days. With 200 seed rows it's instant; with 50k
rows it's 1-2s. Gemini call adds 2-5s. Total >5s response that admins notice.

**Mitigation.** Cache forecast result for the calling user/category for 5
minutes (in-memory `IMemoryCache`). Same for recommendations — already there
is a `UserRecommendationCache` table.

**Rollback.** Accept slower response; not a correctness bug.

---

### R13 — 🟡 Embedding model version drift

**Trigger.** `ComplaintEmbedding.ModelVersion` is stored per row. Earlier rows
have Python-era model strings; new rows write `"google-text-embedding-004"`.
Cosine similarity between vectors from different models is meaningless.

**Mitigation.** Filter dedup candidates by `ModelVersion = current` only;
batch-re-embed the historical rows in a one-time background task during
Phase 5 deployment window. Document in `deployment_steps.md`.

**Rollback.** Disable dedup until re-embedding is complete.

---

## 4. Low risks / accepted

### R14 — 🟢 Python service stays bootable but unused

After Phase 8 the Python service is "stopped" but its files remain. Devs may
accidentally start it; no harm done because the .NET API no longer talks to
it. Optional cleanup in a later sprint.

### R15 — 🟢 `AIServiceKeyMiddleware` becomes vestigial after Phase 8

Same as above — remove or keep. No functional impact while dormant.

### R16 — 🟢 HANDOVER.md / FINAL_PROJECT_HANDOVER.md mention "8 Azure AI services"

Cosmetic. Update during a documentation pass when the migration is settled.

---

## 5. Rollback playbook (master)

In order of escalation:

1. **Per-method revert.** Each controller action switching from `_mlService`
   → `_aiService` is a separate small commit. Revert just that commit and
   the rest of the migration stays intact.
2. **Per-phase revert.** Each phase in `implementation_order.md` is its own
   set of commits (1-3 commits per phase). `git revert` the range.
3. **Service-level rollback.** Remove `_aiService` injection from a controller,
   restore the `_mlService` field. The Python service still runs in dev (until
   Phase 8). No data migration needed.
4. **Full rollback to pre-migration.** `git checkout main~N` to before Phase 1.
   The Python service starts again as before. Cloudinary keys remain in
   `appsettings.Development.json` but are unused.
5. **Data rollback.** None of the planned changes write irreversible data.
   `ComplaintEmbeddings` may contain mixed-model vectors; truncate that table
   to force re-embedding from scratch. `AIPendingScoreQueue` rows are
   self-healing — leave them.

---

## 6. Pre-flight gates before Phase-3 implementation begins

- [ ] `appsettings.Development.json` exists, gitignored, contains real keys.
- [ ] Gemini API key works (cURL test: 200 from `models/gemini-1.5-flash:generateContent`).
- [ ] OpenAI moderation key works (cURL test: 200 from `/v1/moderations`).
- [ ] Cloudinary credentials work (cURL test: 200 from
  `https://api.cloudinary.com/v1_1/<cloud>/usage`).
- [ ] SQL Server LocalDB has the FixMyCity schema and `usp_SaveComplaintEmbedding`
  / `usp_SaveComplaintTags` SPs exist.
- [ ] `git status` clean. Branch off `main` for the migration work.
- [ ] User signs off on `migration_plan.md` §8 acceptance criteria.
- [ ] **Image-serving mechanism understood.** Manual check: submit a complaint
  with photo on the current build and confirm the image actually renders in
  complaint-detail. Document the path (static-files? proxy? broken today?).
