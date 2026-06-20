"""
FixMyCity AI — Categorization Router (fixed)
BUG FIX: CategorySuggestion now includes category_id.
Previously the field was missing, causing Angular's 'Accept AI suggestion'
button to pass undefined to the form — the category was never set.

The KNN classifier predicts a category name; we map it back to an ID
using the label encoder stored alongside the model.
"""
import io
import os
import logging
import base64
from typing import Optional

import numpy as np
from fastapi import APIRouter, BackgroundTasks, HTTPException
from pydantic import BaseModel
from better_profanity import profanity

from config import IMAGE_BASE_PATH, USE_ML_TOXICITY, TOXICITY_THRESHOLD, MODEL_VERSION_KNN
from services.model_manager import get_store
from services.notifier import post_ai_decision_log

logger = logging.getLogger("categorization")
router = APIRouter(prefix="/ai", tags=["Categorization"])

profanity.load_censor_words()


# ── Schemas ────────────────────────────────────────────────────────────────────

class TextCategorizeRequest(BaseModel):
    complaint_id: int
    title:        str
    description:  str

class ImageAnalyzeRequest(BaseModel):
    complaint_id:    int
    file_path:       Optional[str] = None
    image_base64:    Optional[str] = None

class CategorySuggestion(BaseModel):
    category_id:   int          # FIX: was missing — Angular needs this to patch the form
    category_name: str
    confidence:    float

class TextCategorizeResponse(BaseModel):
    complaint_id:          int
    suggestions:           list[CategorySuggestion]
    is_toxic:              bool
    toxic_reason:          Optional[str]
    suggested_description: Optional[str] = None

class ImageAnalyzeResponse(BaseModel):
    complaint_id:          int
    suggestions:           list[CategorySuggestion]
    ocr_text:              Optional[str]
    gps_lat:               Optional[float] = None    # extracted from EXIF if present
    gps_lon:               Optional[float] = None
    suggested_description: Optional[str]  = None     # rule-based description draft

class ToxicityCheckRequest(BaseModel):
    complaint_id: int
    text:         str

class ToxicityCheckResponse(BaseModel):
    complaint_id: int
    is_toxic:     bool
    reason:       Optional[str]
    confidence:   float


# ── Helper: resolve category_id from name ──────────────────────────────────────

def _resolve_category_id(store, category_name: str) -> int:
    """
    Map a predicted category name back to its DB CategoryId.

    Phase 3 (2026-05-19): the previous implementation used the LabelEncoder's
    alphabetical class index as the DB CategoryId, which is wrong — the
    encoder is sorted lexicographically, not by DB ID. It also referenced
    `store.category_label_encoder` even when callers passed `store=None` from
    the keyword-fallback path.

    New behaviour:
      1. Use `store.category_name_to_id` (populated by training.py from
         dbo.IssueCategories) if present.
      2. Fall back to a one-shot DB lookup (memoised onto the store) so even
         a fresh service that hasn't been trained yet still returns a real
         CategoryId.
      3. Final fallback: a stable hash that's at least non-zero so the
         Angular form distinguishes "no suggestion" from "id 0".
    """
    if store is not None:
        mapping = getattr(store, "category_name_to_id", None) or {}
        if category_name in mapping:
            return int(mapping[category_name])

        # Lazy DB-backed warm-up of the cache (covers fresh / pre-training calls).
        try:
            from services.database import fetch_df
            id_df = fetch_df(
                "SELECT CategoryId, CategoryName FROM dbo.IssueCategories")
            mapping = {
                str(row["CategoryName"]): int(row["CategoryId"])
                for _, row in id_df.iterrows()
            }
            store.category_name_to_id = mapping
            if category_name in mapping:
                return int(mapping[category_name])
        except Exception:
            pass

    return (abs(hash(category_name)) % 10000) + 1


# ── Text categorization ────────────────────────────────────────────────────────

