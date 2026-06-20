"""
FixMyCity AI — Analytics Router
Auto-tagging with KeyBERT (Section 2.12)
Geo-clustering with DBSCAN (Section 2.7)
Trend forecasting with Prophet (Section 2.13)
"""
import json
import logging
from typing import Optional
from fastapi import APIRouter, BackgroundTasks
from pydantic import BaseModel

from services.model_manager import get_store
from services.database import fetch_df, load_snapshot_for_prophet
from services.notifier import post_tags, post_ai_decision_log

logger = logging.getLogger("analytics")
router = APIRouter(prefix="/ai", tags=["Analytics"])


# ── Schemas ───────────────────────────────────────────────────────────────────

class TagRequest(BaseModel):
    complaint_id: int
    title:        str
    description:  str
    top_n:        int = 5

class TagResponse(BaseModel):
    complaint_id: int
    tags:         list[dict]   # [{"tag": "pothole", "score": 0.87}, ...]

class GeoClusterRequest(BaseModel):
    locality_id:   Optional[int] = None
    min_samples:   int           = 3     # DBSCAN min_samples
    eps_km:        float         = 0.5   # DBSCAN radius in km

class GeoCluster(BaseModel):
    cluster_id:     int
    complaint_count: int
    centroid_lat:   float
    centroid_lng:   float
    complaint_ids:  list[int]

class GeoClusterResponse(BaseModel):
    clusters: list[GeoCluster]
    noise_count: int

class ForecastRequest(BaseModel):
    category_id: Optional[int] = None
    periods:     int           = 30      # forecast days ahead

class ForecastPoint(BaseModel):
    ds:     str
    yhat:   float
    yhat_lower: float
    yhat_upper: float

class ForecastResponse(BaseModel):
    category_id: Optional[int]
    forecast:    list[ForecastPoint]


# ── Auto-tagging ──────────────────────────────────────────────────────────────

@router.post("/tag-complaint", response_model=TagResponse)
async def tag_complaint(req: TagRequest, background_tasks: BackgroundTasks):
    """
    Extracts top-N keywords from the complaint text using KeyBERT.
    Results are saved asynchronously via POST api/ML/SaveTags.
    """
    store = get_store()
    tags  = []

    if store.keybert_model:
        try:
            text     = f"{req.title}. {req.description}"
            keywords = store.keybert_model.extract_keywords(
                text,
                keyphrase_ngram_range=(1, 2),
                stop_words="english",
                top_n=req.top_n,
                use_maxsum=True,
                nr_candidates=20,
            )
            tags = [{"tag": kw, "score": round(float(sc), 4)} for kw, sc in keywords]
        except Exception as e:
            logger.warning("KeyBERT failed: %s", e)

    if tags:
        tags_json = json.dumps(tags)
        background_tasks.add_task(post_tags, req.complaint_id, tags_json)
        background_tasks.add_task(
            post_ai_decision_log, req.complaint_id, "AutoTag",
            f"{req.title[:100]}", f"tags={[t['tag'] for t in tags]}",
            tags[0]["score"] if tags else 0.0, "keybert")

    return TagResponse(complaint_id=req.complaint_id, tags=tags)


# ── Geo clustering ────────────────────────────────────────────────────────────

@router.post("/geo-cluster", response_model=GeoClusterResponse)
async def geo_cluster(req: GeoClusterRequest):
    """
    DBSCAN spatial clustering on complaint lat/lng coordinates.
    Returns cluster centroids and complaint IDs per cluster for heatmap rendering.
    """
    import numpy as np
    from sklearn.cluster import DBSCAN

    # Phase 3 (2026-05-19): parameterised query — previously interpolated
    # `req.locality_id` directly into the SQL string. Pydantic constrains it
    # to Optional[int], so the prior code was not externally exploitable,
    # but parameter binding is the correct discipline regardless.
    if req.locality_id is not None:
        query = """
            SELECT c.ComplaintId, c.Latitude, c.Longitude
            FROM dbo.Complaints c
            WHERE c.Status NOT IN ('Resolved','Rejected','Linked')
              AND c.Latitude IS NOT NULL AND c.Longitude IS NOT NULL
              AND c.LocalityId = ?
        """
        params = [req.locality_id]
    else:
        query = """
            SELECT c.ComplaintId, c.Latitude, c.Longitude
            FROM dbo.Complaints c
            WHERE c.Status NOT IN ('Resolved','Rejected','Linked')
              AND c.Latitude IS NOT NULL AND c.Longitude IS NOT NULL
        """
        params = None

    try:
        df = fetch_df(query, params)
    except Exception as e:
        logger.error("Geo cluster DB query failed: %s", e)
        return GeoClusterResponse(clusters=[], noise_count=0)

    if df.empty or len(df) < req.min_samples:
        return GeoClusterResponse(clusters=[], noise_count=len(df))

    coords = df[["Latitude", "Longitude"]].values.astype(float)

    # DBSCAN with haversine metric; eps in radians (km / Earth radius)
    EARTH_R = 6371.0
    eps_rad = req.eps_km / EARTH_R
    db      = DBSCAN(eps=eps_rad, min_samples=req.min_samples,
                     algorithm="ball_tree", metric="haversine")
    labels  = db.fit_predict(np.radians(coords))

    df["cluster"] = labels
    noise_count   = int((labels == -1).sum())
    clusters      = []

    for cid in sorted(set(labels)):
        if cid == -1:
            continue
        mask = df["cluster"] == cid
        sub  = df[mask]
        clusters.append(GeoCluster(
            cluster_id      = int(cid),
            complaint_count = int(mask.sum()),
            centroid_lat    = round(float(sub["Latitude"].mean()), 7),
            centroid_lng    = round(float(sub["Longitude"].mean()), 7),
            complaint_ids   = sub["ComplaintId"].tolist(),
        ))

    return GeoClusterResponse(clusters=clusters, noise_count=noise_count)


# ── Trend forecasting ─────────────────────────────────────────────────────────

@router.post("/forecast", response_model=ForecastResponse)
async def forecast_complaints(req: ForecastRequest):
    """
    Prophet time-series forecast on PlatformStatsSnapshot data.
    Returns expected complaint counts for the next `periods` days.
    Requires at least 14 daily snapshots to produce a meaningful forecast.
    """
    try:
        from prophet import Prophet
        import pandas as pd

        df = load_snapshot_for_prophet(req.category_id)

        if len(df) < 14:
            return ForecastResponse(
                category_id=req.category_id,
                forecast=[],
            )

        m = Prophet(
            yearly_seasonality=True,
            weekly_seasonality=True,
            daily_seasonality=False,
            changepoint_prior_scale=0.05,
        )
        m.fit(df)

        future   = m.make_future_dataframe(periods=req.periods)
        forecast = m.predict(future)
        tail     = forecast.tail(req.periods)[["ds", "yhat", "yhat_lower", "yhat_upper"]]

        points = [
            ForecastPoint(
                ds=row["ds"].strftime("%Y-%m-%d"),
                yhat=round(max(0.0, float(row["yhat"])), 2),
                yhat_lower=round(max(0.0, float(row["yhat_lower"])), 2),
                yhat_upper=round(max(0.0, float(row["yhat_upper"])), 2),
            )
            for _, row in tail.iterrows()
        ]

        return ForecastResponse(category_id=req.category_id, forecast=points)

    except ImportError:
        logger.warning("Prophet not installed — install with: pip install prophet")
        return ForecastResponse(category_id=req.category_id, forecast=[])
    except Exception as e:
        logger.error("Forecast failed: %s", e)
        return ForecastResponse(category_id=req.category_id, forecast=[])
