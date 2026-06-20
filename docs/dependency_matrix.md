# dependency_matrix.md — Phase 1 Audit

How every actor in the migration depends on every other actor, with version
notes and conflict flags. Used to verify Phase-3 commits don't break a hidden
contract.

---

## 1. .NET NuGet packages

### Currently in `FixMyCity.API.csproj`

| Package | Pinned | Role | Phase-3 impact |
|---|---|---|---|
| `Swashbuckle.AspNetCore` | 6.6.2 | Swagger UI | None |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 8.0.13 | JWT validation | None — auth pipeline untouched |
| `System.IdentityModel.Tokens.Jwt` | 8.0.2 | Token issuance in `JwtService` | None |
| `QuestPDF` | 2024.3.0 | PDF generation | None |
| `Microsoft.Extensions.Http.Polly` | 8.0.13 | Retry around `MLServiceClient` | Reused — will wrap the new `IHttpClientFactory` clients for Gemini/OpenAI too |
| `Microsoft.Data.SqlClient` | 5.2.2 | Raw ADO for `JwtService` + queue reader | None |

### To be added in Phase 1

| Package | Target version | Role | Risk |
|---|---|---|---|
| `CloudinaryDotNet` | `1.*` (latest 1.x) | Image upload + signed URL | Pulls `Newtonsoft.Json` — already in transitive graph; no conflict with `System.Text.Json` used elsewhere. Use namespace-scoped `using CloudinaryDotNet`. |

### Deliberately NOT added (CLAUDE_CODE_GUIDE-style alternative)

| Package | Why skipped |
|---|---|
| `Google.Cloud.AIPlatform.V1` / `Google.Apis.Generativelanguage.v1beta` | Gemini is consumed via raw `HttpClient` per the guide. Avoids a 30 MB SDK pulled for two endpoints. |
| `Azure.AI.OpenAI`, `Azure.AI.Vision.*`, `Azure.AI.ContentSafety`, `Azure.AI.Language.*`, `Azure.Search.Documents`, `Azure.Storage.Blobs`, `Azure.Messaging.ServiceBus`, `Azure.Maps.*` | Not the chosen direction. HANDOVER doc lists these aspirationally; they are not in the csproj today and will not be added. |

### `FixMyCity.DAL.csproj`

| Package | Phase-3 impact |
|---|---|
| (whatever is currently listed) | None — DAL is unchanged. |

---

## 2. Angular npm packages

### Currently in `FixMyCityApp/package.json`

| Package | Version | Role |
|---|---|---|
| `@angular/{core,common,compiler,forms,router,…}` | `^15.0.0` | Framework |
| `rxjs` | `~7.5.0` | Streams |
| `zone.js` | `~0.12.0` | Change detection |
| `tslib` | `^2.3.0` | TS runtime helpers |
| dev: `@angular/cli`, `@angular-devkit/build-angular`, `typescript ^4.8.4`, jasmine/karma | | Build / test |

### Loaded outside npm

| Asset | Source | Notes |
|---|---|---|
| `leaflet@1.9.4` JS + CSS | `https://unpkg.com/leaflet@1.9.4/dist/` via `<link>` and `<script>` in `src/index.html:39, 44` | The map component already works against the global `L`. **No npm install needed for Phase 3.** Production hardening (vendoring leaflet) is its own ticket. |

### Deliberately NOT added (CLAUDE_CODE_GUIDE §6)

| Package | Why skipped |
|---|---|
| `leaflet`, `@asymmetrik/ngx-leaflet`, `@types/leaflet` | Map already works via CDN. Adopting the npm package would force a rewrite of the working `MapViewComponent` to use directives instead of imperative `L.map()`. Defer. |

---

## 3. External services & secrets

| Service | Used by | Config key | Required by phase |
|---|---|---|---|
| Google Generative AI (Gemini) | `AiService.GeminiGenerateAsync`, `GetEmbeddingAsync` | `Gemini:ApiKey`, `Gemini:Model` (default `gemini-1.5-flash`), `Gemini:EmbedModel` (default `models/text-embedding-004`), `Gemini:BaseUrl` | Phase 3 |
| OpenAI Moderation (free tier) | `AiService.CheckToxicityAsync` | `OpenAI:ApiKey`, `OpenAI:ModerationUrl` (default `https://api.openai.com/v1/moderations`) | Phase 3 |
| Cloudinary | `CloudinaryService` | `Cloudinary:CloudName`, `Cloudinary:ApiKey`, `Cloudinary:ApiSecret`, `Cloudinary:UploadFolder` (default `fixmycity/complaints`) | Phase 2 |
| OpenStreetMap tile server | Angular `MapViewComponent` | None (anonymous tiles, attribution required) | Already in use |
| Razorpay (test mode) | `RazorpayService` | `Razorpay:KeyId`, `Razorpay:KeySecret`, `Razorpay:Currency`, `Razorpay:CompanyName` | Untouched |
| SQL Server LocalDB | EF Core / `JwtService` | `ConnectionStrings:DefaultConnection` | Always |
| Python ml_service @ `localhost:8001` (transitional) | `MLServiceClient` | `AIService:BaseUrl`, `AIService:ServiceKey` | Removed in Phase 8 |

