# migration_plan.md — Phase 1 Audit & Migration Plan

> **Status:** Phase 1 (Audit) complete. **No code has been changed.**
> Phase 2 (Safe Plan) below. Phase 3 (Implementation) starts only after the
> user explicitly approves this document.

---

## 1. Source-of-truth alignment

Two reference documents were supplied:

| Doc | Role | Trustworthiness |
|---|---|---|
| `FINAL_PROJECT_HANDOVER.md` | Architecture narrative ("8 Azure AI services, Service Bus, Functions, Blob Storage…") | **Partially aspirational.** Cross-checking against the actual `.csproj`, `Program.cs`, `Services/` folder, and Angular `package.json` shows none of the Azure AI/Search/Blob/ServiceBus SDKs are referenced. There is no `FixMyCity.Functions` project. The handover describes a *target* end state, not the present code. |
| `FIXMYCITY_CLAUDE_CODE_GUIDE.md` | Step-by-step migration spec (Python → Gemini, disk → Cloudinary, Azure Maps → Leaflet) | **Authoritative for migration steps.** Its "current stack" diagram matches the live repo (Python ml_service on :8001, local `../FixMyCityUploads`, Leaflet via CDN already). Its "target stack" is what we will implement. |

Conclusion: trust the **CLAUDE_CODE_GUIDE** for what to do; treat the
**HANDOVER** as marketing copy when it talks about Azure AI services.

---

## 2. Actual current state (verified)

| Layer | What the code really does | Evidence |
|---|---|---|
| Frontend | Angular 15. Bootstrap-styled, lazy modules per role, HTTP+JWT. | `FixMyCityApp/package.json` (`@angular/core ^15.0.0`), `app-routing.module.ts` |
| Maps | **Leaflet 1.9.4 already loaded via CDN** (not Azure Maps). Uses OSM tiles. | `FixMyCityApp/src/index.html:39,44`; `shared/components/map-view/map-view.component.ts` |
| Backend | .NET 8, EF Core, JWT, Polly retry, rate limiting, Swagger, QuestPDF. | `FixMyCity.API.csproj`, `Program.cs` |
| AI | **Python microservice** on `http://localhost:8001` (FastAPI, Ollama/HF). `.NET` calls it through `MLServiceClient` (typed `HttpClient`). | `FixMyCity.API/Services/MLServiceClient.cs`; `FixMyCity.AI/ml_service/`; `appsettings.json` `AIService:BaseUrl` |
| Image upload | **Local disk** at `../FixMyCityUploads` (configured via `Uploads:BasePath`). | `ComplaintController.cs:51-90`; `appsettings.json:17-20` |
| Image serving | No `ServeImage` endpoint in code. Frontend uses `filePath` directly as `[href]`/`<img src>`. (Will need to confirm static-file pipeline before Cloudinary swap — see Risks §6.) | `grep ServeImage` returns 0 hits in API; `citizen-complaint-detail.component.html:185` uses `att.filePath` |
| Async AI | `AIPendingQueueProcessor` (`BackgroundService`, polls every 5 min) retries failed scoring via `MLServiceClient`. No Service Bus, no Functions. | `Services/AIPendingQueueProcessor.cs` |
| Dedup | `ComplaintEmbedding` EF entity exists with `EmbeddingJson` (JSON-of-floats). DB stores embeddings; similarity computed in Python (currently). No Azure AI Search. | `FixMyCity.DAL/Models/ComplaintEmbedding.cs` |
| Auth | JWT (HS256) + refresh tokens; `JwtSessionContextMiddleware` injects `SESSION_CONTEXT` for SQL-side RLS via `SessionContextInterceptor`. | `Program.cs:48-98`; `Middleware/JwtSessionContextMiddleware.cs`; `DAL/Infrastructure/SessionContextInterceptor.cs` |
| AI write-back security | Custom `AIServiceKeyMiddleware` on `/api/ML/*` write endpoints checks `X-AI-Service-Key`; Angular-consumed endpoints are whitelisted. | `Middleware/AIServiceKeyMiddleware.cs` |
| Payments | Razorpay test keys in `appsettings.json`, Polly-wrapped typed client. | `RazorpayService.cs`, `Program.cs:131-139` |

---

## 3. Target state (per CLAUDE_CODE_GUIDE)

1. New `FixMyCity.API/Services/AiService.cs` calling Google Gemini + OpenAI
   moderation directly via `IHttpClientFactory`. Each method is fail-open.
2. New `FixMyCity.API/Services/CloudinaryService.cs` wrapping `CloudinaryDotNet`.
3. `ComplaintController.UploadComplaintImage` writes to Cloudinary instead of
   disk; `filePath` becomes a Cloudinary HTTPS URL.
4. `MLController` actions delegate to `AiService` instead of `MLServiceClient`.
   **All endpoint URLs, request DTOs and response JSON shapes stay identical.**
