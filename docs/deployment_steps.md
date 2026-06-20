# deployment_steps.md

How to take this migration from a green local build to dev → staging → prod.
Optimised for a single-environment Azure-or-equivalent deployment of the
.NET 8 API + Angular 15 SPA + SQL Server.

---

## 1. Pre-flight (one-time)

1. **Sign up for the free-tier providers** (or confirm existing accounts):
   - Google AI Studio → create an API key for `gemini-1.5-flash`. Note the
     key. Free tier is 15 RPM / 1M tokens/day.
   - OpenAI → confirm moderation endpoint access. Free.
   - Cloudinary → create a cloud, note `CloudName`, `ApiKey`, `ApiSecret`.
     Create a folder `fixmycity/complaints` if you want to lock the upload
     prefix. Free tier covers ~25 GB storage.

2. **Decide the secret store.** Options:
   - **Env vars** (simplest): `Gemini__ApiKey`, `OpenAI__ApiKey`,
     `Cloudinary__CloudName`, `Cloudinary__ApiKey`, `Cloudinary__ApiSecret`.
   - **`appsettings.Production.json`** committed empty + secrets injected at
     deploy time.
   - **Azure Key Vault** (most secure, future).

3. **Cap external quotas.** In Google Cloud Console, set a daily token cap
   on the Gemini key. In Cloudinary, set transformation/bandwidth alerts.

---

## 2. Staging deploy

### 2.1 Backend (FixMyCity.API)

```powershell
# From the FixMyCity.API directory
dotnet publish -c Release -o ./publish
# Copy ./publish to the deployment target (App Service, Linux VM, etc.)
```

Set environment variables on the host:

```text
ConnectionStrings__DefaultConnection = <staging SQL Server connection string>
Jwt__Secret                          = <new 256-bit base64 secret; do NOT reuse dev>
Gemini__ApiKey                       = <Google AI Studio key>
OpenAI__ApiKey                       = <OpenAI key>
Cloudinary__CloudName                = <cloud>
Cloudinary__ApiKey                   = <key>
Cloudinary__ApiSecret                = <secret>
Cloudinary__UploadFolder             = fixmycity/complaints
Razorpay__KeyId                      = <test key>
Razorpay__KeySecret                  = <test secret>
ASPNETCORE_ENVIRONMENT               = Staging
```

Confirm:
- `dotnet --info` shows .NET 8 runtime present on host.
- Container/process can resolve outbound HTTPS to
  `generativelanguage.googleapis.com`, `api.openai.com`, `api.cloudinary.com`,
  `res.cloudinary.com`.
- SQL connection works (run `SELECT TOP 1 * FROM Users` from the host).

### 2.2 Database

The migration introduces **no schema changes**. If your staging DB is
already at the same revision as dev, do nothing. Otherwise re-apply the
existing scripts in order:

```sql
-- Database/FixMyCityDB_Sprint2_FIXED.sql
-- Database/AI_Tables_Addition.sql
-- Database/DB_Patch.sql
```

Verify the SPs `usp_SaveComplaintEmbedding`, `usp_SaveComplaintTags`,
`usp_UpsertRecommendationCache`, `usp_SaveAIDecision` all exist:

```sql
SELECT name FROM sys.procedures
WHERE name IN (
  'usp_SaveComplaintEmbedding',
  'usp_SaveComplaintTags',
  'usp_UpsertRecommendationCache',
  'usp_SaveAIDecision');
```

All four must return.

### 2.3 Frontend (FixMyCityApp)

```bash
cd FixMyCityApp
npm ci                    # reproducible install
npm run build -- --configuration production
# Output goes to dist/. Serve from CDN, Nginx, or Azure Static Web Apps.
```

Update `src/environments/environment.prod.ts` so `apiBaseUrl` points at the
staging API host *before* `npm run build`. Confirm `index.html:39, 44`
still reference Leaflet CDN — vendor Leaflet locally if the staging
environment blocks `unpkg.com`.

### 2.4 First-boot smoke

Run through `testing_checklist.md` against staging. Critical sections:
- §1 Build & startup smoke
- §3 Complaint submission flow (with Cloudinary configured)
- §4 ML endpoints
- §5 Fail-open behaviour

If any §3.6 background AI step fails (no scores, no tags, no embeddings
within 30s), check API logs for `[AiService]` warnings. Common causes:
- Gemini key blocked by quota → check Google Cloud Console quota page.
- DB connection short-cycled (Service Bus → DI scope issue) → restart API,
  re-test.

---

## 3. Production deploy

Only after a full pass of `testing_checklist.md` against staging.

### 3.1 Cutover sequence

1. **Announce maintenance window** (5-10 min). FixMyCity has no formal
   downtime SLA; choose low-traffic window anyway.