### Secret handling

- Real secrets live in **`appsettings.Development.json`** (gitignored) or
  environment variables (`Gemini__ApiKey`, etc.).
- `appsettings.json` keeps only **placeholders**.
- Pre-Phase-3 gate: confirm `appsettings.Development.json` is in `.gitignore`
  and that committed `appsettings.json` has no real keys.

---

## 4. Inter-project / inter-service call graph

```
Angular (FixMyCityApp, :4200)
   │  HTTP + JWT Bearer
   ▼
.NET API (FixMyCity.API, :5065)
   ├─ Controllers/MLController            ──►  Services/AiService (NEW)         ──► Gemini / OpenAI (HTTPS)
   │                                      └─►  Services/MLServiceClient (LEGACY, Phase 8 deletion)
   ├─ Controllers/ComplaintController     ──►  Services/CloudinaryService (NEW) ──► Cloudinary (HTTPS)
   │                                      └─►  Services/AiService (toxicity/score/tag)
   ├─ Controllers/*                       ──►  FixMyCity.DAL/Repositories       ──► SQL Server LocalDB
   ├─ Services/AIPendingQueueProcessor    ──►  Services/AiService (via scope)
   │                                      └─►  FixMyCity.DAL/AIPendingScoreQueue
   ├─ Services/AutoEscalationService      ──►  DAL
   ├─ Services/WeeklyDigestService        ──►  DAL
   ├─ Services/RazorpayService            ──►  api.razorpay.com
   ├─ Services/JwtService                 ──►  DAL via ADO
   └─ Services/QuestPdfService            ──►  (pure)

FixMyCity.AI/ml_service (Python, :8001)   — connected only via MLServiceClient until Phase 8.
```

---

## 5. DI lifetime matrix

| Type | Lifetime today | Lifetime after migration | Notes |
|---|---|---|---|
| `FixMyCityDbContext` | Scoped (via `AddDbContext`) | unchanged | |
| `IJwtService` | Singleton | unchanged | |
| `IQuestPdfService` | Singleton | unchanged | |
| `MLServiceClient` | Scoped (typed HttpClient) | unchanged → removed Phase 8 | |
| `IRazorpayService` | Scoped (typed HttpClient) | unchanged | |
| `AiService` | — | **Scoped** (holds `FixMyCityDbContext`) | `AIPendingQueueProcessor` (singleton) must resolve via `IServiceScopeFactory.CreateScope()` |
| `CloudinaryService` | — | **Singleton** | Pure SDK wrapper, no per-request state |
| `IHttpClientFactory` | Singleton | unchanged | |
| `AIPendingQueueProcessor` | Singleton (`AddHostedService`) | unchanged | |
| `AutoEscalationService` | Singleton (`AddHostedService`) | unchanged | |
| `WeeklyDigestService` | Singleton (`AddHostedService`) | unchanged | |

---

## 6. Database object dependencies

| Object | Used by | Notes |
|---|---|---|
| `ComplaintEmbedding` (EF entity + table) | `AiService.SaveEmbeddingAsync`, `CheckDuplicatesAsync`, `GetRecommendationsAsync` | Already exists; column `EmbeddingJson` holds a `List<float>` serialized as JSON. |
| `ComplaintTags` (table) | `AiService.SaveTagsAsync` (via `usp_SaveComplaintTags` or direct INSERT/DELETE) | Existing. |
| `ComplaintMlscores` (table) | `AiService.SaveMlScoresAsync` (MERGE) | Existing. |
| `AIPendingScoreQueue` (table) | `AIPendingQueueProcessor` (read), `ComplaintController.SubmitComplaint` (enqueue) | Existing. |
| `AIDecisionLog` (table) | `MLController.LogAIDecision`, `OverrideAIDecision` | Existing. Callbacks remain — `AiService` can write here directly in Phase 6 if desired. |
| `UserInterests` | `MLController.GetUserInterests/Add/Remove`, `AiService.GetRecommendationsAsync` | Existing. |
| `usp_SaveComplaintEmbedding` | `MLController.SaveEmbedding` callback | Existing — can be reused or replaced with EF `Add/Update`. |
| `usp_SaveComplaintTags` | `MLController.SaveTags` | Existing — same. |
| `usp_UpsertRecommendationCache` | `MLController.SaveRecommendationCache` | Existing. |
| `usp_SaveAIDecision` | `MLController.LogAIDecision` | Existing. |
| `Complaints` + `IssueCategory` + `Localities` + `Departments` + `ComplaintStatusTimeline` | Read by `AiService.FetchComplaintContextAsync` (chatbot) and `FetchTrendStatsAsync` (forecast) | Read-only; SESSION_CONTEXT must be set — covered by `JwtSessionContextMiddleware` for HTTP path. Background worker already pins `SuperAdmin` (see `AIPendingQueueProcessor.cs:118-124`); replicate that if any read paths run outside an HTTP scope. |

