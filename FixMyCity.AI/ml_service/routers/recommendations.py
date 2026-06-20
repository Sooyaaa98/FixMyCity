"""
FixMyCity AI — Recommendations Router
Section 2.8: Hybrid content-based (existing SQL) → ALS collaborative filtering upgrade.
The nightly job calls /ai/generate-recommendations for all users.
The existing GET api/ML/GetRecommendedComplaints reads from the pre-populated cache.
"""
import json
import logging
import numpy as np
from typing import Optional
from fastapi import APIRouter, BackgroundTasks
from pydantic import BaseModel

from services.model_manager import get_store
from services.notifier import post_recommendation_cache, post_ai_decision_log
from services.database import fetch_df

logger = logging.getLogger("recommendations")
router = APIRouter(prefix="/ai", tags=["Recommendations"])


# ── Schemas ───────────────────────────────────────────────────────────────────

class RecommendRequest(BaseModel):
    user_id:      int
    category_ids: list[int] = []
    locality_ids: list[int] = []
    top_n:        int       = 10

class RecommendItem(BaseModel):
    complaint_id: int
    score:        float

class RecommendResponse(BaseModel):
    user_id:      int
    items:        list[RecommendItem]
    source:       str   # "als" | "content" | "fallback"

class BulkRecommendRequest(BaseModel):
    top_n: int = 10   # run nightly for ALL active users


# ── Per-user recommendation ───────────────────────────────────────────────────

@router.post("/recommend", response_model=RecommendResponse)
async def recommend(req: RecommendRequest, background_tasks: BackgroundTasks):
    """
    Returns top-N recommended complaints for a single user.
    Uses ALS if model is trained, falls back to content-based SQL filter.
    """
    store = get_store()

    # ── Try ALS ───────────────────────────────────────────────────────────────
    if (store.als_model is not None
            and store.als_user_map is not None
            and req.user_id in store.als_user_map):
        items  = _als_recommend(req.user_id, req.top_n, store)
        source = "als"
    else:
        # Content-based fallback: SQL join on UserInterests
        items  = _content_recommend(req.user_id, req.category_ids,
                                    req.locality_ids, req.top_n)
        source = "content" if items else "fallback"

    if not items:
        items  = _popular_fallback(req.top_n)
        source = "fallback"

    # Post cache back to .NET API asynchronously
    recs_json = json.dumps([{"complaint_id": i.complaint_id, "score": i.score} for i in items])
    background_tasks.add_task(post_recommendation_cache, req.user_id, recs_json)
    background_tasks.add_task(
        post_ai_decision_log, None, "Recommendation",
        f"userId={req.user_id} source={source}",
        f"top_complaint={items[0].complaint_id if items else 'none'}",
        items[0].score if items else 0.0, source)

    return RecommendResponse(user_id=req.user_id, items=items, source=source)


# ── Nightly bulk recommendation ───────────────────────────────────────────────

@router.post("/generate-recommendations")
async def generate_all_recommendations(req: BulkRecommendRequest,
                                       background_tasks: BackgroundTasks):
    """
    Called by Azure Function Timer Trigger nightly.
    Generates recommendations for all active users and posts results to cache.
    """
    df = fetch_df("""
        SELECT DISTINCT u.UserId,
               STRING_AGG(CAST(ui.CategoryId AS VARCHAR), ',') AS CatIds,
               STRING_AGG(CAST(ui.PreferredLocalityId AS VARCHAR), ',') AS LocIds
        FROM dbo.Users u
        LEFT JOIN dbo.UserInterests ui ON ui.UserId = u.UserId
        WHERE u.IsActive = 1
        GROUP BY u.UserId
    """)

    user_ids = df["UserId"].tolist()
    logger.info("Generating recommendations for %d users...", len(user_ids))

    background_tasks.add_task(_bulk_regen, df, req.top_n)
    return {"success": True, "users_queued": len(user_ids)}


async def _bulk_regen(df, top_n: int):
    import asyncio
    store = get_store()
    for _, row in df.iterrows():
        uid    = int(row["UserId"])
        cats   = [int(x) for x in str(row.get("CatIds") or "").split(",") if x.strip()]
        locs   = [int(x) for x in str(row.get("LocIds") or "").split(",") if x.strip()]

        if store.als_model and uid in (store.als_user_map or {}):
            items = _als_recommend(uid, top_n, store)
        else:
            items = _content_recommend(uid, cats, locs, top_n)

        recs_json = json.dumps([{"complaint_id": i.complaint_id, "score": i.score} for i in items])
        await post_recommendation_cache(uid, recs_json)
        await asyncio.sleep(0.05)   # throttle to avoid overwhelming .NET API


# ── ALS recommendation ────────────────────────────────────────────────────────

def _als_recommend(user_id: int, top_n: int, store) -> list[RecommendItem]:
    """Matrix factorization recommendation using pre-trained ALS factors."""
    try:
        user_idx = store.als_user_map.get(user_id)
        if user_idx is None:
            return []
        user_vec = store.als_user_factors[user_idx]
        scores   = store.als_item_factors @ user_vec
        top_idx  = np.argsort(scores)[::-1][:top_n * 2]   # overshoot, filter below

        # Reverse-map ALS item indices to ComplaintIds
        idx_to_cid = {v: k for k, v in store.als_item_map.items()}
        items = []
        for idx in top_idx:
            cid = idx_to_cid.get(int(idx))
            if cid:
                items.append(RecommendItem(complaint_id=cid, score=round(float(scores[idx]), 6)))
            if len(items) >= top_n:
                break
        return items
    except Exception as e:
        logger.warning("ALS recommend failed: %s", e)
        return []


# ── Content-based (SQL) fallback ──────────────────────────────────────────────

def _content_recommend(user_id: int, category_ids: list,
                       locality_ids: list, top_n: int) -> list[RecommendItem]:
    if not category_ids and not locality_ids:
        return []

    cat_clause = f"c.CategoryId IN ({','.join(str(x) for x in category_ids)})" if category_ids else "1=0"
    loc_clause = f"c.LocalityId IN ({','.join(str(x) for x in locality_ids)})" if locality_ids else "1=0"

    query = f"""
        SELECT TOP ({top_n})
            c.ComplaintId,
            ISNULL(ml.PriorityScore, 0) AS Score
        FROM dbo.Complaints c
        LEFT JOIN dbo.ComplaintMLScores ml ON ml.ComplaintId = c.ComplaintId
        WHERE c.Status NOT IN ('Resolved','Rejected','Linked')
          AND ({cat_clause} OR {loc_clause})
        ORDER BY Score DESC, c.SubmittedAt DESC
    """
    try:
        df = fetch_df(query)
        return [RecommendItem(complaint_id=int(r["ComplaintId"]),
                              score=round(float(r["Score"]), 6)) for _, r in df.iterrows()]
    except Exception as e:
        logger.warning("Content-based fallback failed: %s", e)
        return []


def _popular_fallback(top_n: int) -> list[RecommendItem]:
    """Most recent open complaints when no interest data exists."""
    query = f"""
        SELECT TOP ({top_n}) c.ComplaintId, 0.5 AS Score
        FROM dbo.Complaints c
        WHERE c.Status NOT IN ('Resolved','Rejected','Linked')
        ORDER BY c.SubmittedAt DESC
    """
    try:
        df = fetch_df(query)
        return [RecommendItem(complaint_id=int(r["ComplaintId"]),
                              score=0.5) for _, r in df.iterrows()]
    except Exception:
        return []
