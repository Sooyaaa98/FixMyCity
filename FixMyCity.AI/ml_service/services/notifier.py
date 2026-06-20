"""
FixMyCity AI Service — Notifier
Posts AI results back to the .NET API via async HTTP.
The .NET API then calls the appropriate DAL method (e.g. SaveMLScores).
"""
import logging
import httpx
from config import DOTNET_API_BASE, DOTNET_API_KEY

logger = logging.getLogger("notifier")

HEADERS = {
    "Content-Type":    "application/json",
    "X-AI-Service-Key": DOTNET_API_KEY,
}


async def post_ml_scores(
    complaint_id: int,
    priority_score: float,
    resolution_probability: float,
    predicted_date: str,
    model_version: str,
):
    """POSTs scoring results to POST api/ML/SaveMLScores."""
    url     = f"{DOTNET_API_BASE}/api/ML/SaveMLScores"
    payload = {
        "complaintId":            complaint_id,
        "priorityScore":          priority_score,
        "resolutionProbability":  resolution_probability,
        "predictedResolutionDate": predicted_date,
        "modelVersion":           model_version,
    }
    await _post(url, payload)


async def post_ai_decision_log(
    complaint_id: int,
    decision_type: str,
    input_summary: str,
    output_summary: str,
    confidence: float,
    model_version: str,
):
    """Logs an AI decision to POST api/ML/LogAIDecision."""
    url     = f"{DOTNET_API_BASE}/api/ML/LogAIDecision"
    payload = {
        "complaintId":  complaint_id,
        "decisionType": decision_type,
        "inputSummary": input_summary,
        "outputSummary": output_summary,
        "confidence":   confidence,
        "modelVersion": model_version,
    }
    await _post(url, payload)


async def post_embedding(complaint_id: int, embedding_json: str, model_version: str):
    """Saves an embedding via POST api/ML/SaveEmbedding."""
    url     = f"{DOTNET_API_BASE}/api/ML/SaveEmbedding"
    payload = {
        "complaintId":   complaint_id,
        "embeddingJson": embedding_json,
        "modelVersion":  model_version,
    }
    await _post(url, payload)


async def post_tags(complaint_id: int, tags_json: str):
    """Saves AI tags via POST api/ML/SaveTags."""
    url     = f"{DOTNET_API_BASE}/api/ML/SaveTags"
    payload = {
        "complaintId": complaint_id,
        "tagsJson":    tags_json,
    }
    await _post(url, payload)


async def post_recommendation_cache(user_id: int, recs_json: str):
    """Saves nightly recommendation cache via POST api/ML/SaveRecommendationCache."""
    url     = f"{DOTNET_API_BASE}/api/ML/SaveRecommendationCache"
    payload = {"userId": user_id, "recsJson": recs_json}
    await _post(url, payload)


async def _post(url: str, payload: dict):
    """Shared async HTTP POST with timeout and graceful failure."""
    try:
        async with httpx.AsyncClient(timeout=10.0) as client:
            r = await client.post(url, json=payload, headers=HEADERS)
            if r.status_code not in (200, 201):
                logger.warning("Callback %s returned HTTP %d: %s", url, r.status_code, r.text[:200])
    except Exception as e:
        logger.error("Callback to %s failed: %s", url, e)
