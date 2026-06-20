# implementation_order.md — Phase 1 Audit

The exact commit-by-commit sequence to take the repo from its current
"Python ml_service + local disk + Leaflet CDN" state to the target
"AiService + Cloudinary + Leaflet CDN" state, with no working feature
broken at any commit boundary.

**Rule of thumb:** every commit ends with `dotnet build` green and the app
boots. Phase boundaries also end with a manual smoke test from
`testing_checklist.md`.

---

## Phase 1 — Foundation (additive, no behaviour change)

> Goal: app boots with the new packages and DI registrations, behaves
> identically to today. Confidence-building phase.

**Commit 1.1 — `chore: add CloudinaryDotNet package + AI/Cloudinary config keys`**
- `FixMyCity.API.csproj`: add `CloudinaryDotNet` 1.*.
- `appsettings.json`: add `Gemini`, `OpenAI`, `Cloudinary`, `AiScoring` sections
  with placeholder strings.
- `appsettings.Development.json`: real keys (file is gitignored).
- Verify: `dotnet restore && dotnet build` → success. App starts with
  `dotnet run`, swagger loads, login still works.

**Commit 1.2 — `feat: scaffold AiService + CloudinaryService (skeletons)`**
- New file `Services/AiService.cs` with class skeleton, constructor,
  config properties, and the three helpers from CLAUDE_CODE_GUIDE §4.2
  (`GeminiGenerateAsync`, `GetEmbeddingAsync`, `CosineSimilarity`,
  `StripFences`). No public feature methods yet — only health.
- Add `IsHealthyAsync()` (pings Gemini; returns `false` on any failure).
- New file `Services/CloudinaryService.cs` per CLAUDE_CODE_GUIDE §5.1.
- `Program.cs`: register `AddHttpClient()`, `AddSingleton<CloudinaryService>()`,
  `AddScoped<AiService>()`. Do **not** wire these into controllers yet.
- Verify: `dotnet build` green. `dotnet run` → swagger loads. No controller
  behaviour changed.

✅ **Phase 1 exit gate:** existing manual smoke test still passes
(login → submit complaint → see it on the list → chatbot replies via Python).

---

## Phase 2 — Cloudinary image upload

> Goal: photo uploads land in Cloudinary instead of disk; existing photos
> stay readable via fallback. Angular response shape unchanged.

**Commit 2.1 — `feat(api): cloudinary upload path with disk fallback`**
- `Controllers/ComplaintController.cs`: inject `CloudinaryService`. Inside
  `UploadComplaintImage`, branch on
  `string.IsNullOrWhiteSpace(_config["Cloudinary:CloudName"])`:
  - empty → existing disk write (current behaviour).
  - configured → upload to Cloudinary as `Type = "private"`, set `filePath`
    to the secure URL.
- Response shape unchanged: `{ success, filePath, fileName, fileSizeKB }`.
- Verify: with Cloudinary creds in `appsettings.Development.json`, submit a
  complaint with a photo → response `filePath` starts `https://res.cloudinary.com/`.
- With creds removed (env override), disk path still works.

**Commit 2.2 — `feat(api): ServeImage redirect endpoint for private cloudinary URLs`**
- `Controllers/ComplaintController.cs`: add `[HttpGet, AllowAnonymous]
  IActionResult ServeImage([FromQuery] string path)` that returns a 302 to
  `CloudinaryService.GetSignedUrl(path, 3600)`. Falls back to
  `Redirect(path)` for non-Cloudinary URLs (preserves disk-era links).
- Update Angular `<img src>` / `<a href>` bindings that point at
  `filePath` to route through `/api/Complaint/ServeImage?path=<encoded>`
  ONLY where the image is loaded by the browser directly. Most current
  bindings (e.g. `citizen-complaint-detail.component.html:185`) already
  use `[href]` and download via the user; redirect-on-click is fine.
- Verify: complaint-detail image renders both for new (Cloudinary) and
  pre-existing (disk basename) `filePath` values.

