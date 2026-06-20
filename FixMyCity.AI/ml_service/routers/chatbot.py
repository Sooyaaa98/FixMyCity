"""
FixMyCity AI — Chatbot Router (HF API edition)
Replace the original routers/chatbot.py with this file.

Changes vs original:
  - Calls hf_chat() and hf_chat_stream() instead of ollama.chat()
  - No Ollama dependency — no 2 GB llama3.2 download needed.
  - Uses mistralai/Mistral-7B-Instruct-v0.3 by default (free on HF).
  - System prompt and complaint lookup logic unchanged.
"""
import logging
import re
from typing import Optional, AsyncGenerator
from fastapi import APIRouter
from fastapi.responses import StreamingResponse
from pydantic import BaseModel

from services.database import fetch_df

logger = logging.getLogger("chatbot")
router = APIRouter(prefix="/ai", tags=["Chatbot"])

SYSTEM_PROMPT = """You are FixMyCity Assistant, a helpful civic engagement chatbot.
You help citizens of Bengaluru with the FixMyCity platform.

Key facts about FixMyCity:
- Citizens can submit civic complaints (potholes, water issues, power outages, garbage, etc.)
- Complaints are rated Low / Medium / High / Critical in urgency
- Government departments (Solvers) handle complaints
- PWG (Public Working Groups) like NGOs can assist with approved complaints
- Citizens can rate resolved complaints 1–5 stars
- Complaints rated below 3 stars can be re-opened
- Points are awarded for civic engagement (rating, contributing funds, submitting)
- Milestones and certificates reward active citizens

When asked about a specific complaint number (e.g. "complaint 5042"), use the
[LOOKUP] tag followed by the ID: [LOOKUP]5042
The system will inject the complaint status into your context automatically.

Be concise, friendly, and factual. If unsure, say so honestly.
Do not make up complaint statuses or user data."""


# ── Schemas ───────────────────────────────────────────────────────────────────

class ChatMessage(BaseModel):
    role:    str
    content: str

class ChatRequest(BaseModel):
    messages:             list[ChatMessage]
    stream:               bool = True
    complaint_lookup_id:  Optional[int] = None

class ChatResponse(BaseModel):
    reply: str


# ── Non-streaming ─────────────────────────────────────────────────────────────

@router.post("/chat", response_model=ChatResponse)
async def chat(req: ChatRequest):
    try:
        from services.hf_inference import hf_chat
        messages = _build_messages(req)
        reply    = hf_chat(messages)
        return ChatResponse(reply=reply)
    except Exception as e:
        logger.warning("HF chat failed: %s", e)
        return ChatResponse(
            reply="Sorry, I'm having trouble connecting to the AI model right now. "
                  "Please try again in a moment or visit our Help page."
        )


# ── Streaming ─────────────────────────────────────────────────────────────────

@router.post("/chat/stream")
async def chat_stream(req: ChatRequest):
    messages = _build_messages(req)
    return StreamingResponse(
        _stream_tokens(messages),
        media_type="text/event-stream",
    )


async def _stream_tokens(messages: list) -> AsyncGenerator[str, None]:
    try:
        from services.hf_inference import hf_chat_stream
        for token in hf_chat_stream(messages):
            yield f"data: {token}\n\n"
        yield "data: [DONE]\n\n"
    except Exception as e:
        logger.error("Streaming chat failed: %s", e)
        yield "data: Sorry, chatbot error occurred.\n\n"
        yield "data: [DONE]\n\n"


# ── Helpers ───────────────────────────────────────────────────────────────────

def _build_messages(req: ChatRequest) -> list[dict]:
    msgs = [{"role": "system", "content": SYSTEM_PROMPT}]

    context_injection = ""
    lookup_id         = req.complaint_lookup_id

    if not lookup_id and req.messages:
        last  = req.messages[-1].content
        match = re.search(r'\b(\d{4,6})\b', last)
        if match:
            lookup_id = int(match.group(1))

    if lookup_id:
        complaint_data = _lookup_complaint(lookup_id)
        if complaint_data:
            context_injection = f"\n\n[Complaint #{lookup_id} data]: {complaint_data}"

    for msg in req.messages:
        content = msg.content
        if context_injection and msg.role == "user" and msg == req.messages[-1]:
            content += context_injection
        msgs.append({"role": msg.role, "content": content})

    return msgs


def _lookup_complaint(complaint_id: int) -> Optional[str]:
    try:
        query = """
            SELECT c.Title, c.Status, c.Criticality,
                   c.SubmittedAt, c.ResolvedAt,
                   d.DeptName, cat.CategoryName
            FROM dbo.Complaints c
            LEFT JOIN dbo.Departments d   ON d.DeptId    = c.DeptId
            LEFT JOIN dbo.IssueCategories cat ON cat.CategoryId = c.CategoryId
            WHERE c.ComplaintId = ?
        """
        df = fetch_df(query, [complaint_id])
        if df.empty:
            return f"Complaint #{complaint_id} not found."
        r = df.iloc[0]
        return (f"Title: {r['Title']}, Status: {r['Status']}, "
                f"Category: {r['CategoryName']}, Dept: {r['DeptName']}, "
                f"Submitted: {str(r['SubmittedAt'])[:10]}")
    except Exception as e:
        logger.warning("Complaint lookup failed for %d: %s", complaint_id, e)
        return None