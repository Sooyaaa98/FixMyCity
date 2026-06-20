"""
FixMyCity AI — Scoring Router
Handles: Priority Score (rule-based Phase 1 + LightGBM Phase 2),
         Resolution Probability and Predicted Date (LightGBM classifier/regressor).
Implements Section 2.4 and 2.9 of the integration analysis.
"""
import json
import logging
from datetime import datetime, timedelta
from typing import Optional

import numpy as np
import pandas as pd
from fastapi import APIRouter, BackgroundTasks, HTTPException
from pydantic import BaseModel

from config import MODEL_VERSION_RULES, MODEL_VERSION_LGBM
from services.model_manager import get_store
from services.notifier import post_ml_scores, post_ai_decision_log

logger = logging.getLogger("scoring")
router = APIRouter(prefix="/ai", tags=["Scoring"])

# ── Criticality weights ───────────────────────────────────────────────────────
CRIT_WEIGHT = {"Low": 1, "Medium": 2, "High": 3, "Critical": 4}
# ── Expected resolution days by criticality (rule-based SLA) ─────────────────
SLA_DAYS    = {"Low": 21, "Medium": 14, "High": 7, "Critical": 3}


# ── Schemas ───────────────────────────────────────────────────────────────────

class ScoreRequest(BaseModel):
    complaint_id:    int
    category_id:     int
    criticality:     str
    locality_id:     int
    dept_id:         Optional[int]  = None
    days_open:       int            = 0
    has_pwg:         bool           = False
    funding_amount:  float          = 0.0
    was_escalated:   bool           = False
    description_len: int            = 0

class ScoreResponse(BaseModel):
    complaint_id:              int
    priority_score:            float
    resolution_probability:    float
    predicted_resolution_date: Optional[str]
    model_version:             str


# ── Main endpoint ─────────────────────────────────────────────────────────────

@router.post("/score-complaint", response_model=ScoreResponse)
async def score_complaint(req: ScoreRequest, background_tasks: BackgroundTasks):
    """
    Scores a single complaint.
    Phase 1: rule-based weighted formula (immediate, no training data needed).
    Phase 2: LightGBM models if trained (switched automatically when available).
    Results are posted back to the .NET API asynchronously.
    """
    store = get_store()
    use_ml = (store.resolution_classifier is not None
              and store.resolution_regressor  is not None
              and store.trained_count >= 100)

    if use_ml:
        result = _score_lgbm(req, store)
    else:
        result = _score_rules(req)

    # Fire-and-forget: POST back to .NET API
    background_tasks.add_task(
        post_ml_scores,
        req.complaint_id,
        result.priority_score,
        result.resolution_probability,
        result.predicted_resolution_date,
        result.model_version,
    )
    background_tasks.add_task(
        post_ai_decision_log,
        req.complaint_id,
        "PriorityScore",
        f"cat={req.category_id} crit={req.criticality} days={req.days_open}",
        f"priority={result.priority_score:.1f} prob={result.resolution_probability:.2f}",
        result.resolution_probability,
        result.model_version,
    )

    return result


# ── Rule-based scoring (Phase 1) ──────────────────────────────────────────────

def _score_rules(req: ScoreRequest) -> ScoreResponse:
    """
    Weighted formula: Score = (Criticality × 20) + (Overdue × 2) + (Funding × 0.01) + (Escalation × 15)
    Range: 0–100. Safe immediately with zero training data.
    """
    crit_num     = CRIT_WEIGHT.get(req.criticality, 2)
    sla          = SLA_DAYS.get(req.criticality, 14)
    days_overdue = max(0, req.days_open - sla)

    priority = (
        crit_num          * 20 +
        days_overdue      * 2  +
        min(req.funding_amount * 0.01, 10) +
        (15 if req.was_escalated else 0) +
        (5  if req.has_pwg       else 0)
    )
    priority = round(min(priority, 100.0), 2)

    # Resolution probability: base rate by criticality, penalised by age
    base_prob = {1: 0.85, 2: 0.75, 3: 0.65, 4: 0.55}.get(crit_num, 0.70)
    penalty   = min(0.30, req.days_open * 0.005)
    res_prob  = round(max(0.10, base_prob - penalty), 4)

    # Predicted date
    remaining_days = max(1, sla - req.days_open)
    pred_date      = (datetime.now() + timedelta(days=remaining_days)).strftime("%Y-%m-%d")

    return ScoreResponse(
        complaint_id              = req.complaint_id,
        priority_score            = priority,
        resolution_probability    = res_prob,
        predicted_resolution_date = pred_date,
        model_version             = MODEL_VERSION_RULES,
    )


# ── LightGBM scoring (Phase 2) ────────────────────────────────────────────────

def _score_lgbm(req: ScoreRequest, store) -> ScoreResponse:
    """Uses trained LightGBM models. Falls back to rules on any error."""
    try:
        enc = store.label_encoders
        def safe_encode(encoder_name, val):
            le = enc.get(encoder_name)
            if le is None: return 0
            classes = list(le.classes_)
            return classes.index(str(val)) if str(val) in classes else 0

        X = np.array([[
            safe_encode("category",   req.category_id),
            safe_encode("criticality", req.criticality),
            req.locality_id,
            req.days_open,
            int(req.has_pwg),
            min(req.funding_amount, 10000),
            int(req.was_escalated),
            req.description_len,
        ]])

        # Resolution probability
        res_prob   = float(store.resolution_classifier.predict_proba(X)[0][1])
        # Expected days to resolve
        exp_days   = float(store.resolution_regressor.predict(X)[0])
        remaining  = max(1, int(exp_days - req.days_open))
        pred_date  = (datetime.now() + timedelta(days=remaining)).strftime("%Y-%m-%d")

        # Priority: higher urgency = higher score
        crit_num     = CRIT_WEIGHT.get(req.criticality, 2)
        sla          = SLA_DAYS.get(req.criticality, 14)
        days_overdue = max(0, req.days_open - sla)
        priority     = round(min(
            (crit_num * 20) + (days_overdue * 2) + (1 - res_prob) * 30 +
            (5 if req.has_pwg else 0) + (15 if req.was_escalated else 0),
            100.0
        ), 2)

        return ScoreResponse(
            complaint_id              = req.complaint_id,
            priority_score            = priority,
            resolution_probability    = round(res_prob, 4),
            predicted_resolution_date = pred_date,
            model_version             = MODEL_VERSION_LGBM,
        )
    except Exception as e:
        logger.warning("LightGBM scoring failed (%s) — falling back to rules.", e)
        return _score_rules(req)