✅ **Phase 2 exit gate:** photo submission → upload → AnalyzeImage still
returns suggestions (via Python service, unchanged) → photo renders in
complaint-detail.

---

## Phase 3 — Add synchronous AI feature methods (no controller switching yet)

> Goal: `AiService` has working implementations of the three sync features
> the user-facing form needs. Controllers still call `_mlService`.

**Commit 3.1 — `feat(ai): AiService.AnalyzeImageAsync (gemini vision)`**
- Implement per CLAUDE_CODE_GUIDE §7. Output property names in camelCase
  (`suggestions`, `ocrText`, `gpsLat`, `gpsLon`, `suggestedDescription`).
  Suggestion items use `categoryId` / `categoryName` / `confidence` (NOT
  snake_case — see R5).
- Unit-smoke test: call from a temporary `[HttpGet] /api/ML/_TestAnalyze`
  action (gated by `IsDevelopment()`) and verify shape; then delete the
  test action.

**Commit 3.2 — `feat(ai): AiService.CategorizeTextAsync`**
- Same pattern as 3.1 but for text-only categorization (CLAUDE_CODE_GUIDE
  §17 note). Output `{ suggestions: [...], suggestedDescription }`.

**Commit 3.3 — `feat(ai): AiService.CheckToxicityAsync (openai moderation)`**
- Per CLAUDE_CODE_GUIDE §10. Fail-open: any exception returns
  `{ isToxic = false, categories = [], score = 0.0 }`.

**Commit 3.4 — `feat(ai): AiService.ChatAsync with DB context injection`**
- Per CLAUDE_CODE_GUIDE §9. `FetchComplaintContextAsync` uses EF on
  `Complaints` joined with `Category`, `Locality`, `Department`, and reads
  latest `ComplaintStatusTimeline` row.
- Verify: from a temporary dev-only endpoint, send `"complaint 1"` and
  confirm the reply quotes the seeded title.

✅ **Phase 3 exit gate:** `_aiService.IsHealthyAsync()` returns true with
real Gemini key. Controllers are unchanged, app behaves as before.

---

## Phase 4 — Switch read-side MLController actions

> Goal: each user-facing AI feature now backed by Gemini instead of Python,
> one action at a time. Rollback granularity is one commit per action.

**Commit 4.1 — `feat(ml): route CheckAIHealth through AiService`**
- `MLController.cs`: inject `AiService _aiService` *in addition to*
  `_mlService`. In `CheckAIHealth`, call `_aiService.IsHealthyAsync()`.
- Verify: `GET /api/ML/CheckAIHealth` returns `{ success: true,
  aiServiceOnline: true }`.

**Commit 4.2 — `feat(ml): route AnalyzeImage through AiService`**
- In `MLController.AnalyzeImage`, call `_aiService.AnalyzeImageAsync`.
  Keep wrapping `Json(new { success, result })` shape.
- Verify: submit-complaint form pre-fills category and description from
  an uploaded photo.

**Commit 4.3 — `feat(ml): route CategorizeText through AiService`**
- In `MLController.CategorizeText`, call `_aiService.CategorizeTextAsync`.
  Response stays `{ success, suggestions, suggestedDescription? }`.

**Commit 4.4 — `feat(ml): route Chat through AiService`**
- In `MLController.Chat`, call `_aiService.ChatAsync`. Response shape
  `{ success, reply }`.

**Commit 4.5 — `feat(ml): route CheckDuplicates through AiService`** (depends
on Phase 5.1 if we want real dedup — otherwise stub returning empty
candidates is fine here).

✅ **Phase 4 exit gate:** every Angular ML interaction now goes through
Gemini. Python service can be stopped during a 30-minute spot check; Angular
still works. Re-start Python at end of test.

---

## Phase 5 — Background / persistent AI features

> Goal: complaint submission scoring + tagging + dedup all flow through
> `AiService`. Worker no longer talks to Python.