---

## 7. Conflict / hazard register

| # | Hazard | Mitigation |
|---|---|---|
| 1 | `CloudinaryDotNet` pulls `Newtonsoft.Json`. The codebase uses `System.Text.Json`. | Keep namespaces isolated — never serialize Cloudinary responses with `System.Text.Json` reflection over their types. We only read `result.SecureUrl.ToString()`. |
| 2 | `Newtonsoft.Json` version pinned by `CloudinaryDotNet` may clash with QuestPDF transitive `Newtonsoft.Json`. | `dotnet build` after the package add will surface NU1605 if so. Resolve by central package management or explicit `Newtonsoft.Json` 13.x reference. |
| 3 | `AiService` scoped vs `AIPendingQueueProcessor` singleton — captive dependency. | Documented in `migration_plan.md` §6. Resolve via `IServiceScopeFactory.CreateScope()` per iteration. Existing code already does this for `DbContext`. |
| 4 | Gemini free tier: 15 RPM / 1M tokens-per-day. Background `ScoreComplaintAsync` is deterministic and **does not call Gemini** (per CLAUDE_CODE_GUIDE §12), so this is fine. `AutoTagAsync` does call Gemini and must rate-limit (`Task.Delay(1000)` between items per guide). | Apply `Task.Delay(1000)` in any batch loop. Add Polly retry policy with exponential backoff (1s → 2s → 4s) for 429 responses. |
| 5 | OpenAI moderation rate limits. | Wrap call with Polly retry policy. Fail-open if exceeded. |
| 6 | Cloudinary "private" type requires signed URLs to view. Angular today binds `[href]="att.filePath"` directly. If `filePath` becomes a private Cloudinary URL, the link will 401 without a signed URL. | Phase 2 must either (a) upload as `Type = "upload"` (public, longer-lived) accepting the trade-off, or (b) add a `ServeImage` redirect endpoint and update Angular to call it. Pick (b) — matches the guide §5.3 and preserves the "no public image URL" property the HANDOVER promised. |
| 7 | Removing `AIService:` config without removing `Program.cs` Polly retry around `MLServiceClient` causes startup to fail. | Phase 8 sequence: remove controller injections → remove `AddHttpClient<MLServiceClient>` → only then remove config section. |
| 8 | `JsonNamingPolicy.SnakeCaseLower` is configured inside `MLServiceClient` for the Python wire. Gemini and OpenAI APIs use snake_case too in their request bodies (`generationConfig`, `text-moderation-latest`). Confirm in code. | `AiService` uses anonymous objects with the exact snake_case fields the APIs expect; no global serializer change needed. |
| 9 | `MaxDepth = 128` set in `Program.cs:190` is a defensive measure for entity cycle protection. Adding `AiService` does not affect this. | None. |
| 10 | EF Core query tracking is `NoTracking` by default. Methods that need `Add/Update` on `ComplaintEmbeddings` must explicitly use `AsTracking()` or call `SaveChangesAsync` on a newly created entity (which EF tracks regardless). | `AiService.SaveEmbeddingAsync` uses `FindAsync` → returns tracked entity → property mutation works. Verify no `AsNoTracking()` slips in. |

---

## 8. Test-environment dependencies (pre-Phase 3)

- SQL Server LocalDB instance with `FixMyCityDB` already created and seeded
  (run `Database/FixMyCityDB_Sprint2_FIXED.sql` + patches once).
- `appsettings.Development.json` populated with: Gemini key (Google AI Studio,
  free), OpenAI key (free moderation tier), Cloudinary credentials (free tier
  plenty for dev).
- For Phases 1-3 to be testable without leaving the Python service running,
  the Python service must remain optionally bootable so we can A/B compare.
  Keep `docker-compose.yml` working.