2. **Take DB backup.** `BACKUP DATABASE FixMyCityDB TO DISK = '…/pre-ai-cutover.bak'`.
3. **Stop the Python ml_service** if still running anywhere (`docker
   compose down` on any host). The new API doesn't talk to it but a
   forgotten instance can confuse ops.
4. **Deploy the .NET API** to production using the same recipe as §2.1
   with `ASPNETCORE_ENVIRONMENT=Production` and production secrets.
5. **Deploy the Angular bundle.** Cache-bust by ensuring the build hash
   changes (Angular CLI does this automatically).
6. **Smoke test (5 min).**
   - Login as a known prod citizen, view dashboard.
   - Submit a complaint with a small photo. Confirm Cloudinary upload,
     AI category suggestions appear.
   - Chatbot returns sensible reply.
   - Check `ComplaintMlscores` for the new row within 30s.

### 3.2 Rollback

If anything fails in the 5-min smoke:

1. Re-deploy the previous build of `FixMyCity.API` (binary + previous
   `appsettings.json` containing the `AIService` section).
2. Re-deploy previous Angular bundle.
3. Optionally start the Python `ml_service` if it was retired from infra.
   The new API doesn't talk to it; the old API does.
4. No DB rollback needed — schema unchanged.

Decision tree for choosing rollback vs forward-fix:

```
Backend boots, Gemini calls 401/403          → forward-fix key, no rollback.
Backend boots, every AI call returns empty   → forward-fix DI registration; check logs.
Backend doesn't boot                          → rollback immediately.
Photos won't upload                           → check Cloudinary creds; disk fallback should keep things alive.
Photos won't render                            → check ServeImage path; could be a CSP block.
Chatbot answers "temporarily unavailable"    → degraded, not failure; forward-fix later.
ComplaintMLScores not populating              → background task crashed; check logs; queue retry will catch up.
```

---

## 4. Post-deploy follow-ups (week 1)

- **Day 1-2.** Tail `[AiService]` warnings. Expect occasional 429 if a
  large submission burst hits Gemini free tier.
- **Day 3.** Re-embed historical complaints (R13):
  ```sql
  -- Find rows needing re-embed
  SELECT TOP 1000 ComplaintId FROM ComplaintEmbeddings
  WHERE ModelVersion <> 'google-text-embedding-004'
  ORDER BY GeneratedAt;
  ```
  A one-shot console runner (TBD) walks these and calls
  `AiService.GetEmbeddingAsync` / `SaveEmbeddingAsync` with
  `Task.Delay(1000)` between items.
- **Day 4.** If 429s observed in §Day 1, wire Polly retry around the
  Gemini client (R4').
- **Day 7.** Sign-off review. If green:
  - Delete `FixMyCity.AI/` directory.
  - Delete `FixMyCityUploads/` once Cloudinary is the verified sole image
    store and all historical disk-resident `filePath` values resolve via
    `ServeImage`.
  - Refresh `HANDOVER.md` / `FINAL_PROJECT_HANDOVER.md` to reflect the
    actual stack (Gemini + OpenAI + Cloudinary + Leaflet, no Azure AI).

---

## 5. Operational watchlist (long term)

| Signal | Where | Action |
|---|---|---|
| `[AiService] CheckDuplicatesAsync failed` | API logs | Gemini key or quota issue. Verify in Google Cloud Console. |
| Cloudinary `[CloudinaryService] Not configured` | API logs | Creds missing — uploads silently fall back to disk. Investigate before disk fills. |
| `AIPendingScoreQueue.AttemptCount` ≥ 5 | DB | Background scoring permanently failing for those rows. Inspect, manually re-run, or accept. |
| 429 from `generativelanguage.googleapis.com` | API logs | Rate-limit hit. Add Polly retry (R4'). |
| Daily Gemini cost > $10 | Google Cloud Console | Capacity check; reduce prompt sizes or batch better. |
| Cloudinary bandwidth >80% of free tier | Cloudinary dashboard | Upgrade plan or move to a different CDN. |

---

## 6. Disaster recovery quick reference

- **Lost API host.** Re-deploy from CI artifact. Secrets re-injected from
  vault. SQL DB unchanged. Cloudinary state unchanged.
- **Lost SQL.** Restore from automated backup. All AI artifacts
  (`ComplaintEmbeddings`, `ComplaintTags`, `ComplaintMlscores`) are
  regenerable from Complaints + Gemini — schedule a re-embed/re-score job
  after restore.
- **Lost Cloudinary account.** New photos stop uploading; existing photo
  references in `ComplaintAttachments.filePath` 404. Disk fallback path
  resumes immediately. Migrating Cloudinary content to another provider is
  a separate project.
- **Lost Gemini access.** AI features degrade open. The platform stays
  functional. Spin up a fallback provider (OpenAI chat completions, or
  rehydrate the Python `ml_service` from `FixMyCity.AI/`) once the outage
  is confirmed long-term.
