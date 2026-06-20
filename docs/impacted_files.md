# impacted_files.md — Phase 1 Audit

Every file the planned migration will create, modify, or leave deliberately
unchanged. Paths are repo-root-relative.

Legend
- 🆕 **CREATE** — new file.
- ✏️ **MODIFY** — edit existing file. Scope of change noted.
- 🗑️ **OBSOLETE** — kept on disk until Phase 8 verification; then deleted in a
  dedicated cleanup commit.
- ⛔ **DO NOT TOUCH** — listed because the docs imply churn here but the audit
  found it is correct as-is.

---

## A. .NET API project (`FixMyCity.API/`)

### A.1 Configuration & project metadata

| File | Action | Phase | Detail |
|---|---|---|---|
| `FixMyCity.API.csproj` | ✏️ MODIFY | 1 | Add `<PackageReference Include="CloudinaryDotNet" Version="1.*" />`. Leave existing entries (JwtBearer, QuestPDF, Polly, Swashbuckle, Data.SqlClient) alone. |
| `appsettings.json` | ✏️ MODIFY | 1 | Add `Gemini`, `OpenAI`, `Cloudinary`, `AiScoring` sections (placeholder/empty values; real secrets go in `appsettings.Development.json` or env). **Do not remove** `AIService:` and `Uploads:` sections yet — they keep the Python fallback alive through Phase 7. |
| `appsettings.Development.json` | ✏️ MODIFY | 1 | Same sections as above, populated with real keys (file must remain gitignored). |
| `Program.cs` | ✏️ MODIFY | 1, 5 | Phase 1: add `builder.Services.AddHttpClient()`, `AddSingleton<CloudinaryService>()`, `AddScoped<AiService>()`. Phase 5: no change here — `AIPendingQueueProcessor` already uses `IServiceScopeFactory`. **Do not touch** JWT, RLS, Polly, rate limiting, CORS, Swagger, or middleware ordering. |

### A.2 Services (new and modified)

| File | Action | Phase | Detail |
|---|---|---|---|
| `Services/AiService.cs` | 🆕 CREATE | 1, 3, 5, 6 | Phase 1 = skeleton + helpers (`GeminiGenerateAsync`, `GetEmbeddingAsync`, `CosineSimilarity`, `StripFences`). Phase 3 = sync feature methods. Phase 5 = scoring + tagging + dedup. Phase 6 = PWG verdict + forecast + recommendations. |
| `Services/CloudinaryService.cs` | 🆕 CREATE | 1 | `UploadImageAsync(stream, fileName) → secureUrl`; `GetSignedUrl(cloudinaryUrl, expires=3600)`. Singleton. |
| `Services/MLServiceClient.cs` | 🗑️ OBSOLETE (Phase 8) | 8 | Stays untouched until the very end as the rollback safety net. Final delete only when all replacements verified. |
| `Services/AIPendingQueueProcessor.cs` | ✏️ MODIFY | 5 | Inside `ProcessQueueAsync`, also resolve `AiService` from the scope: `var ai = scope.ServiceProvider.GetRequiredService<AiService>();`. Replace the `mlService.ScoreComplaintAsync(...)` call with `ai.ScoreComplaintAsync(...)`. **Keep** the health-check, attempt counter, dead-letter behaviour. |
| `Services/AutoEscalationService.cs` | ⛔ DO NOT TOUCH | — | Unrelated to AI. |
| `Services/WeeklyDigestService.cs` | ⛔ DO NOT TOUCH | — | Unrelated. |
| `Services/RazorpayService.cs` | ⛔ DO NOT TOUCH | — | Payments out of scope. |
| `Services/JwtService.cs` | ⛔ DO NOT TOUCH | — | Auth out of scope. |
| `Services/QuestPdfService.cs` | ⛔ DO NOT TOUCH | — | PDFs out of scope. |

### A.3 Controllers