@router.post("/categorize-text", response_model=TextCategorizeResponse)
async def categorize_text(req: TextCategorizeRequest, background_tasks: BackgroundTasks):
    store = get_store()
    text  = f"{req.title}. {req.description}"

    is_toxic, toxic_reason, toxic_conf = _check_toxicity(text, store)

    suggestions = []
    # Phase 3 (2026-05-19): the previous block read `idx` from
    # `knn.kneighbors()` (a *row* index into the training set) and used it
    # to look up `category_label_encoder.classes_` (a *class* list). That is
    # an off-by-everything bug. The KNN classifier already exposes class-level
    # probabilities via `predict_proba()`; use that instead and rank top 3
    # by probability.
    if store.sentence_model is not None and store.category_knn is not None:
        try:
            vec    = store.sentence_model.encode([text], normalize_embeddings=True)
            probs  = store.category_knn.predict_proba(vec)[0]
            classes = list(store.category_knn.classes_)

            ranked = sorted(
                zip(classes, probs.tolist()),
                key=lambda x: x[1],
                reverse=True,
            )
            for name, conf in ranked[:3]:
                if conf <= 0:
                    continue
                cat_id = _resolve_category_id(store, name)
                suggestions.append(CategorySuggestion(
                    category_id=cat_id,
                    category_name=name,
                    confidence=round(max(0.0, min(1.0, float(conf))), 4),
                ))
        except Exception as e:
            logger.warning("KNN categorization failed: %s", e)

    # Rule-based fallback — return top keyword-matched category
    if not suggestions:
        suggestions = _keyword_fallback(text)

    background_tasks.add_task(
        post_ai_decision_log,
        req.complaint_id, "Categorization",
        text[:200],
        ", ".join(f"{s.category_name}({s.confidence:.2f})" for s in suggestions[:2]),
        suggestions[0].confidence if suggestions else 0.0,
        MODEL_VERSION_KNN,
    )

    top_cat = suggestions[0].category_name if suggestions else None
    suggested_description = _suggest_description(top_cat, req.description, None)

    return TextCategorizeResponse(
        complaint_id=req.complaint_id,
        suggestions=suggestions,
        is_toxic=is_toxic,
        toxic_reason=toxic_reason,
        suggested_description=suggested_description,
    )


# ── Image analysis ─────────────────────────────────────────────────────────────

@router.post("/analyze-image", response_model=ImageAnalyzeResponse)
async def analyze_image(req: ImageAnalyzeRequest, background_tasks: BackgroundTasks):
    store = get_store()

    # Load image bytes
    img_bytes = None
    if req.image_base64:
        try:
            img_bytes = base64.b64decode(req.image_base64)
        except Exception:
            raise HTTPException(400, "Invalid base64 image data.")
    elif req.file_path:
        full_path = os.path.join(IMAGE_BASE_PATH, os.path.basename(req.file_path))
        if not os.path.isfile(full_path):
            raise HTTPException(404, f"Image not found: {full_path}")
        with open(full_path, "rb") as f:
            img_bytes = f.read()

    suggestions: list[CategorySuggestion] = []
    ocr_text: Optional[str] = None

    if img_bytes:
        # OCR
        try:
            import pytesseract
            from PIL import Image as PILImage
            pil_img  = PILImage.open(io.BytesIO(img_bytes)).convert("RGB")
            ocr_text = pytesseract.image_to_string(pil_img).strip() or None
        except Exception as e:
            logger.warning("OCR failed: %s", e)

        # CLIP / HF zero-shot classification
        labels = _get_category_labels(store)
        if labels:
            try:
                from services.hf_inference import hf_zero_shot_image_classify
                results = hf_zero_shot_image_classify(img_bytes, candidate_labels=labels)
                for r in results[:3]:
                    cat_id = _resolve_category_id(store, r["label"])
                    suggestions.append(CategorySuggestion(
                        category_id=cat_id,
                        category_name=r["label"],
                        confidence=round(float(r["score"]), 4),
                    ))
            except Exception as e:
                logger.warning("HF image classification failed: %s — using OCR fallback", e)
                if ocr_text:
                    suggestions = _keyword_fallback(ocr_text)

    gps_lat, gps_lon = _extract_gps(img_bytes) if img_bytes else (None, None)
    top_cat = suggestions[0].category_name if suggestions else None
    suggested_description = _suggest_description(top_cat, None, ocr_text)

    return ImageAnalyzeResponse(
        complaint_id=req.complaint_id,
        suggestions=suggestions,
        ocr_text=ocr_text,
        gps_lat=gps_lat,
        gps_lon=gps_lon,
        suggested_description=suggested_description,
    )


# ── Toxicity check ─────────────────────────────────────────────────────────────

@router.post("/check-toxicity", response_model=ToxicityCheckResponse)
async def check_toxicity_endpoint(req: ToxicityCheckRequest):
    store = get_store()
    is_toxic, reason, confidence = _check_toxicity(req.text, store)
    return ToxicityCheckResponse(
        complaint_id=req.complaint_id,
        is_toxic=is_toxic,
        reason=reason,
        confidence=confidence,
    )