**Commit 5.1 — `feat(ai): SaveEmbeddingAsync + CheckDuplicatesAsync`**
- Per CLAUDE_CODE_GUIDE §8. Stores embedding via EF (not the SP) — the SP
  remains callable for backward compatibility.
- Filter candidates by `ModelVersion = "google-text-embedding-004"` to avoid
  mixing model spaces (R13).
- Wire `MLController.CheckDuplicates` and `SaveEmbedding` actions.

**Commit 5.2 — `feat(ai): ScoreComplaintAsync + SaveMlScoresAsync`**
- Per CLAUDE_CODE_GUIDE §12. Pure deterministic algorithm, weights from
  `AiScoring` config section.
- Add `MLController.ScoreComplaint` action (currently absent — Python is
  fire-and-forget). Response `{ priorityScore, resolutionProbability,
  predictedResolutionDate, modelVersion }`.

**Commit 5.3 — `feat(ai): AutoTagAsync + wire SaveTags`**
- Per CLAUDE_CODE_GUIDE §11. Gemini call → 5 tags → write to `ComplaintTags`.
- `MLController.AutoTag` action and `SaveTags` (existing callback) unchanged
  in shape.

**Commit 5.4 — `refactor(worker): AIPendingQueueProcessor uses AiService`**
- In `ProcessQueueAsync`, also `scope.ServiceProvider.GetRequiredService<AiService>()`.
- Replace `mlService.ScoreComplaintAsync(...)` with `aiService.ScoreComplaintAsync(...)`.
- Keep the dead-letter / attempt-count behaviour. Keep the `MLServiceClient`
  resolve too (one safety net): only the call is swapped, the field stays.
- Verify: insert a row into `AIPendingScoreQueue` manually, wait 5 min,
  confirm `ComplaintMLScores` updated.

**Commit 5.5 — `refactor(api): SubmitComplaint inline AI calls via scope factory`**
- In `ComplaintController.SubmitComplaint`, replace the `Task.Run(...)`
  body to create a new `IServiceScope` and resolve `AiService` from it
  (mitigates R6). Move the `Score / Check / Tag` calls inside.
- Verify: end-to-end submit, then check `ComplaintMLScores`, `ComplaintTags`,
  `ComplaintEmbeddings`, `DuplicateComplaintLinks` populate within 30s.

✅ **Phase 5 exit gate:** stop the Python service entirely. Submit a
complaint with a photo. Within 30s: tags exist, score exists, embedding
exists, duplicates (if any seeded similar) flagged. Chatbot works.

---

## Phase 6 — Admin & advanced AI

**Commit 6.1 — `feat(ai): GetForecastAsync (admin trend narrative)`**
- Per CLAUDE_CODE_GUIDE §14. Cache result for 5 minutes in `IMemoryCache`
  per `(categoryId, periods)` key (mitigates R12).
- Wire `MLController.GetForecast`.

**Commit 6.2 — `feat(ai): GetRecommendationsAsync (vector + interest)`**
- Per CLAUDE_CODE_GUIDE §15. Filter `ComplaintEmbeddings` by model version
  (R13). Background re-embed remaining historical rows in 6.3 if needed.
- Wire `MLController.GetRecommendedComplaints` to call `AiService` (currently
  falls back from `_mlService` to SP — keep SP as the absolute last resort).

**Commit 6.3 — `feat(ai): GetPwgVerdictAsync`**
- Per CLAUDE_CODE_GUIDE §13. Add `MLController.GetPwgVerdict` action.
  Update `PWGController.ReviewPWGWork` to call it and include the verdict in
  its existing response (additive — no field removed).

**Commit 6.4 — `chore(data): batch re-embed historical complaints` (optional)**
- One-shot console runner or admin endpoint that walks `Complaints`
  whose `ComplaintEmbedding.ModelVersion != "google-text-embedding-004"`
  (or missing) and regenerates embeddings with a 1s sleep between calls.

✅ **Phase 6 exit gate:** admin dashboard shows AI narrative; PWG verdict
modal shows reason; recommendations list is non-empty for a citizen with at
least one interest.