| File | Action | Phase | Detail |
|---|---|---|---|
| `Controllers/MLController.cs` | ✏️ MODIFY | 4, 5, 6 | Add `private readonly AiService _aiService;` constructor param **without removing** `_mlService` yet. Phase 4: switch `AnalyzeImage`, `CategorizeText`, `Chat`, `CheckDuplicates`, `CheckAIHealth` to `_aiService.*`. Phase 5: switch `SaveEmbedding`, `SaveTags`, ensure `SaveMLScores` callback still works. Phase 6: route `GetForecast`, `GetRecommendedComplaints`, add `GetPwgVerdict` action (new path stays inside `/api/ML/*`). Each action keeps its existing `[HttpGet]/[HttpPost]` attribute and response shape exactly. |
| `Controllers/ComplaintController.cs` | ✏️ MODIFY | 2, 5 | Phase 2: add `CloudinaryService _cloudinary` ctor param; rewrite `UploadComplaintImage` body so the file streams to Cloudinary and `filePath` becomes the secure URL. Add new `[HttpGet] ServeImage([FromQuery] string path)` that redirects to a Cloudinary signed URL. Phase 5: optionally replace inline `_mlService.ScoreComplaintAsync/Check/Tag` calls inside `SubmitComplaint` with `_aiService` equivalents — but only after Phase 5 service methods land. |
| `Controllers/AuthController.cs` | ⛔ DO NOT TOUCH | — | |
| `Controllers/AdminController.cs` | ⛔ DO NOT TOUCH | — | |
| `Controllers/PWGController.cs` | ⛔ DO NOT TOUCH | — | (Unless Phase 6 wires `ReviewPWGWork` to call `_aiService.GetPwgVerdictAsync`; if so the response field for the verdict is added but no existing field is removed.) |
| `Controllers/PaymentController.cs` | ⛔ DO NOT TOUCH | — | |
| `Controllers/GamificationController.cs` | ⛔ DO NOT TOUCH | — | |
| `Controllers/PublicController.cs` | ⛔ DO NOT TOUCH | — | |
| `Controllers/ReportController.cs` | ⛔ DO NOT TOUCH | — | |
| `Controllers/UserController.cs` | ⛔ DO NOT TOUCH | — | |

### A.4 Middleware

| File | Action | Phase | Detail |
|---|---|---|---|
| `Middleware/AIServiceKeyMiddleware.cs` | ✏️ MODIFY (Phase 8 only) | 8 | After the Python service is retired, the X-AI-Service-Key check on write-back endpoints (`SaveMLScores`, `SaveEmbedding`, `SaveTags`, `SaveRecommendationCache`, `LogAIDecision`) becomes vestigial because all writes now happen in-process through `AiService`. Choices: (a) drop the middleware entirely, or (b) repurpose for any future webhook. Recommend (a) in Phase 8. |
| `Middleware/JwtSessionContextMiddleware.cs` | ⛔ DO NOT TOUCH | — | RLS injection — load-bearing. |
| `Middleware/SecurityHeadersMiddleware.cs` | ⛔ DO NOT TOUCH | — | |

### A.5 Models

| File | Action | Phase | Detail |
|---|---|---|---|
| `Models/MLAndPaymentRequests.cs` | ✏️ MODIFY | 6 | Add `PwgVerdictRequest` and `PwgVerdictResult` if not present. Other request DTOs (`CategorizeTextRequest`, `AnalyzeImageRequest`, `CheckDuplicatesRequest`, `ChatRequest`, etc.) already exist — do not rename. |
| `Models/AIModels.cs` | ⛔ DO NOT TOUCH | — | Shared AI request DTOs are correct. |
| Everything else | ⛔ DO NOT TOUCH | — | |

---

## B. DAL project (`FixMyCity.DAL/`)

| File | Action | Phase | Detail |
|---|---|---|---|
| Entire project | ⛔ DO NOT TOUCH | — | All entities/repos/SPs needed already exist: `ComplaintEmbedding`, `ComplaintTag`, `ComplaintMlscore`, `AIDecisionLog`, `AIPendingScoreQueue`. The SP-based callbacks (`usp_SaveComplaintEmbedding`, `usp_SaveComplaintTags`, `usp_UpsertRecommendationCache`, `usp_SaveAIDecision`) are reused by `AiService`. |
| `Infrastructure/SessionContextInterceptor.cs` | ⛔ DO NOT TOUCH | — | RLS — load-bearing. |
| `FixMyCity.DAL.csproj` | ⛔ DO NOT TOUCH | — | |
| `*.sql` (`FixMyCityDB_Sprint2_FIXED.sql`, `AI_Tables_Addition.sql`, `DB_Patch.sql`) | ⛔ DO NOT TOUCH | — | Schema frozen. |

---

## C. Python AI project (`FixMyCity.AI/`)

| Path | Action | Phase | Detail |
|---|---|---|---|
| `ml_service/main.py`, `routers/*.py`, `services/*.py`, `config.py`, `requirements*.txt`, `Dockerfile`, `docker-compose.yml` | 🗑️ OBSOLETE | 8 | Stop the service in dev when Phase 8 begins. Leave on disk until verification passes. Final cleanup commit can delete `FixMyCity.AI/` once Phase 9 is green. |
| `ml_service/venv/` | n/a | — | Already developer-local. |

---

## D. Frontend (`FixMyCityApp/`)

### D.1 Build / config