# ── Internal toxicity helper ───────────────────────────────────────────────────

def _check_toxicity(text: str, store) -> tuple[bool, Optional[str], float]:
    """
    Returns (is_toxic, reason, confidence).
    Layer 1: profanity filter (always active, fast).
    Layer 2: ML model (only if USE_ML_TOXICITY=true and model loaded).
    Fail-open: if both layers pass, the complaint is allowed through.
    """
    # Layer 1: rule-based profanity
    if profanity.contains_profanity(text):
        return True, "Contains profanity or inappropriate language.", 1.0

    # Layer 2: ML toxicity model
    if USE_ML_TOXICITY and store.toxicity_pipeline is not None:
        try:
            result = store.toxicity_pipeline(text[:512])[0]
            if result["label"].upper() == "TOXIC" and result["score"] >= TOXICITY_THRESHOLD:
                return True, "Detected as toxic by AI classifier.", float(result["score"])
        except Exception as e:
            logger.warning("ML toxicity check failed (fail-open): %s", e)

    # Layer 3: HF API fallback
    # Phase 3 (2026-05-19): hf_toxicity_check returns a list of
    # `{"label": str, "score": float}` straight from the HF text-classification
    # endpoint — NOT a dict with `is_toxic` / `confidence`. The previous code
    # called `.get()` on a list which raises AttributeError and got swallowed
    # below — every HF toxicity check was a silent no-op.
    if USE_ML_TOXICITY:
        try:
            from services.hf_inference import hf_toxicity_check
            raw = hf_toxicity_check(text) or []
            # toxic-bert returns a single top-label dict per call in most cases;
            # be permissive and accept either a list of dicts or a single dict.
            if isinstance(raw, dict):
                raw = [raw]
            for entry in raw:
                label = str(entry.get("label", "")).upper()
                score = float(entry.get("score", 0.0))
                if label == "TOXIC" and score >= TOXICITY_THRESHOLD:
                    return True, "Detected as toxic by HF classifier.", score
        except Exception as e:
            logger.warning("HF toxicity check failed (fail-open): %s", e)

    return False, None, 0.0


# ── Keyword fallback categorization ───────────────────────────────────────────

_KEYWORD_RULES: list[tuple[str, list[str]]] = [
    ("Road & Infrastructure", ["pothole", "road", "footpath", "bridge", "pavement", "highway"]),
    ("Water Supply",          ["water", "pipe", "leak", "supply", "drainage", "sewage", "flood"]),
    ("Electricity",           ["electricity", "power", "light", "streetlight", "outage", "wire"]),
    ("Garbage & Sanitation",  ["garbage", "waste", "trash", "dump", "litter", "sanitation"]),
    ("Public Safety",         ["safety", "crime", "accident", "theft", "danger", "violence"]),
    ("Parks & Trees",         ["park", "tree", "garden", "playground", "grass", "bench"]),
    ("Noise Pollution",       ["noise", "loud", "sound", "disturbance", "music"]),
    ("Other",                 []),
]

def _keyword_fallback(text: str) -> list[CategorySuggestion]:
    text_lower = text.lower()
    results    = []
    for cat_name, keywords in _KEYWORD_RULES:
        if not keywords:
            continue
        hits = sum(1 for kw in keywords if kw in text_lower)
        if hits:
            conf   = min(0.95, hits * 0.25)
            cat_id = _resolve_category_id(None, cat_name)
            results.append(CategorySuggestion(
                category_id=cat_id,
                category_name=cat_name,
                confidence=round(conf, 4),
            ))

    results.sort(key=lambda s: s.confidence, reverse=True)
    if not results:
        results.append(CategorySuggestion(category_id=8, category_name="Other", confidence=0.5))
    return results[:3]


# ── Helper: get category label list for CLIP ──────────────────────────────────

def _get_category_labels(store) -> list[str]:
    """
    Returns the candidate label list for CLIP zero-shot classification.

    Phase 3 (2026-05-19): preferred source is now `store.category_labels`,
    which training.py sets to the unique trained category names. The previous
    code read `store.category_label_encoder.classes_` which was never set;
    on every cold call this fell through to the rule-based list silently.
    """
    if store.category_labels:
        return list(store.category_labels)
    if store.category_label_encoder is not None:
        try:
            return list(store.category_label_encoder.classes_)
        except Exception:
            pass
    return [name for name, _ in _KEYWORD_RULES if name != "Other"]


