"""
FixMyCity AI Service — Model Manager (Hugging Face API edition)
Replace the original services/model_manager.py with this file.

Changes vs original:
  - load_sentence_model() → creates HFSentenceTransformer (no local download)
  - load_clip_model()     → sets a flag; CLIP calls go through hf_inference.py
  - load_keybert_model()  → creates HFKeyBERT (no local download)
  - load_toxicity_model() → sets a flag; toxicity calls go through hf_inference.py
  - Everything else (LightGBM, KNN, ALS, Prophet) unchanged — stays local.
"""

import os
import json
import logging
import numpy as np
import joblib
from pathlib import Path
from dataclasses import dataclass, field
from typing import Optional

logger = logging.getLogger("model_manager")


@dataclass
class ModelStore:
    """Holds all in-memory models and encoders."""
    # Resolution prediction
    resolution_classifier: Optional[object] = None
    resolution_regressor:  Optional[object] = None
    label_encoders:        dict = field(default_factory=dict)

    # Categorization
    category_knn:            Optional[object]     = None
    category_embeddings:     Optional[np.ndarray] = None
    category_labels:         Optional[list]       = None
    # Phase 3 (2026-05-19): added the two fields below.
    # `category_label_encoder` is a sklearn LabelEncoder fit on the unique
    # category names — `classes_` is the sorted name list used both as the
    # KNN's class space and as CLIP's candidate labels.
    # `category_name_to_id` is the name → DB CategoryId map; populated at
    # training time from a one-shot query against dbo.IssueCategories, and
    # consulted by _resolve_category_id() in routers/categorization.py so the
    # Angular submit form can patch the dropdown without re-fetching.
    category_label_encoder:  Optional[object]     = None
    category_name_to_id:     dict                 = field(default_factory=dict)

    # Sentence transformer — now an HFSentenceTransformer instance
    sentence_model: Optional[object] = None

    # CLIP — replaced by HF API flag; no local model object
    clip_model:     Optional[object] = None   # always None in HF mode
    clip_processor: Optional[object] = None   # always None in HF mode
    clip_hf_ready:  bool = False              # True once HF token verified

    # KeyBERT — now an HFKeyBERT instance
    keybert_model: Optional[object] = None

    # ALS (collaborative filtering) — unchanged, fully local
    als_model: Optional[object] = None
    als_user_factors:  Optional[np.ndarray] = None
    als_item_factors:  Optional[np.ndarray] = None
    als_user_map:      Optional[dict] = None
    als_item_map:      Optional[dict] = None

    # Toxicity — replaced by HF API flag
    toxicity_pipeline: Optional[object] = None  # always None in HF mode
    toxicity_hf_ready: bool = False

    # Versions
    version_rules: str  = "v1.0.0-rules"
    version_embed: str  = "v1.1.0-knn-hf"
    version_lgbm:  str  = "v2.0.0-lgbm"
    trained_count: int  = 0


_store = ModelStore()


def get_store() -> ModelStore:
    return _store


def load_sentence_model(model_name: str = "sentence-transformers/all-MiniLM-L6-v2"):
    """
    Creates an HFSentenceTransformer — no local model download.
    The model_name is passed to the HF API so it can be swapped freely.
    """
    global _store
    if _store.sentence_model is not None:
        return
    try:
        from services.hf_inference import HFSentenceTransformer
        logger.info("Initialising HF sentence embeddings (model=%s)", model_name)
        _store.sentence_model = HFSentenceTransformer(model_name)
        logger.info("HF sentence model ready.")
    except Exception as e:
        logger.warning("Could not initialise HF sentence model: %s — "
                       "duplicate/category features will degrade.", e)


def load_clip_model(clip_model_name: str = "openai/clip-vit-base-patch32"):
    """
    In HF mode there is nothing to download — just mark the flag.
    categorization.py checks store.clip_hf_ready and calls hf_zero_shot_image_classify().
    """
    global _store
    try:
        from services.hf_inference import _get_client
        _get_client()   # validates token
        _store.clip_hf_ready = True
        logger.info("HF CLIP ready (model=%s, API mode).", clip_model_name)
    except Exception as e:
        logger.warning("HF CLIP not available: %s", e)


def load_keybert_model():
    """Creates HFKeyBERT backed by the already-loaded HFSentenceTransformer."""
    global _store
    if _store.keybert_model is not None:
        return
    if _store.sentence_model is None:
        logger.warning("sentence_model not loaded yet — skipping KeyBERT init.")
        return
    try:
        from services.hf_inference import HFKeyBERT
        logger.info("Loading HFKeyBERT...")
        _store.keybert_model = HFKeyBERT(sentence_model=_store.sentence_model)
        logger.info("HFKeyBERT ready.")
    except Exception as e:
        logger.warning("HFKeyBERT not available: %s", e)


