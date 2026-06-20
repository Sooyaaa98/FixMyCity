# remaining_risks.md

Risks that survive into post-migration steady state, plus the ones that
closed out during the migration. Mirrors the categories in `risk_analysis.md`.

---

## 1. Closed (mitigated by code in this migration)

| ID | Title | Resolution |
|---|---|---|
| R3 | Secret leakage via `appsettings.json` | `appsettings.json` carries only placeholders. Real values live in `appsettings.Development.json` (gitignored) or env vars. |
| R5 | Wire-contract drift between `AiService` output and Angular | Every `AiService` method returns either a typed result class with PascalCase properties or an anonymous object with explicit camelCase property names. `JsonNamingPolicy.CamelCase` in `Program.cs:184` normalises the wire shape. Manually re-verified against `ml.service.ts` for every wired endpoint. |
| R6 | Background worker captures scoped `DbContext` in a fire-and-forget lambda | `ComplaintController.SubmitComplaint` injects `IServiceScopeFactory`; the `Task.Run` body creates its own scope. Same pattern preserved in `AIPendingQueueProcessor`. |
| R7 | `MLServiceClient` removed prematurely | `grep -rn MLServiceClient` returns only `using`-free file references in Phase-9 docs; the type is gone, all controllers compile. |
| R10 | `Newtonsoft.Json` ↔ `System.Text.Json` clash via `CloudinaryDotNet` | `dotnet restore` + `dotnet build` produced no NU1605. No explicit pin needed. |

---

## 2. Open — high priority (carry into next sprint)

### R1' — Image-link rendering depends on `ServeImage` redirect

**Status.** Mitigation shipped (`ServeImage` redirect endpoint added), but
the new code path has not been validated against every Angular consumer.

**Action.** During the testing pass (`testing_checklist.md` §3.3, §3.4),
confirm every `<img>` and `<a href="att.filePath">` that previously rendered
disk basenames still renders when `filePath` is a Cloudinary URL. If any
binding pulls the URL into a CSS background-image or a download link, that
path also needs `ServeImage` indirection.

**Rollback.** Clear Cloudinary creds, fall back to disk uploads. Existing
photos keep working because `ServeImage` 302's pass-through unknown paths.

---

### R2' — RLS silent failure outside HTTP scope

**Status.** Background path (`AIPendingQueueProcessor`) explicitly sets
`SESSION_CONTEXT('UserRole', 'SuperAdmin')` before reading the queue.
`AiService` methods called from this worker (`ScoreComplaintAsync`,
`AutoTagAsync`, `CheckDuplicatesAsync`) do their own SQL through the same
DbContext — they inherit the session context.

**Open question.** If RLS is later enabled on `Complaints` (currently
`STATE = OFF` per the SQL script comment), the `_db.Complaints` reads inside
`AiService.FetchComplaintContextAsync` and `FetchTrendStatsAsync` and
`GetRecommendationsAsync` will need a session-context pin when invoked
outside an HTTP scope. They are HTTP-invoked today, so this is latent.

**Action.** Before enabling RLS in production, add an
`EnsureBackgroundSessionAsync()` helper to `AiService` and call it at the top
of every non-HTTP entry point.

---

### R4' — Gemini free-tier rate limits (15 RPM / 1M tokens/day)

**Status.** No Polly retry policy is wired around the Gemini calls in
`AiService`. A burst of 16+ submissions in a minute will produce silent
429-failure → empty-suggestions / "temporarily unavailable" replies.

**Action.** Add a named `HttpClient` with a Polly retry policy
(`HandleTransientHttpError().OrResult(r => (int)r.StatusCode == 429)`,
3 retries, exponential 1s/2s/4s). Resolve from `AiService` via
`_http.CreateClient("gemini")`. In background batch loops
(`AutoTagAsync` per-complaint), insert `await Task.Delay(1000)` to stay
under 15 RPM.

---

### R8' — OpenAI moderation accepted as silent fail-open

**Status.** Unchanged from `risk_analysis.md`. Behaviour was identical pre-
migration (Python service was also fail-open). Documented as accepted policy.

**Action.** None required. If toxic content slips through and becomes an
incident, change `CheckToxicityAsync` to return `isToxic = true` on auth/
quota errors (fail-closed). Tradeoff: legitimate citizens get blocked when
OpenAI is down.

---

## 3. Open — medium priority

### R9' — Cloudinary signed-URL expiry leaks into PDF certificates