# ── EXIF GPS extraction ───────────────────────────────────────────────────────

def _extract_gps(img_bytes: bytes) -> tuple[Optional[float], Optional[float]]:
    """
    Returns (lat, lon) decoded from the JPEG's EXIF GPS IFD, or (None, None) if
    the image has no EXIF, no GPS block, or any tag fails to parse. Never
    raises — the submit form falls back to manual address entry.
    """
    try:
        from PIL import Image as PILImage
        from PIL.ExifTags import GPSTAGS, TAGS

        pil = PILImage.open(io.BytesIO(img_bytes))
        exif = pil.getexif()
        if not exif:
            return None, None

        gps_info_raw = None
        for tag_id, value in exif.items():
            if TAGS.get(tag_id) == "GPSInfo":
                gps_info_raw = value
                break
        if not gps_info_raw:
            # Some EXIF dialects nest GPS data behind an IFD pointer
            try:
                gps_info_raw = exif.get_ifd(0x8825)  # ExifTags.IFD.GPSInfo
            except Exception:
                gps_info_raw = None

        if not gps_info_raw:
            return None, None

        gps = {GPSTAGS.get(k, k): v for k, v in gps_info_raw.items()}
        lat_dms = gps.get("GPSLatitude")
        lon_dms = gps.get("GPSLongitude")
        if not lat_dms or not lon_dms:
            return None, None

        lat = _dms_to_deg(lat_dms)
        lon = _dms_to_deg(lon_dms)
        if gps.get("GPSLatitudeRef")  in ("S", b"S"): lat = -lat
        if gps.get("GPSLongitudeRef") in ("W", b"W"): lon = -lon
        # Coarse sanity check — anything outside this isn't a real coordinate
        if not (-90.0 <= lat <= 90.0 and -180.0 <= lon <= 180.0):
            return None, None
        return round(lat, 7), round(lon, 7)
    except Exception as e:
        logger.warning("EXIF GPS extraction failed: %s", e)
        return None, None


def _dms_to_deg(dms) -> float:
    """Convert (deg, min, sec) rationals (any iterable of floats) to decimal degrees."""
    d, m, s = (float(x) for x in dms)
    return d + m / 60.0 + s / 3600.0


# ── Suggested description (rule-based draft) ──────────────────────────────────

def _suggest_description(top_category: Optional[str],
                         user_text: Optional[str],
                         ocr_text: Optional[str]) -> Optional[str]:
    """
    Returns a draft description the citizen can accept or edit. Combines:
      - top AI-predicted category (e.g. "Road & Infrastructure")
      - user's existing free-text (if any)
      - OCR text extracted from an uploaded image (if any)

    Kept rule-based so it works without HF/Ollama; can be swapped for an LLM
    call later by replacing the body of this function.
    """
    parts: list[str] = []
    if top_category:
        parts.append(_category_intro(top_category))
    if user_text and len(user_text.strip()) >= 5:
        parts.append(f"Reporter's note: {user_text.strip()[:300]}")
    if ocr_text and len(ocr_text.strip()) >= 4:
        snippet = " ".join(ocr_text.split())[:200]
        parts.append(f"Text visible in the photo: \"{snippet}\".")
    if not parts:
        return None
    parts.append("Kindly inspect and resolve at the earliest.")
    return " ".join(parts)


_CATEGORY_INTRO = {
    "Road & Infrastructure": "There is a road / infrastructure issue that needs urgent attention (potholes, broken pavement, or unsafe footpath).",
    "Water Supply":          "A water supply / drainage problem has been observed in the area.",
    "Electricity":           "An electricity issue (faulty streetlight, exposed wiring, or repeated outage) requires inspection.",
    "Garbage & Sanitation":  "Garbage and sanitation issues are affecting the neighbourhood.",
    "Public Safety":         "A public safety concern has been observed and needs to be addressed.",
    "Parks & Trees":         "A park / trees issue (broken equipment, overgrown trees, or unsafe area) needs maintenance.",
    "Noise Pollution":       "A noise pollution complaint has been raised for this locality.",
    "Other":                 "An issue has been observed and reported for review.",
}

def _category_intro(category: str) -> str:
    return _CATEGORY_INTRO.get(category, _CATEGORY_INTRO["Other"])
