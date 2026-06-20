"""
FixMyCity AI — Hugging Face Inference Adapter
Drop-in replacement for local sentence-transformers, CLIP, Ollama, and toxic-bert.
All heavy models run on HF's free serverless Inference API — nothing downloaded locally.

Setup:
  1. Get a free token at https://huggingface.co/settings/tokens  (read-only token is fine)
  2. Add HF_API_TOKEN=hf_xxxx to your ml_service/.env file
  3. Copy this file to ml_service/services/hf_inference.py
"""

import os
import io
import logging
import numpy as np
from typing import Optional

logger = logging.getLogger("hf_inference")

EMBED_MODEL    = "sentence-transformers/all-MiniLM-L6-v2"
CLIP_MODEL     = "openai/clip-vit-base-patch32"
CHAT_MODEL     = "mistralai/Mistral-7B-Instruct-v0.3"   # free, no approval needed
TOXICITY_MODEL = "unitary/toxic-bert"


def _resolve_token() -> str:
    """
    Re-reads HF_API_TOKEN on every call so a late `load_dotenv()` (or a token
    set after this module was imported) is picked up. Previously the token
    was captured at import time and stale-empty if dotenv ran later, which
    caused every HF model load to skip with "HF_API_TOKEN is not set".
    """
    return os.getenv("HF_API_TOKEN", "") or os.getenv("HUGGINGFACE_HUB_TOKEN", "")


def _get_client():
    """Returns a HF InferenceClient. Raises if token is missing."""
    try:
        from huggingface_hub import InferenceClient
        token = _resolve_token()
        if not token:
            raise RuntimeError(
                "HF_API_TOKEN is not set. "
                "Get a free token at https://huggingface.co/settings/tokens "
                "and add it to ml_service/.env as HF_API_TOKEN=hf_xxxx"
            )
        return InferenceClient(token=token)
    except ImportError:
        raise RuntimeError("huggingface_hub not installed. Run: pip install huggingface_hub")


# ── Sentence Embeddings ───────────────────────────────────────────────────────

class HFSentenceTransformer:
    """
    Drop-in replacement for sentence_transformers.SentenceTransformer.
    Implements only the .encode() interface used by FixMyCity.
    Calls HF Inference API in batches of 32 to stay within rate limits.
    """

    def __init__(self, model_name: str = EMBED_MODEL):
        self.model_name = model_name
        self._client = _get_client()
        logger.info("HFSentenceTransformer ready (model=%s, API mode)", model_name)

    def encode(
        self,
        sentences,
        batch_size: int = 32,
        normalize_embeddings: bool = True,
        show_progress_bar: bool = False,
        **kwargs,
    ) -> np.ndarray:
        """
        Encodes a list of strings into a 2-D numpy array of shape (N, 384).
        Automatically batches to avoid HF API payload limits.
        """
        if isinstance(sentences, str):
            sentences = [sentences]

        all_vecs = []
        for i in range(0, len(sentences), batch_size):
            batch = sentences[i : i + batch_size]
            try:
                # feature_extraction returns list[list[float]]
                result = self._client.feature_extraction(batch, model=self.model_name)
                vecs   = np.array(result, dtype=np.float32)

                # HF sometimes returns (N, 1, D) — squeeze the middle dim
                if vecs.ndim == 3:
                    vecs = vecs[:, 0, :]

                # Handle single-sentence squeeze from HF (returns 1-D)
                if vecs.ndim == 1:
                    vecs = vecs.reshape(1, -1)

                all_vecs.append(vecs)
            except Exception as e:
                logger.error("HF embedding API call failed for batch %d: %s", i, e)
                # Return zero vectors as fallback so training doesn't crash
                dim = 384
                all_vecs.append(np.zeros((len(batch), dim), dtype=np.float32))

        embeddings = np.vstack(all_vecs)

        if normalize_embeddings:
            norms = np.linalg.norm(embeddings, axis=1, keepdims=True)
            norms = np.where(norms == 0, 1.0, norms)
            embeddings = embeddings / norms

        return embeddings


# ── CLIP Image Classification ─────────────────────────────────────────────────

