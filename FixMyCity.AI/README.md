# FixMyCity — AI/ML Integration Guide

## What This Package Contains

```
FixMyCity.AI/
├── sql/
│   └── AI_Tables_Addition.sql       New DB tables + stored procedures
├── ml_service/                      Python FastAPI AI microservice
│   ├── main.py                      Entry point (all routers, security, startup)
│   ├── config.py                    All settings (env vars with defaults)
│   ├── requirements.txt
│   ├── Dockerfile
│   ├── services/
│   │   ├── database.py              Read-only SQL Server helpers for training
│   │   ├── model_manager.py         Singleton model store (all ML models in memory)
│   │   └── notifier.py              POSTs results back to .NET API
│   └── routers/
│       ├── scoring.py               Priority score + resolution prediction (LightGBM)
│       ├── duplicates.py            Semantic duplicate detection (cosine similarity)
│       ├── categorization.py        Text KNN + CLIP image classification + OCR + toxicity
│       ├── recommendations.py       ALS collaborative filtering + content fallback
│       ├── analytics.py             KeyBERT tagging + DBSCAN geo-clustering + Prophet forecast
│       ├── chatbot.py               Ollama RAG chatbot (llama3.2:3b)
│       └── training.py              POST /ai/train — retrains all models
└── dotnet/                          Changes to apply to your FixMyCity.API project
    ├── Middleware/
    │   └── AIServiceKeyMiddleware.cs  Secures /api/ML/* write endpoints
    ├── Services/
    │   └── MLServiceClient.cs         HTTP client for Python AI service
    ├── Models/
    │   └── AIModels.cs                New request/response models
    ├── Controllers/
    │   ├── MLController.cs            Complete replacement (new AI endpoints added)
    │   └── ComplaintController_AIChanges.cs  Shows what changed in ComplaintController
    ├── Program_Updated.cs             Complete replacement Program.cs
    └── appsettings_withAI.json        New config keys to add
```

---

## Step 1 — Run the SQL

```sql
-- Connect to FixMyCityDB, then:
-- Run: sql/AI_Tables_Addition.sql
```

---

## Step 2 — Start the Python AI Service

**Option A: Direct (development)**
```bash
cd ml_service
pip install -r requirements.txt
# If using CPU-only (no NVIDIA GPU):
pip install torch==2.3.0 --index-url https://download.pytorch.org/whl/cpu
uvicorn main:app --host 0.0.0.0 --port 8001 --reload
```

**Option B: Docker Compose (recommended)**
```bash
docker-compose up -d
# Pull the LLM for the chatbot (one-time, ~2 GB):
docker exec fixmycity-ollama ollama pull llama3.2:3b
```

Verify it's running:
```bash
curl http://localhost:8001/health
# → {"status":"ok","sentence_model":true,"clip_model":false,...}
```

---

## Step 3 — Apply .NET Changes

Copy these files into your `FixMyCity.API` project:

| Source file | Destination |
|---|---|
| `dotnet/Middleware/AIServiceKeyMiddleware.cs` | `FixMyCity.API/Middleware/AIServiceKeyMiddleware.cs` (new folder) |
| `dotnet/Services/MLServiceClient.cs` | `FixMyCity.API/Services/MLServiceClient.cs` (new folder) |
| `dotnet/Models/AIModels.cs` | `FixMyCity.API/Models/AIModels.cs` |
| `dotnet/Controllers/MLController.cs` | Replace `FixMyCity.API/Controllers/MLController.cs` |
| `dotnet/Controllers/ComplaintController_AIChanges.cs` | Apply the changes to `ComplaintController.cs` (see file header) |
| `dotnet/Program_Updated.cs` | Replace `FixMyCity.API/Program.cs` (rename to Program.cs) |
| `dotnet/appsettings_withAI.json` | Merge the `AIService` section into `appsettings.json` |

---

## Step 4 — Configure the Shared Secret

Set **the same key** in both places:

**appsettings.json** (or Azure App Service config):
```json
"AIService": {
  "BaseUrl": "http://localhost:8001",
  "ServiceKey": "your-strong-secret-key-here"
}
```

**Python service** (env var or `.env` file):
```bash
AI_SERVICE_KEY=your-strong-secret-key-here
DOTNET_API_KEY=your-strong-secret-key-here
DOTNET_API_BASE=http://localhost:5183
```

---

## Step 5 — Train Initial Models

```bash
# Trigger training via admin dashboard or directly:
curl -X POST http://localhost:8001/ai/train \
     -H "X-AI-Service-Key: your-strong-secret-key-here"

# Response:
# {"success":true,"lgbm_trained":false,"knn_trained":false,...}
# (false = not enough data yet — rule-based fallback active)
```

The system runs on **rule-based scoring** until you have enough data:
- **KNN Categorizer**: needs ≥ 30 complaints
- **LightGBM Resolution**: needs ≥ 100 resolved complaints  
- **ALS Recommender**: needs ≥ 50 user interactions

---

## API Endpoints Reference (AI additions)

| Method | Endpoint | Description | Caller |
|---|---|---|---|
| POST | `/api/ML/SaveMLScores` | Save AI scores | Python AI service |
| POST | `/api/ML/LogAIDecision` | Log AI decision | Python AI service |
| POST | `/api/ML/SaveEmbedding` | Save complaint embedding | Python AI service |
| POST | `/api/ML/SaveTags` | Save auto-tags | Python AI service |
| POST | `/api/ML/SaveRecommendationCache` | Save recommendations | Python AI service |
| GET  | `/api/ML/GetMLScores?complaintId=N` | Read scores | Angular |
| GET  | `/api/ML/GetRecommendedComplaints?userId=N` | Get recommendations | Angular |
| GET  | `/api/ML/GetTags?complaintId=N` | Get complaint tags | Angular |
| POST | `/api/ML/CategorizeText` | Text → category suggestion | Angular form |
| POST | `/api/ML/AnalyzeImage` | Image → category + OCR | Angular form |
| POST | `/api/ML/CheckDuplicates` | Semantic duplicate check | Angular form |
| POST | `/api/ML/GetGeoClusters` | Hotspot clusters | Admin map |
| POST | `/api/ML/GetForecast` | Prophet trend forecast | Admin dashboard |
| POST | `/api/ML/Chat` | Chatbot query | Angular chatbot |
| POST | `/api/ML/TriggerRetrain` | Retrain all models | Admin panel |
| GET  | `/api/ML/CheckAIHealth` | Check AI service status | Admin panel |

---

## Architecture Decision Log

Based on the Senior AI Architect analysis (FixMyCity_AI_ML_Integration_Analysis.md):

| Decision | Reason |
|---|---|
| Python FastAPI microservice | All top ML libraries are Python-first; .NET API stays clean |
| Rule-based priority score first | No historical data on launch; formula gives immediate value |
| Cosine similarity for duplicates | SQL pre-filters candidates; AI re-ranks — no vector DB needed |
| JSON embedding storage in SQL | < 10k complaints; in-memory numpy is fast enough; avoids infra complexity |
| Fail-open toxicity | False positives worse than negatives on a civic platform |
| Fire-and-forget AI scoring | Never block the complaint submission response |
| DB retry queue | AI service failures don't cause data loss |
| Shared API key (not JWT) | Internal service-to-service; JWT is overkill |
| Ollama llama3.2:3b for chatbot | Free, local, 4GB RAM, Azure OpenAI-compatible API for production swap |
| CLIP zero-shot for images | No labelled training data exists yet; 65–75% accuracy on civic images |