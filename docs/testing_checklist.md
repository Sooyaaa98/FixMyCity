# testing_checklist.md

End-to-end verification runbook. Work through it once before considering the
migration "merged".

Legend: ☐ pending · ✅ passes · ⚠️ degraded · ❌ fails.

---

## 0. Prerequisites

- [ ] SQL Server LocalDB instance reachable at `(localdb)\\MSSQLLocalDB`.
- [ ] `FixMyCityDB` exists with schema + seed (`Database/FixMyCityDB_Sprint2_FIXED.sql`,
      `AI_Tables_Addition.sql`, `DB_Patch.sql`).
- [ ] `appsettings.Development.json` populated with real keys:
  - `Gemini:ApiKey` (Google AI Studio)
  - `OpenAI:ApiKey` (free moderation tier)
  - `Cloudinary:CloudName` / `ApiKey` / `ApiSecret`
  - Or each as env var (`Gemini__ApiKey`, `OpenAI__ApiKey`,
    `Cloudinary__CloudName`, …).
- [ ] `cd FixMyCity.API && dotnet run` boots cleanly. Swagger reachable at
      `https://localhost:7030/swagger` (or http://localhost:5065).
- [ ] `cd FixMyCityApp && npm start` boots Angular at http://localhost:4200.
- [ ] Python ml_service is **not** running. (`docker compose down` or kill
      `uvicorn`.) The new build must not depend on it.

---

## 1. Build & startup smoke

- [ ] `dotnet build FixMyCity.API` → 0 errors.
- [ ] App boots without `MLServiceClient` resolution errors.
- [ ] Swagger lists `POST /api/ML/GetPwgVerdict` and `GET /api/Complaint/ServeImage`.
- [ ] Swagger no longer lists `SaveMLScores`, `SaveEmbedding`, `SaveTags`,
      `SaveRecommendationCache`, `LogAIDecision` (those endpoints were removed
      with the Python integration).
- [ ] Logs show `[CloudinaryService] Not configured` if Cloudinary creds blank,
      else absent.

---

## 2. Authentication (regression — should still work)

- [ ] `POST /api/Auth/Login` with seeded user (`arjun.r@example.com` / `Password123!`)
      returns `{ success: true, token, refreshToken, role, user, expiresIn: 900 }`.
- [ ] Angular login page navigates to citizen dashboard.
- [ ] Refresh token rotation works (wait 15 min or force token expiry).

---

## 3. Complaint submission flow

### 3.1 Image upload — disk path (Cloudinary unconfigured)

- [ ] Leave `Cloudinary:CloudName` blank.
- [ ] Citizen submits a complaint with a JPG. Network tab shows
      `POST /api/Complaint/UploadComplaintImage` → 200 with
      `filePath: "complaint_<guid>.jpg"` (basename only).
- [ ] File appears on disk under `FixMyCityUploads/`.

### 3.2 Image upload — Cloudinary path (configured)

- [ ] Populate Cloudinary creds in `appsettings.Development.json`. Restart.
- [ ] Citizen submits a complaint with a JPG. Response `filePath` starts with
      `https://res.cloudinary.com/<cloud>/image/private/…`.
- [ ] Cloudinary dashboard shows the file under `fixmycity/complaints/`.

### 3.3 ServeImage redirect

- [ ] Browse to `/api/Complaint/ServeImage?path=<encodedCloudinaryUrl>` →
      302 to a signed Cloudinary URL that returns the image bytes (~1h expiry).
- [ ] Browse to `/api/Complaint/ServeImage?path=complaint_xyz.jpg` (legacy
      basename) → 302 to `complaint_xyz.jpg`. Renders if static-files
      pipeline is configured; 404 otherwise — confirms the legacy story.

### 3.4 AI pre-fill on image upload

- [ ] After upload, Angular calls `POST /api/ML/AnalyzeImage` with the
      returned `filePath`. Response is `{ success: true, result: { complaintId,
      suggestions: [{categoryId, categoryName, confidence}], ocrText, gpsLat:
      null, gpsLon: null, suggestedDescription: "…" } }`.
- [ ] Submit-complaint form pre-fills category dropdown and description.

### 3.5 Toxicity gate

- [ ] Submit a complaint with obviously abusive text → `POST /api/Complaint/SubmitComplaint`
      returns `{ success: false, message: "Complaint not submitted: …" }`.
- [ ] Submit normal complaint → 200 with `complaintId > 0`.
- [ ] Disable `OpenAI:ApiKey` (clear the value). Repeat abusive submission →
      now passes through (fail-open behaviour, R8 documented).

### 3.6 Background AI (post-submit)

After successful submission, within ~30 seconds:

- [ ] `SELECT * FROM ComplaintMlscores WHERE ComplaintId = <new>` → 1 row,
      `PriorityScore` between 0-100, `PredictionModelVersion = 'v2-scoring-dotnet'`.
- [ ] `SELECT * FROM ComplaintTags WHERE ComplaintId = <new>` → 5 rows (Gemini
      may produce fewer if it returns a short JSON; ≥ 1 is acceptable).
- [ ] `SELECT * FROM ComplaintEmbeddings WHERE ComplaintId = <new>` → 1 row,
      `ModelVersion = 'google-text-embedding-004'`, `EmbeddingJson` a JSON array
      of 768 floats.
- [ ] Submit a similar complaint in the same locality → server-side
      `CheckDuplicatesAsync` flags it (visible in the next dedup check).

### 3.7 Background retry queue

- [ ] Stop the API. Manually insert a row into `AIPendingScoreQueue` for an
      existing complaint that has no `ComplaintMlscores` row.
- [ ] Start the API. Wait up to 5 minutes. Worker should pick it up, write a
      score, delete the queue row. Log line: `Retry scoring succeeded for
      complaint {id}.`.

---

## 4. ML endpoints (Angular smoke)

For each, open browser dev-tools and confirm the shape exactly matches the
"Wire shape" column.

| Action in UI | Endpoint | Wire shape |
|---|---|---|
| Open chatbot, ask "what is FixMyCity?" | `POST /api/ML/Chat` | `{ success: true, reply: "…" }` |
| In chatbot, ask "tell me about complaint 1" | same | `reply` references the seeded complaint #1's title and status |
| Citizen interests page → toggle a category | `POST /api/ML/AddUserInterest` then `GET GetUserInterests` | `IUserInterest[]` |
| Submit form → type text without photo → press "Suggest description" | `POST /api/ML/CategorizeText` | `{ success: true, suggestions: [{categoryId, categoryName, confidence}], suggestedDescription }` |
| Submit form → potential duplicate check | `POST /api/ML/CheckDuplicates` | `{ success: true, result: { complaintId, candidates: [{complaintId, similarity, isDuplicate}], embeddingStored } }` |
| Citizen dashboard → recommendations | `GET /api/ML/GetRecommendedComplaints` | `[{ complaintId }]` |
| Admin → trend dashboard | `POST /api/ML/GetForecast` | `{ success: true, result: { narrative, trend, hotspots } }` |
| Admin → "Trigger retrain" button | `POST /api/ML/TriggerRetrain` | `{ success: true, message: "Retraining is no longer required…" }` |
| Solver → "Review PWG work" → "AI verdict" | `POST /api/ML/GetPwgVerdict` | `{ success: true, result: { verdict: "APPROVE"\|"REDO", reason, confidence } }` |
| Anywhere → AI health badge | `GET /api/ML/CheckAIHealth` | `{ success: true, aiServiceOnline: true }` when Gemini key present, `false` when blank |

---

## 5. Fail-open behaviour (negative tests)

- [ ] Clear `Gemini:ApiKey`. Restart. `CheckAIHealth` returns
      `{ aiServiceOnline: false }`. Submit complaint still succeeds.
      `AnalyzeImage` returns empty `suggestions`. Chat replies "temporarily
      unavailable". No 5xx.
- [ ] Clear `OpenAI:ApiKey`. Restart. Submit toxic complaint → passes through
      (fail-open). No 5xx.
- [ ] Clear Cloudinary creds. Upload still works (disk fallback).
- [ ] Block outbound HTTPS to `generativelanguage.googleapis.com` via firewall.
      Submit complaint → still saved. Background scoring still runs
      (deterministic). Tag/embedding skipped silently.

---

## 6. RLS regression

- [ ] Log in as citizen A. Submit a complaint. Note its ID.
- [ ] Log out. Log in as citizen B. Hit
      `GET /api/Complaint/GetComplaintById/<A's id>` → 404 or empty.
- [ ] Log in as Solver of the routed department. Same endpoint → returns
      the complaint. (Confirms `SessionContextInterceptor` still injects
      `SESSION_CONTEXT('UserId')`.)

---

## 7. Pagination, rate limit, security headers (regression — should still work)

- [ ] Hit `POST /api/Auth/Login` 11 times in one minute from one IP → 11th
      returns 429 (`login` limiter, see `Program.cs:152-158`).
- [ ] Inspect any response → `X-Content-Type-Options: nosniff`,
      `X-Frame-Options: DENY` headers present
      (`SecurityHeadersMiddleware`).

---

## 8. Map (Phase 7)

- [ ] Open the submit-complaint page → map tile loads from
      `https://*.tile.openstreetmap.org/…`. Marker click sets lat/lng.
- [ ] Admin dashboard heatmap → semi-transparent circles overlay complaint
      density.

---

## 9. Sign-off

When every box above is checked or has an explicit ⚠️ / ❌ explanation,
write a one-paragraph summary in this section and consider the migration
shippable to staging.

Date: ______________
Operator: ______________
Result: ☐ pass · ☐ pass with degradations (list) · ☐ fail (block ship)
