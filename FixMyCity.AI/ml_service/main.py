"""
FixMyCity AI Microservice — Main Entry Point
FastAPI application exposing all AI endpoints.

Start: uvicorn main:app --host 0.0.0.0 --port 8001 --reload
Docs:  http://localhost:8001/docs

Architecture: called by .NET API (fire-and-forget after complaint submit).
Results are posted back to .NET API via HTTP callbacks.
All models loaded at startup; CLIP loaded lazily on first image request.
"""
import logging
import os
from contextlib import asynccontextmanager

# CRITICAL: load ml_service/.env BEFORE any module-level `os.getenv` call.
# services/hf_inference.py reads HF_API_TOKEN at import time; if dotenv runs
# afterwards the token is silently empty and every HF model load fails with
# "HF_API_TOKEN is not set", which is what causes the
# "sentence_model=False, clip=False, keybert=False" startup banner.
try:
    from dotenv import load_dotenv
    # Look for .env in the same directory as main.py
    _env_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), ".env")
    if os.path.isfile(_env_path):
        load_dotenv(_env_path, override=False)
except ImportError:
    # python-dotenv missing — fall back to whatever the shell env provides.
    pass

from fastapi import FastAPI, Request, HTTPException, Depends
from fastapi.middleware.cors import CORSMiddleware

from config import AI_SERVICE_KEY, MODEL_DIR, EMBEDDING_MODEL, USE_ML_TOXICITY
from services import model_manager as mm
from routers import scoring, duplicates, categorization, recommendations, analytics, chatbot, training

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s %(levelname)s [%(name)s] %(message)s",
)
logger = logging.getLogger("main")


# ── Startup / Shutdown ────────────────────────────────────────────────────────

@asynccontextmanager
async def lifespan(app: FastAPI):
    """Load models that are always needed at startup. CLIP loaded lazily."""
    logger.info("FixMyCity AI Service starting up...")

    # Always load sentence model (used by duplicates + categorization + KeyBERT)
    mm.load_sentence_model(EMBEDDING_MODEL)

    # Load KeyBERT after sentence model
    mm.load_keybert_model()

    # Load toxicity model only if enabled (adds ~500MB RAM)
    if USE_ML_TOXICITY:
        mm.load_toxicity_model()

    # Restore previously trained ML models from disk
    mm.load_persisted_models(MODEL_DIR)

    logger.info("Startup complete. Available: sentence_model=%s, clip=%s, keybert=%s",
                mm.get_store().sentence_model is not None,
                mm.get_store().clip_model is not None,
                mm.get_store().keybert_model is not None)
    yield
    logger.info("FixMyCity AI Service shutting down.")


# ── App ───────────────────────────────────────────────────────────────────────

app = FastAPI(
    title       = "FixMyCity AI Microservice",
    description = "AI features: scoring, duplicate detection, categorization, "
                  "recommendations, trend forecasting, geo-clustering, chatbot.",
    version     = "1.0.0",
    lifespan    = lifespan,
)

# ISSUE 20 FIX: Origins driven by ALLOWED_ORIGINS env var — was hardcoded to dev ports
_raw_origins    = os.getenv("ALLOWED_ORIGINS", "http://localhost:5065,http://localhost:4200")
_ALLOWED_ORIGINS = [o.strip() for o in _raw_origins.split(",") if o.strip()]

app.add_middleware(
    CORSMiddleware,
    allow_origins=_ALLOWED_ORIGINS,   # set ALLOWED_ORIGINS env var for production
    allow_methods=["*"],
    allow_headers=["*"],
)


# ── Security — API key check ──────────────────────────────────────────────────

async def verify_api_key(request: Request):
    """
    Every endpoint requires X-AI-Service-Key header.
    Same shared secret configured in both .NET API (appsettings.json)
    and Python service (AI_SERVICE_KEY env var).
    """
    key = request.headers.get("X-AI-Service-Key")
    if not key or key != AI_SERVICE_KEY:
        raise HTTPException(status_code=401, detail="Invalid or missing X-AI-Service-Key header.")


# ── Routers ───────────────────────────────────────────────────────────────────

COMMON = {"dependencies": [Depends(verify_api_key)]}

app.include_router(scoring.router,          **COMMON)
app.include_router(duplicates.router,       **COMMON)
app.include_router(categorization.router,   **COMMON)
app.include_router(recommendations.router,  **COMMON)
app.include_router(analytics.router,        **COMMON)
app.include_router(chatbot.router,          **COMMON)
app.include_router(training.router,         **COMMON)


# ── Health check (no auth required) ──────────────────────────────────────────

@app.get("/health")
async def health():
    store = mm.get_store()
    return {
        "status":          "ok",
        "sentence_model":  store.sentence_model  is not None,
        "clip_model":      store.clip_model       is not None,
        "keybert":         store.keybert_model    is not None,
        "lgbm_classifier": store.resolution_classifier is not None,
        "lgbm_regressor":  store.resolution_regressor  is not None,
        "als_model":       store.als_model        is not None,
        "trained_samples": store.trained_count,
    }


# ── Lazy CLIP loader endpoint ─────────────────────────────────────────────────

@app.post("/ai/load-clip", dependencies=[Depends(verify_api_key)])
async def load_clip():
    """
    CLIP is not loaded at startup (it is ~350 MB).
    Call this endpoint once before using /ai/analyze-image.
    """
    from config import CLIP_MODEL
    mm.load_clip_model(CLIP_MODEL)
    return {"success": mm.get_store().clip_model is not None}


if __name__ == "__main__":
    import uvicorn
    uvicorn.run("main:app", host="0.0.0.0", port=8001, reload=True)