---

## Phase 7 — Maps verification (no code change)

**No commit.** Manual confirmation only:
- Open submit-complaint form → map loads, click anywhere → coordinates
  captured. (Already works on `main` per `map-view.component.ts`.)
- Open admin dashboard → heatmap renders cluster circles.

If the demo target environment blocks `unpkg.com`, open a separate ticket
to vendor Leaflet into `src/assets/` (defer; not part of this migration).

---

## Phase 8 — Retire Python service

> Goal: delete the dead code path. Reach this only after a week of green
> Phase-6 production usage in the dev/staging environment.

**Commit 8.1 — `chore: stop calling MLServiceClient anywhere`**
- `grep -rn "MLServiceClient\|_mlService" FixMyCity.API/` must return zero
  matches. Remove the field declaration and ctor parameter from every
  controller that still has it (`MLController`, `ComplaintController`).
- Remove `builder.Services.AddHttpClient<MLServiceClient>(...).AddPolicyHandler(...)`
  block from `Program.cs`.
- Verify: build + boot + full smoke.

**Commit 8.2 — `chore: delete MLServiceClient.cs`**
- File deletion only. Build must still pass.

**Commit 8.3 — `chore: drop AIService config + AIServiceKeyMiddleware`**
- `appsettings.json`: remove `AIService` and `Uploads` sections (the latter
  if no remaining caller).
- `Program.cs`: remove the `app.UseWhen(... AIServiceKeyMiddleware)` block.
- Delete `Middleware/AIServiceKeyMiddleware.cs`.
- Verify: `/api/ML/SaveMLScores` and other former-callback endpoints either
  removed or now require JWT only (they're internal-only and unreachable
  from Angular — safe to remove the actions in a follow-up).

**Commit 8.4 — `chore: delete FixMyCity.AI/ Python project`**
- Optional — can defer to a separate cleanup PR.
- After this commit, `docker-compose.yml` references must be removed too.

✅ **Phase 8 exit gate:** repo no longer references Python AI in any path
the build sees. Manual smoke + automated tests (when they exist) all green.

---

## Phase 9 — Verification + final docs

> Goal: produce the four post-implementation documents the user asked for.

**Commit 9.1 — `docs: final migration summary, testing checklist, remaining risks, deployment steps`**
- Write `final_migration_summary.md` — what changed, by phase, with links to
  each commit.
- Write `testing_checklist.md` — the runbook used at every phase exit gate,
  collated into a single document for QA.
- Write `remaining_risks.md` — the post-migration risk register (carry over
  the items from `risk_analysis.md` that remain live; close the rest).
- Write `deployment_steps.md` — how to roll the change to staging then prod:
  env-var setup, Cloudinary preset creation, Gemini quota request, one-shot
  re-embedding job, rollback knob.

---

## Phase ordering quick reference

```
┌──────────────────────────────────────────────────────────────────┐
│ 1 Foundation         ──► 2 Cloudinary     ──► 3 AiService sync  │
│                                                       │           │
│  4 Switch readers ◄──────────────────────────────────┘           │
│   │                                                                │
│   ▼                                                                │
│ 5 Background features ──► 6 Admin features ──► 7 Maps (verify)   │
│                                                       │           │
│  8 Retire Python    ◄────────────────────────────────┘           │
│   │                                                                │
│   ▼                                                                │
│ 9 Verification + final docs                                       │
└──────────────────────────────────────────────────────────────────┘
```

Stops at any phase boundary are safe rollback points.

---

## Estimated effort (post-approval)

| Phase | Commits | Hours (skilled solo dev) |
|---|---|---|
| 1 | 2 | 1 |
| 2 | 2 | 2 |
| 3 | 4 | 3 |
| 4 | 5 | 2 |
| 5 | 5 | 4 |
| 6 | 4 | 3 |
| 7 | 0 | 0.5 (verification only) |
| 8 | 4 | 2 |
| 9 | 1 | 2 (docs) |
| **Total** | **27** | **~20 hrs** |