def hf_zero_shot_image_classify(
    image,   # PIL.Image.Image
    candidate_labels: list[str],
    model: str = CLIP_MODEL,
) -> list[dict]:
    """
    Calls HF zero-shot-image-classification and returns
    [{"label": str, "score": float}, ...] sorted descending by score.

    Replacement for:
        inputs = clip_processor(text=labels, images=image, ...)
        outputs = clip_model(**inputs)
        probs   = outputs.logits_per_image.softmax(dim=1)[0].tolist()
    """
    client = _get_client()
    try:
        # Convert PIL to bytes
        buf = io.BytesIO()
        image.save(buf, format="JPEG")
        image_bytes = buf.getvalue()

        results = client.zero_shot_image_classification(
            image_bytes,
            candidate_labels=candidate_labels,
            model=model,
        )
        # HF returns list[{"label": ..., "score": ...}] already sorted desc
        return results
    except Exception as e:
        logger.error("HF CLIP API failed: %s", e)
        return []


# ── Chatbot (LLM) ─────────────────────────────────────────────────────────────

def hf_chat(
    messages: list[dict],
    model: str = CHAT_MODEL,
    max_tokens: int = 400,
    temperature: float = 0.3,
) -> str:
    """
    Calls HF chat_completion. Drop-in for:
        response = ollama.chat(model=..., messages=..., options=...)
        return response["message"]["content"]

    messages should be a list of {"role": "user"|"assistant"|"system", "content": str}
    """
    client = _get_client()
    try:
        response = client.chat_completion(
            messages=messages,
            model=model,
            max_tokens=max_tokens,
            temperature=temperature,
        )
        return response.choices[0].message.content or ""
    except Exception as e:
        logger.error("HF chat API failed: %s", e)
        return (
            "Sorry, I'm having trouble connecting to the AI model right now. "
            "Please try again in a moment or visit our Help page."
        )


def hf_chat_stream(messages: list[dict], model: str = CHAT_MODEL, max_tokens: int = 400):
    """
    Generator that yields string tokens for SSE streaming.
    Drop-in for Ollama's stream=True mode.
    """
    client = _get_client()
    try:
        stream = client.chat_completion(
            messages=messages,
            model=model,
            max_tokens=max_tokens,
            temperature=0.3,
            stream=True,
        )
        for chunk in stream:
            token = chunk.choices[0].delta.content
            if token:
                yield token
    except Exception as e:
        logger.error("HF streaming chat failed: %s", e)
        yield "Sorry, chatbot error occurred."


# ── Toxicity Detection ────────────────────────────────────────────────────────

def hf_toxicity_check(text: str, model: str = TOXICITY_MODEL) -> list[dict]:
    """
    Calls HF text-classification for toxicity.
    Returns list[{"label": str, "score": float}] (all labels).
    Drop-in for transformers pipeline("text-classification", model="unitary/toxic-bert").
    """
    client = _get_client()
    try:
        result = client.text_classification(text[:512], model=model)
        # HF returns list of dicts with "label" and "score"
        return result if isinstance(result, list) else [result]
    except Exception as e:
        logger.error("HF toxicity API failed: %s", e)
        return []


# ── KeyBERT shim (uses HFSentenceTransformer) ────────────────────────────────

class HFKeyBERT:
    """
    Minimal KeyBERT replacement using HF embeddings for keyword scoring.
    Uses cosine similarity between candidate ngrams and the full text embedding.
    """

    def __init__(self, sentence_model: HFSentenceTransformer):
        self.model = sentence_model

    def extract_keywords(
        self,
        text: str,
        keyphrase_ngram_range=(1, 2),
        stop_words="english",
        top_n: int = 5,
        use_maxsum: bool = True,
        nr_candidates: int = 20,
        **kwargs,
    ) -> list[tuple[str, float]]:
        """Returns [(keyword, score), ...] sorted descending."""
        from sklearn.feature_extraction.text import CountVectorizer

        # Extract ngram candidates
        try:
            vectorizer = CountVectorizer(
                ngram_range=keyphrase_ngram_range,
                stop_words=stop_words,
            ).fit([text])
            candidates = vectorizer.get_feature_names_out().tolist()
        except Exception:
            return []

        if not candidates:
            return []

        # Limit candidates
        candidates = candidates[:nr_candidates]

        # Embed document and candidates together
        all_texts  = [text] + candidates
        embeddings = self.model.encode(all_texts, normalize_embeddings=True)

        doc_vec   = embeddings[0]
        cand_vecs = embeddings[1:]

        # Cosine similarity (already normalized)
        scores = cand_vecs @ doc_vec

        # Rank and return top_n
        ranked = sorted(zip(candidates, scores.tolist()), key=lambda x: x[1], reverse=True)
        return ranked[:top_n]