5. `AIPendingQueueProcessor` resolves `AiService` from a scope and calls
   `AiService.ScoreComplaintAsync(...)` with the same parameter list.
6. Python `ml_service` and `MLServiceClient.cs` stay in the tree (disabled but
   not deleted) until every replacement is verified end-to-end.

---

## 4. Migration plan — step ordering

Phases are sequenced so the app boots and serves traffic at the **end of every
phase**. The Python service keeps running as the fallback until Phase 7.

| # | Phase | What changes | Risk if it breaks |
|---|---|---|---|
| 1 | **Foundation (additive, no behaviour change)** | Add `CloudinaryDotNet` to `.csproj`. Add `Gemini`, `OpenAI`, `Cloudinary`, `AiScoring` sections to `appsettings.json` with empty/placeholder keys. Register `IHttpClientFactory`, `CloudinaryService`, `AiService` (empty skeleton) in `Program.cs`. Keep `MLServiceClient` registration untouched. | Build break only — caught immediately by `dotnet build`. No runtime impact. |
| 2 | **Cloudinary upload path** | Replace `ComplaintController.UploadComplaintImage` body to push to Cloudinary; `filePath` becomes the secure URL. Add `ServeImage` redirect endpoint (or update Angular to use the URL directly). | Image submission blocked. **Mitigation:** keep a fallback branch that writes to disk if Cloudinary creds are missing, gated by env. |
| 3 | **AiService — sync features first** | Implement `AnalyzeImageAsync`, `CategorizeTextAsync`, `CheckToxicityAsync`, `IsHealthyAsync` in `AiService`. **Do not yet rewire the controllers** — add the methods, validate via unit smoke test. | Pure addition — zero traffic impact. |
| 4 | **Switch MLController readers** | One action at a time, change `_mlService.X` → `_aiService.X` inside `MLController.AnalyzeImage`, `CategorizeText`, `Chat`, `CheckDuplicates`, `CheckAIHealth`. Each change tested against Angular before moving on. | A single endpoint can revert to old `MLServiceClient` if the new path misbehaves. |
| 5 | **Background scoring + tagging** | Implement `ScoreComplaintAsync`, `AutoTagAsync`, `SaveTagsAsync`, `SaveEmbeddingAsync`, `CheckDuplicatesAsync` in `AiService`. Update `AIPendingQueueProcessor` to resolve `AiService` from `IServiceScopeFactory` (it's a `BackgroundService`, `AiService` is scoped — see CLAUDE_CODE_GUIDE §17 *AiService Scope* gotcha). | Background job stops scoring new complaints. Retry queue catches them; no user-visible failure. |
| 6 | **PWG verdict + Forecast + Recommendations** | Add `GetPwgVerdictAsync`, `GetForecastAsync`, `GetRecommendationsAsync`, route `MLController` actions to them. | Admin-side, low traffic. |
| 7 | **Maps — verify only** | The repo already uses Leaflet via CDN with OSM tiles. CLAUDE_CODE_GUIDE §6 prescribes the npm-package route, but **the working code already satisfies the goal**. Decision: do not churn the working component. Optionally pin Leaflet via npm later for production builds. | None — explicit decision to skip a working migration step. |
| 8 | **Disable Python fallback** | Stop the Python service in dev. Leave `MLServiceClient.cs` in place but unreferenced. Drop the `AIService` config section. Optionally tag for deletion in a later sprint. | Easy revert: restart Python, restore controller injection. |
| 9 | **Verification** | Run the entire test plan in `testing_checklist.md`. Confirm every `ml.service.ts` method returns the expected shape via DevTools. | n/a |

---

## 5. Wire-format contracts that MUST NOT change

Drawn from `FixMyCityApp/src/app/fmc-services/ml.service.ts` and
`fmc-interfaces/ml.interface.ts`. Any deviation breaks Angular at runtime
without a compile error.

| Endpoint | Request → Response shape (camelCase) |
|---|---|
| `GET /api/ML/CheckAIHealth` | `{ success, aiServiceOnline }` |
| `GET /api/ML/GetMLScores?complaintId` | `IMLScores` (priorityScore, resolutionProbability, predictedResolutionDate, modelVersion, …) |
| `GET /api/ML/GetTags?complaintId` | `[{ tag, score }]` |
| `GET /api/ML/GetUserInterests?userId` | `[{ interestId, userId, categoryId?, preferredLocalityId? }]` |
| `GET /api/ML/GetRecommendedComplaints?userId&topN` | `[{ complaintId }]` |
| `POST /api/ML/CategorizeText` | `{ success, suggestions: [{ categoryId, categoryName, confidence }], suggestedDescription? }` |
| `POST /api/ML/AnalyzeImage` | `{ success, result: { complaintId?, suggestions, ocrText, gpsLat, gpsLon, suggestedDescription } }` |
| `POST /api/ML/CheckDuplicates` | `{ success, result: { complaintId, candidates: [{ complaintId, similarity, isDuplicate }], embeddingStored } }` |
| `POST /api/ML/GetGeoClusters` | `{ success, result: { clusters: [{ clusterId, complaintCount, centroidLat, centroidLng, complaintIds }], noiseCount } }` |
| `POST /api/ML/GetForecast` | `{ success, result }` (passthrough; Angular treats as `any`) |
| `POST /api/ML/Chat` | `{ success, reply }` |
| `POST /api/ML/TriggerRetrain` | `{ success, message }` |
| `PUT  /api/ML/OverrideAIDecision` | `IApiResponse` |
| `POST /api/ML/AddUserInterest` | `IApiResponse` |
| `DELETE /api/ML/RemoveUserInterest` | `IApiResponse` |
| `POST /api/Complaint/UploadComplaintImage` | `{ success, filePath, fileName, fileSizeKB }` (filePath may become a full URL — Angular treats it as opaque) |

---

## 6. Phase-2 safety checklist (preserve these patterns)

- **Angular endpoints unchanged.** Every URL above stays identical. Only the
  controller's internal collaborator changes.
- **Response shapes unchanged.** `JsonNamingPolicy.CamelCase` is already set in
  `Program.cs:184`; keep it.
- **Fail-open at every layer.** `MLServiceClient` returns empty DTOs / `false`
  on exception. `AiService` must do the same: every public method wraps the
  Gemini/OpenAI call in `try/catch` and returns the documented default.
- **JWT + RLS.** Don't touch `Program.cs` auth pipeline, `JwtSessionContextMiddleware`,
  or `SessionContextInterceptor`. Cloudinary swap does not affect SQL access.
- **Database schema is frozen.** No new tables, no column drops. The
  `ComplaintEmbedding`/`ComplaintTags`/`ComplaintMlscore` tables are already in
  place. `usp_SaveComplaintEmbedding`, `usp_SaveComplaintTags`,
  `usp_UpsertRecommendationCache` SPs are reused.
- **Scoped vs Singleton trap.** `AiService` is **Scoped** (uses
  `FixMyCityDbContext`). `AIPendingQueueProcessor` is a **Singleton**
  `BackgroundService` — it must resolve `AiService` per-iteration via
  `IServiceScopeFactory.CreateScope()`. Already the pattern used today for
  `FixMyCityDbContext` (`AIPendingQueueProcessor.cs:49`); apply the same pattern
  to `AiService`.
- **Polly retry.** Currently wired around `MLServiceClient`. After migration we
  should attach a similar retry policy to `IHttpClientFactory.CreateClient()`
  for Gemini/OpenAI — or use a named client with the retry policy registered in
  `Program.cs`. Don't lose this — 429 from Gemini free tier is common.
- **Don't delete `MLServiceClient.cs`** until Phase 8 verification passes. It
  is our rollback path.
- **ServeImage gap.** Before Phase 2 commits, locate the exact mechanism that
  currently serves uploaded photos to the browser (no `UseStaticFiles` call
  was found in `Program.cs`; the frontend binds `filePath` as `[href]`). Either
  add `ServeImage` redirect or change Angular to use the Cloudinary URL
  directly — pick ONE and document.

See **risk_analysis.md** for the full risk register and rollback playbook.

---

## 7. What this plan deliberately does NOT do

- **No Azure deployment.** The HANDOVER's Azure-AI-Search / Blob / Service-Bus /
  Functions story is out of scope. The repo's de-facto direction is the free-tier
  Gemini+OpenAI+Cloudinary stack; aligning with the false Azure narrative would
  multiply cost and rewrite the AI layer twice.
- **No Angular refactor.** Even `app.module.ts` stays untouched. The Leaflet
  map already works.
- **No schema migrations.** The DAL is already wired for the target features.
- **No removal of `MLServiceClient`** until the end. Reversibility > tidiness.

---

## 8. Acceptance criteria for Phase 1 ↔ Phase 2 hand-off

Before any code is changed, the user must confirm:

1. ✅ Migration steps in §4 are the right ordering.
2. ✅ Wire-format inventory in §5 is complete (or list any missing endpoints).
3. ✅ The decision to **skip** the Azure Maps → Leaflet npm migration (because
   Leaflet is already in use via CDN) is acceptable.
4. ✅ API keys for Gemini, OpenAI, and Cloudinary will be supplied via
   `appsettings.Development.json` (gitignored) or environment variables before
   Phase 3 begins — Phase-3 cannot complete end-to-end testing without them.
5. ✅ A local SQL Server LocalDB instance with the FixMyCity schema is
   available for verification at the end of each phase.

Once those five points are signed off, the implementation can begin
following `implementation_order.md`.