| File | Action | Phase | Detail |
|---|---|---|---|
| `package.json` | ⛔ DO NOT TOUCH (decision) | 7 | CLAUDE_CODE_GUIDE §6 prescribes `npm install leaflet @asymmetrik/ngx-leaflet`. Leaflet is **already loaded via CDN** in `src/index.html` and the existing `MapViewComponent` works against the global `L`. Migrating to the npm package is churn with no behavioural benefit until prod offline-build is required. Recommend deferring to a separate ticket. |
| `angular.json` | ⛔ DO NOT TOUCH | — | Per above. |
| `src/index.html` | ⛔ DO NOT TOUCH | — | Leaflet CDN tags already present (`index.html:39, 44`). |

### D.2 Map component

| File | Action | Phase | Detail |
|---|---|---|---|
| `src/app/shared/components/map-view/map-view.component.ts` (.html, .css) | ⛔ DO NOT TOUCH | — | Already uses Leaflet + OSM tiles. Renders markers, clusters, heatmap. The CLAUDE_CODE_GUIDE replacement is functionally equivalent for less feature; not adopting it preserves the working heatmap. |

### D.3 AI service shape (must remain stable)

| File | Action | Phase | Detail |
|---|---|---|---|
| `src/app/fmc-services/ml.service.ts` | ⛔ DO NOT TOUCH | — | Reference for Phase-5 response contracts in `migration_plan.md` §5. |
| `src/app/fmc-interfaces/ml.interface.ts` | ⛔ DO NOT TOUCH | — | Same. Any new field added on the backend MUST be optional. |
| `src/app/fmc-services/complaint.service.ts` | ⛔ DO NOT TOUCH | — | `uploadComplaintImage` treats `filePath` as opaque. Cloudinary URL is a drop-in. |

### D.4 Image consumers (Cloudinary impact)

| File | Action | Phase | Detail |
|---|---|---|---|
| `src/app/citizen/complaint-detail/citizen-complaint-detail.component.html` | ⛔ DO NOT TOUCH (likely) | 2 | Uses `[href]="att.filePath"` — when `filePath` becomes an `https://res.cloudinary.com/...` URL the anchor still works. Verify image `<img src>` bindings before confirming. |
| `src/app/shared/components/photo-compare/photo-compare.component.ts` | ⛔ DO NOT TOUCH (likely) | 2 | Treats `filePath` as opaque. |
| `src/app/citizen/submit-complaint/submit-complaint.component.ts` | ⛔ DO NOT TOUCH | — | Receives `filePath` and forwards to `analyzeImage`. |
| `src/app/gamification/my-certificates/my-certificates.component.ts` | ⛔ DO NOT TOUCH | — | |

### D.5 Everything else under `src/app/`

| Path | Action | Detail |
|---|---|---|
| `admin/`, `auth/`, `citizen/`, `core/`, `gamification/`, `layouts/`, `public/`, `pwg/`, `solver/`, `shared/components/<everything except map-view>`, `fmc-services/*`, `fmc-interfaces/*` | ⛔ DO NOT TOUCH | Out of scope. |

---

## E. Root-level documentation / tooling

| File | Action | Detail |
|---|---|---|
| `README.md`, `AUDIT.md`, `HANDOVER.md`, `SYSTEM_FIX_PLAN.md`, `FINAL_SETUP.md`, `fixmycity-feature-suggestions.md`, `fixmycity-frontend-upgrades.md`, `FixMyCity_Backlog_v2.xlsx`, `Database/`, `Database.zip`, `routers.zip`, `FixMyCityUploads/` | ⛔ DO NOT TOUCH | The 5 new docs we are generating (`migration_plan.md`, `impacted_files.md`, `dependency_matrix.md`, `risk_analysis.md`, `implementation_order.md`) sit alongside these. Phase-3 deliverables (`final_migration_summary.md`, `testing_checklist.md`, `remaining_risks.md`, `deployment_steps.md`) will too. |

---

## F. File totals

- **CREATE:** 2 (`AiService.cs`, `CloudinaryService.cs`) + 5 plan docs already
  written + 4 final-phase docs to write after implementation.
- **MODIFY:** 6 (`csproj`, `appsettings.json`, `appsettings.Development.json`,
  `Program.cs`, `MLController.cs`, `ComplaintController.cs`,
  `AIPendingQueueProcessor.cs`, `MLAndPaymentRequests.cs`,
  `AIServiceKeyMiddleware.cs`).
- **OBSOLETE (kept until Phase 8):** `MLServiceClient.cs`, entire `FixMyCity.AI/`
  directory.
- **TOUCH ZERO LINES OF:** all 9 unrelated controllers, all of DAL, all of
  Angular (except possibly a verification pass on image anchors).
