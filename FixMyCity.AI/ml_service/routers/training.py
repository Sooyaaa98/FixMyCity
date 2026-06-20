"""
FixMyCity AI — Training Router
POST /ai/train retrains all models from current DB data.
Called by Admin dashboard or Azure Function Timer Trigger.
"""
import json
import logging
import numpy as np
from fastapi import APIRouter
from pydantic import BaseModel

from config import MODEL_DIR, EMBEDDING_MODEL
from services.database import (
    load_resolved_complaints_for_training,
    load_user_interactions_for_als,
    fetch_df,
)
from services import model_manager as mm

logger = logging.getLogger("training")
router = APIRouter(prefix="/ai", tags=["Training"])

MIN_SAMPLES_LGBM = 100   # don't train until we have enough resolved complaints
MIN_SAMPLES_ALS  = 50    # minimum interactions for ALS


class TrainResponse(BaseModel):
    success:          bool
    lgbm_trained:     bool
    knn_trained:      bool
    als_trained:      bool
    lgbm_samples:     int
    knn_samples:      int
    als_interactions: int
    message:          str


@router.post("/train", response_model=TrainResponse)
async def train_all_models():
    """
    Retrains: LightGBM resolution models, KNN categorizer, ALS recommender.
    Safe to call with insufficient data — gracefully skips models that need more rows.
    """
    result = {
        "lgbm_trained": False, "knn_trained": False, "als_trained": False,
        "lgbm_samples": 0, "knn_samples": 0, "als_interactions": 0,
    }
    store = mm.get_store()

    # ── 1. LightGBM — Resolution Prediction ──────────────────────────────────
    try:
        df = load_resolved_complaints_for_training()
        result["lgbm_samples"] = len(df)

        if len(df) >= MIN_SAMPLES_LGBM:
            from sklearn.preprocessing import LabelEncoder
            from sklearn.ensemble import GradientBoostingClassifier, GradientBoostingRegressor

            le_crit = LabelEncoder().fit(df["Criticality"])
            le_cat  = LabelEncoder().fit(df["CategoryId"].astype(str))

            df["CritCode"] = le_crit.transform(df["Criticality"])
            df["CatCode"]  = le_cat.transform(df["CategoryId"].astype(str))

            feature_cols = ["CatCode","CritCode","LocalityId","DaysToResolve",
                            "HasPWG","FundingAmount","WasEscalated","DescLength"]
            X = df[feature_cols].fillna(0).values

            y_class = df["IsResolved"].values
            y_reg   = df[df["IsResolved"] == 1]["DaysToResolve"].values
            X_res   = df[df["IsResolved"] == 1][feature_cols].fillna(0).values

            store.resolution_classifier = GradientBoostingClassifier(
                n_estimators=100, learning_rate=0.1, max_depth=4, random_state=42
            ).fit(X, y_class)

            if len(X_res) >= 30:
                store.resolution_regressor = GradientBoostingRegressor(
                    n_estimators=100, learning_rate=0.1, max_depth=4, random_state=42
                ).fit(X_res, y_reg)

            store.label_encoders = {"criticality": le_crit, "category": le_cat}
            store.trained_count  = len(df)
            result["lgbm_trained"] = True
            logger.info("LightGBM trained on %d samples.", len(df))
        else:
            logger.info("Not enough data for LightGBM (%d/%d).", len(df), MIN_SAMPLES_LGBM)
    except Exception as e:
        logger.error("LightGBM training failed: %s", e)

    # ── 2. KNN Categorizer ────────────────────────────────────────────────────
    try:
        if store.sentence_model is None:
            mm.load_sentence_model(EMBEDDING_MODEL)

        if store.sentence_model:
            cat_df = fetch_df("""
                SELECT DISTINCT c.Title, c.Description, cat.CategoryName
                FROM dbo.Complaints c
                INNER JOIN dbo.IssueCategories cat ON cat.CategoryId = c.CategoryId
                WHERE c.Title IS NOT NULL AND c.Description IS NOT NULL
            """)
            result["knn_samples"] = len(cat_df)

            if len(cat_df) >= 30:
                from sklearn.neighbors  import KNeighborsClassifier
                from sklearn.preprocessing import LabelEncoder

                texts  = (cat_df["Title"] + ". " + cat_df["Description"]).tolist()
                labels = cat_df["CategoryName"].tolist()

                embeddings = store.sentence_model.encode(
                    texts, batch_size=32, normalize_embeddings=True, show_progress_bar=False)

                knn = KNeighborsClassifier(
                    n_neighbors=min(5, len(set(labels))),
                    metric="cosine", algorithm="brute")
                knn.fit(embeddings, labels)

                store.category_knn        = knn
                store.category_embeddings = embeddings
                store.category_labels     = sorted(set(labels))

                # Phase 3 (2026-05-19): persist a LabelEncoder so categorize-text
                # has stable name→index mapping that survives a restart, and a
                # name→DB-CategoryId map so suggestions can patch the Angular
                # dropdown without a second lookup.
                store.category_label_encoder = LabelEncoder().fit(sorted(set(labels)))

                try:
                    id_df = fetch_df(
                        "SELECT CategoryId, CategoryName FROM dbo.IssueCategories")
                    store.category_name_to_id = {
                        str(row["CategoryName"]): int(row["CategoryId"])
                        for _, row in id_df.iterrows()
                    }
                except Exception as e:
                    logger.warning("Could not load CategoryId map: %s — "
                                   "fallback hash will be used.", e)

                result["knn_trained"] = True
                logger.info("KNN categorizer trained on %d complaints.", len(cat_df))
    except Exception as e:
        logger.error("KNN training failed: %s", e)

    # ── 3. ALS — Collaborative Filtering ─────────────────────────────────────
    try:
        int_df = load_user_interactions_for_als()
        result["als_interactions"] = len(int_df)

        if len(int_df) >= MIN_SAMPLES_ALS:
            import implicit
            import scipy.sparse as sparse

            # Map users and complaints to integer indices
            users = {u: i for i, u in enumerate(int_df["UserId"].dropna().unique())}
            items = {c: i for i, c in enumerate(int_df["ComplaintId"].dropna().unique())}

            rows, cols, data = [], [], []
            for _, row in int_df.iterrows():
                uid = row.get("UserId")
                cid = row.get("ComplaintId")
                if uid in users and cid in items:
                    rows.append(users[uid])
                    cols.append(items[cid])
                    data.append(float(row.get("Weight", 1.0)))

            if rows:
                mat = sparse.csr_matrix(
                    (data, (rows, cols)),
                    shape=(len(users), len(items)), dtype=np.float32)

                als = implicit.als.AlternatingLeastSquares(
                    factors=64, regularization=0.01, iterations=20, random_state=42)
                als.fit(mat)

                store.als_model       = als
                store.als_user_factors = als.user_factors
                store.als_item_factors = als.item_factors
                store.als_user_map    = users
                store.als_item_map    = items
                result["als_trained"] = True
                logger.info("ALS trained with %d users, %d items.", len(users), len(items))
    except Exception as e:
        logger.error("ALS training failed: %s", e)

    # ── Persist all models ────────────────────────────────────────────────────
    try:
        mm.save_models(MODEL_DIR)
    except Exception as e:
        logger.warning("Model save failed: %s", e)

    result["success"] = True
    result["message"] = (
        f"Training complete. "
        f"LGBM={'✓' if result['lgbm_trained'] else '✗ (insufficient data)'}, "
        f"KNN={'✓' if result['knn_trained'] else '✗'}, "
        f"ALS={'✓' if result['als_trained'] else '✗'}"
    )
    return TrainResponse(**result)