def load_toxicity_model():
    """In HF mode just validates the token and sets the ready flag."""
    global _store
    if _store.toxicity_hf_ready:
        return
    try:
        from services.hf_inference import _get_client
        _get_client()
        _store.toxicity_hf_ready = True
        logger.info("HF toxicity model ready (API mode).")
    except Exception as e:
        logger.warning("HF toxicity not available: %s", e)


def load_persisted_models(model_dir: str):
    """Restores LGBM + KNN + ALS from disk if they exist. Unchanged from original."""
    global _store
    model_path = Path(model_dir)
    if not model_path.exists():
        return

    cls_path     = model_path / "resolution_classifier.pkl"
    reg_path     = model_path / "resolution_regressor.pkl"
    enc_path     = model_path / "label_encoders.pkl"
    knn_path     = model_path / "category_knn.pkl"
    cat_emb_path = model_path / "category_embeddings.npy"
    cat_lbl_path = model_path / "category_labels.json"
    cat_enc_path = model_path / "category_label_encoder.pkl"   # Phase 3
    cat_map_path = model_path / "category_name_to_id.json"     # Phase 3
    als_path     = model_path / "als_model.pkl"

    try:
        if cls_path.exists():
            _store.resolution_classifier = joblib.load(cls_path)
            logger.info("Loaded resolution classifier from disk.")
        if reg_path.exists():
            _store.resolution_regressor = joblib.load(reg_path)
            logger.info("Loaded resolution regressor from disk.")
        if enc_path.exists():
            _store.label_encoders = joblib.load(enc_path)
        if knn_path.exists() and cat_emb_path.exists() and cat_lbl_path.exists():
            _store.category_knn        = joblib.load(knn_path)
            _store.category_embeddings = np.load(str(cat_emb_path))
            with open(cat_lbl_path) as f:
                _store.category_labels = json.load(f)
            logger.info("Loaded category KNN from disk.")
        if cat_enc_path.exists():
            _store.category_label_encoder = joblib.load(cat_enc_path)
            logger.info("Loaded category label encoder from disk.")
        if cat_map_path.exists():
            with open(cat_map_path) as f:
                _store.category_name_to_id = {k: int(v) for k, v in json.load(f).items()}
        if als_path.exists():
            als_data               = joblib.load(als_path)
            _store.als_model       = als_data.get("model")
            _store.als_user_factors = als_data.get("user_factors")
            _store.als_item_factors = als_data.get("item_factors")
            _store.als_user_map    = als_data.get("user_map")
            _store.als_item_map    = als_data.get("item_map")
            logger.info("Loaded ALS model from disk.")
    except Exception as e:
        logger.warning("Error loading persisted models: %s", e)


def save_models(model_dir: str):
    """Persists all trained models to disk. Unchanged from original."""
    global _store
    os.makedirs(model_dir, exist_ok=True)
    model_path = Path(model_dir)

    if _store.resolution_classifier:
        joblib.dump(_store.resolution_classifier, model_path / "resolution_classifier.pkl")
    if _store.resolution_regressor:
        joblib.dump(_store.resolution_regressor, model_path / "resolution_regressor.pkl")
    if _store.label_encoders:
        joblib.dump(_store.label_encoders, model_path / "label_encoders.pkl")
    if _store.category_knn:
        joblib.dump(_store.category_knn, model_path / "category_knn.pkl")
    if _store.category_embeddings is not None:
        np.save(str(model_path / "category_embeddings.npy"), _store.category_embeddings)
    if _store.category_labels:
        with open(model_path / "category_labels.json", "w") as f:
            json.dump(_store.category_labels, f)
    if _store.category_label_encoder is not None:
        joblib.dump(_store.category_label_encoder, model_path / "category_label_encoder.pkl")
    if _store.category_name_to_id:
        with open(model_path / "category_name_to_id.json", "w") as f:
            json.dump(_store.category_name_to_id, f)
    if _store.als_model:
        joblib.dump({
            "model":        _store.als_model,
            "user_factors": _store.als_user_factors,
            "item_factors": _store.als_item_factors,
            "user_map":     _store.als_user_map,
            "item_map":     _store.als_item_map,
        }, model_path / "als_model.pkl")

    logger.info("All models saved to %s", model_dir)