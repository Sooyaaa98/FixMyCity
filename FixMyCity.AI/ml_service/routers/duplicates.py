"""
FixMyCity AI — Duplicate Detection Router
Implements Section 2.2: sentence-transformer embeddings + cosine similarity.
SQL narrows candidates (same locality + category); AI re-ranks by semantic similarity.
The embedding for each new complaint is stored for future comparisons.
"""
import json
import logging
import numpy as np
from typing import Optional
from fastapi import APIRouter, BackgroundTasks
from pydantic import BaseModel

from config import DUPLICATE_THRESHOLD, EMBEDDING_MODEL
from services.database import fetch_all_embeddings
from services.model_manager import get_store
from services.notifier import post_embedding, post_ai_decision_log

logger = logging.getLogger("duplicates")
router = APIRouter(prefix="/ai", tags=["Duplicate Detection"])


# ── Schemas ───────────────────────────────────────────────────────────────────

class DuplicateCheckRequest(BaseModel):
    complaint_id:  int
    title:         str
    description:   str
    category_id:   int
    locality_id:   int
    exclude_id:    int = 0

class DuplicateCandidate(BaseModel):
    complaint_id: int
    similarity:   float
    is_duplicate: bool    # True if similarity >= DUPLICATE_THRESHOLD

class DuplicateCheckResponse(BaseModel):
    complaint_id:     int
    candidates:       list[DuplicateCandidate]
    embedding_stored: bool


# ── Endpoint ──────────────────────────────────────────────────────────────────

@router.post("/duplicate-check", response_model=DuplicateCheckResponse)
async def check_duplicates(req: DuplicateCheckRequest, background_tasks: BackgroundTasks):
    """
    1. Generates an embedding for the new complaint's text.
    2. Loads existing embeddings from the same locality + category (SQL-filtered).
    3. Computes cosine similarity against each candidate.
    4. Returns ranked candidates above threshold.
    5. Stores the new embedding for future comparisons.
    """
    store = get_store()
    if store.sentence_model is None:
        return DuplicateCheckResponse(
            complaint_id=req.complaint_id, candidates=[], embedding_stored=False)

    # Combine title + description for richer embedding
    text    = f"{req.title}. {req.description}"
    new_vec = _embed(store, text)

    # Load existing embeddings (SQL-narrowed to locality + category)
    existing = fetch_all_embeddings(
        locality_id=req.locality_id, category_id=req.category_id)

    candidates = []
    for _, row in existing.iterrows():
        if row["ComplaintId"] == req.exclude_id:
            continue
        try:
            existing_vec = np.array(json.loads(row["EmbeddingJson"]), dtype=np.float32)
            sim          = float(_cosine_similarity(new_vec, existing_vec))
            candidates.append(DuplicateCandidate(
                complaint_id=int(row["ComplaintId"]),
                similarity=round(sim, 4),
                is_duplicate=sim >= DUPLICATE_THRESHOLD,
            ))
        except Exception:
            continue

    # Sort by similarity descending, return top 5
    candidates.sort(key=lambda c: c.similarity, reverse=True)
    candidates = candidates[:5]

    # Store embedding + log decision asynchronously
    emb_json    = json.dumps(new_vec.tolist())
    top_sim     = candidates[0].similarity if candidates else 0.0
    is_dup_flag = any(c.is_duplicate for c in candidates)

    background_tasks.add_task(
        post_embedding, req.complaint_id, emb_json, EMBEDDING_MODEL)
    background_tasks.add_task(
        post_ai_decision_log,
        req.complaint_id, "DuplicateFlag",
        text[:200],
        f"top_sim={top_sim:.3f} is_dup={is_dup_flag}",
        top_sim, EMBEDDING_MODEL,
    )

    return DuplicateCheckResponse(
        complaint_id=req.complaint_id,
        candidates=candidates,
        embedding_stored=True,
    )


# ── Helpers ───────────────────────────────────────────────────────────────────

def _embed(store, text: str) -> np.ndarray:
    vec = store.sentence_model.encode([text], normalize_embeddings=True)
    return vec[0].astype(np.float32)


def _cosine_similarity(a: np.ndarray, b: np.ndarray) -> float:
    """Cosine similarity between two L2-normalized vectors."""
    dot = float(np.dot(a, b))
    return max(-1.0, min(1.0, dot))   # clamp for floating-point safety
