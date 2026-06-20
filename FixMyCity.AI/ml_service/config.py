"""
FixMyCity AI Service — Configuration
All values read from environment variables with safe defaults.
"""
import os
from pathlib import Path

# ── Database ──────────────────────────────────────────────────────────────────
DB_CONN_STR: str = os.getenv(
    "DB_CONN_STR",
    "DRIVER={ODBC Driver 17 for SQL Server};"
    "SERVER=.;DATABASE=FixMyCityDB;Trusted_Connection=yes;"
)

# ── .NET API callback ─────────────────────────────────────────────────────────
DOTNET_API_BASE: str   = os.getenv("DOTNET_API_BASE", "http://localhost:5065")   # ISSUE 8 FIX: was 5183
AI_SERVICE_KEY: str    = os.getenv("AI_SERVICE_KEY", "fixmycity-ai-internal-key-change-me")
DOTNET_API_KEY: str    = os.getenv("DOTNET_API_KEY", AI_SERVICE_KEY)   # same key both sides

# ── Embedding model ───────────────────────────────────────────────────────────
EMBEDDING_MODEL: str   = os.getenv("EMBEDDING_MODEL", "all-MiniLM-L6-v2")
EMBEDDING_DIM: int     = 384

# ── Duplicate detection threshold ────────────────────────────────────────────
DUPLICATE_THRESHOLD: float = float(os.getenv("DUPLICATE_THRESHOLD", "0.82"))

# ── Model versioning ─────────────────────────────────────────────────────────
MODEL_VERSION_RULES: str  = "v1.0.0-rules"
MODEL_VERSION_KNN: str    = "v1.1.0-knn"
MODEL_VERSION_LGBM: str   = "v2.0.0-lgbm"
MODEL_DIR: str            = os.getenv("MODEL_DIR", "models")

# ── Image / CLIP ─────────────────────────────────────────────────────────────
# Phase 3 (2026-05-19): IMAGE_BASE_PATH default now resolves to the shared
# FixMyCityUploads directory two levels above ml_service/, matching the .NET
# API's default `Uploads:BasePath` (../FixMyCityUploads from FixMyCity.API/).
# This works on Windows, macOS, and Linux out of the box. The previous default
# was a hard-coded `C:/FixMyCityUploads` which broke any non-Windows host.
# Override via the IMAGE_BASE_PATH env var if your deploy mounts files
# somewhere else (e.g. docker-compose maps it to /uploads).
CLIP_MODEL: str           = os.getenv("CLIP_MODEL", "openai/clip-vit-base-patch32")
_default_uploads = Path(__file__).resolve().parent.parent.parent / "FixMyCityUploads"
IMAGE_BASE_PATH: str      = os.getenv("IMAGE_BASE_PATH", str(_default_uploads))

# ── Chatbot / Ollama ──────────────────────────────────────────────────────────
OLLAMA_HOST: str          = os.getenv("OLLAMA_HOST", "http://localhost:11434")
OLLAMA_MODEL: str         = os.getenv("OLLAMA_MODEL", "llama3.2:3b")

# ── Toxicity ──────────────────────────────────────────────────────────────────
USE_ML_TOXICITY: bool     = os.getenv("USE_ML_TOXICITY", "false").lower() == "true"
TOXICITY_THRESHOLD: float = float(os.getenv("TOXICITY_THRESHOLD", "0.85"))

# ── Async / retry ─────────────────────────────────────────────────────────────
MAX_SCORE_RETRIES: int    = int(os.getenv("MAX_SCORE_RETRIES", "3"))
SCORE_RETRY_DELAY: float  = float(os.getenv("SCORE_RETRY_DELAY", "0.5"))