**Status.** Not actively triggered yet because certificates are generated
on demand at view time. If a user downloads a PDF and reopens it more than
an hour later, embedded private-URL images will 401.

**Action.** When `QuestPdfService` embeds a complaint image into a PDF,
either (a) fetch the image bytes via `CloudinaryService.GetSignedUrl`,
download, embed the bytes; or (b) generate a long-lived signed URL (24h+).
Tracked outside this migration.

---

### R12' — Forecast performance / cost

**Status.** Each `GetForecastAsync` call runs 3 grouping queries + 1 Gemini
call. With 200 seeded complaints it's <1s end-to-end. At 50k+ rows the SQL
side still scales; the Gemini call still costs ~2-5s.

**Action.** Add `IMemoryCache` keyed by `(categoryId, periods)` with 5-min
TTL inside `GetForecastAsync`. Until then, throttle by hand — the admin
dashboard doesn't poll, so this is acceptable for MVP.

---

### R13' — Embedding model version drift in historical data

**Status.** Mitigated for new traffic — `EmbeddingModelVersion =
"google-text-embedding-004"` is set on every new write and filtered on every
read.

**Action.** Old Python-era embeddings (384-dim sentence-transformer
vectors) remain in `ComplaintEmbeddings` and are ignored by dedup /
recommendations. To make them count, run a one-shot batch re-embed job
(walk complaints where `ModelVersion != current`, recompute via
`AiService.GetEmbeddingAsync`, `SaveEmbeddingAsync`). Insert
`Task.Delay(1000)` between calls to respect Gemini limits. Tracked outside
this migration.

---

### R16' — DBSCAN geo-clustering gone

**Status.** `GetGeoClusters` returns an empty cluster list. The admin
heatmap still renders individual complaint markers via the existing
client-side `renderMarkers` path, so the map is functional but lacks the
density-aware cluster halos the Python service produced.

**Action.** Either accept (the heatmap circles already convey density) or
port a simple `MeanShift`/`grid` cluster to C#. Out of this migration's
scope.

---

## 4. Open — low priority

### R11 — Leaflet CDN unreachable during demo

Unchanged. Vendor Leaflet into `src/assets/` if your demo environment
blocks `unpkg.com`. Out of scope.

### R14 — Python project files on disk

`FixMyCity.AI/` and `FixMyCityUploads/` remain on disk for one verification
cycle. Delete in a follow-up cleanup commit.

### R15 — `AIServiceKeyMiddleware` file

Deleted in Phase 8. The five callback endpoints are also gone. No further
action.

### R16-cosmetic — `HANDOVER.md` / `FINAL_PROJECT_HANDOVER.md`

Still talks about "8 Azure AI services". Documentation pass to align with
reality (Gemini + OpenAI + Cloudinary, no Azure) is a separate
documentation task.

---

## 5. New post-migration risks

### N1 — Cloudinary free-tier quota

Free Cloudinary plans cap at ~25 GB storage / 25 GB monthly bandwidth /
500 transformations per month. A medium-traffic deployment will hit one of
those limits and uploads will start failing. Cloud-side errors fall through
to the disk path in `UploadComplaintImage`, so no data loss — but new
images won't appear in Cloudinary until the limit resets or you upgrade.
Monitor `CloudinaryService` warning logs.

### N2 — Gemini quota billing surprise

If you upgrade past the free tier, costs scale with token count. The
chatbot prompt + DB context can hit 1-2k tokens per message; image
analysis adds the base64-encoded image. Set a project-level quota cap in
Google Cloud Console before going public.

### N3 — `[AllowAnonymous]` on `ServeImage`

Anyone with a Cloudinary URL can fetch the image. Private complaint photos
should be access-controlled. Acceptable for MVP; for production add a
JWT check + complaint-id-to-user authorization before issuing the signed
redirect.

### N4 — Deterministic scorer drifts from civic reality

`ScoreComplaintAsync` uses fixed weights (`AiScoring` config). If the
weights aren't tuned against real outcomes, the priority dashboard will
keep ranking the same kind of complaint high. Schedule a quarterly review
of `AiScoring` parameters against resolved-complaint data.

---

## 6. Closure schedule

Suggest a one-week post-deploy review:
- Day 1-3: complete `testing_checklist.md`.
- Day 4: backfill R13 (one-shot re-embed of historical complaints).
- Day 5: tune R4 (Polly retry + Task.Delay) if any 429 hits observed.
- Day 7: clean up `FixMyCity.AI/` and update HANDOVER docs.
