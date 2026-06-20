# FixMyCity

> A full-stack civic-complaint platform for Bengaluru — citizens file issues, departments resolve them, NGOs volunteer to help, and an AI layer handles routing, duplicate detection, prioritisation, and a conversational assistant.

<p align="left">
  <img alt=".NET 8" src="https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white" />
  <img alt="Angular 15" src="https://img.shields.io/badge/Angular-15-DD0031?logo=angular&logoColor=white" />
  <img alt="SQL Server" src="https://img.shields.io/badge/SQL%20Server-2019%2B-CC2927?logo=microsoftsqlserver&logoColor=white" />
  <img alt="Python" src="https://img.shields.io/badge/Python-3.11-3776AB?logo=python&logoColor=white" />
  <img alt="FastAPI" src="https://img.shields.io/badge/FastAPI-Microservice-009688?logo=fastapi&logoColor=white" />
  <img alt="JWT" src="https://img.shields.io/badge/Auth-JWT%20%2B%20Refresh-000000?logo=jsonwebtokens&logoColor=white" />
  <img alt="Docker" src="https://img.shields.io/badge/Docker-Compose-2496ED?logo=docker&logoColor=white" />
</p>

---

## Overview

**FixMyCity** is a production-grade civic engagement platform built around four distinct user roles — **Citizen**, **Solver** (municipal department), **PWG** (public-welfare group / NGO), and **SuperAdmin**. It implements the full lifecycle of a complaint from submission to resolution, with AI assistance at every step.

Built as a multi-tier system: an **Angular** SPA, a **.NET 8 Web API**, a **Python FastAPI** AI microservice, and a **SQL Server** backend with stored procedures and row-level security.

---

## Highlights

- **65 user stories** delivered end-to-end across authentication, complaint lifecycle, gamification, PWG workflows, solver dashboards, ML scoring, and admin tooling.
- **JWT auth with refresh-token rotation** (15-min access / 7-day refresh), SHA-256-hashed refresh tokens, server-side revocation.
- **SQL Server Row-Level Security** populated from JWT claims via custom middleware — citizens see their own data, solvers see only their department's, admins see all.
- **AI microservice** (FastAPI + Hugging Face / Groq / Gemini) for duplicate detection (sentence embeddings), image classification (CLIP), toxicity moderation, keyword tagging, and a Mistral-powered chatbot.
- **Deterministic AI scoring model** combining criticality, age, funding, and escalation signals for priority ranking.
- **Auto-routing engine** that assigns complaints to the correct department by category + locality, with a hosted background service for daily escalation.
- **QuestPDF** report generation for citizen certificates and complaint summaries.
- **Hardened HTTP layer:** CSP, X-Frame-Options, X-Content-Type-Options, no-store on `/api/*`, rate-limited login (10/min/IP), constant-time service-key comparison.

---

## Architecture

```
                    ┌──────────────────────────┐
                    │   Angular 15 SPA         │
                    │   (FixMyCityApp)         │
                    └──────────────┬───────────┘
                                   │  HTTPS + JWT
                    ┌──────────────▼───────────┐
                    │   .NET 8 Web API         │
                    │   (FixMyCity.API)        │
                    │   Controllers / JWT      │
                    │   Middleware / RLS       │
                    │   QuestPDF reports       │
                    └────┬──────────────┬──────┘
                         │              │
              EF Core    │              │  X-AI-Service-Key
                         │              │
        ┌────────────────▼─────┐  ┌─────▼──────────────────┐
        │  SQL Server          │  │  FastAPI AI service    │
        │  28 tables           │  │  (FixMyCity.AI)        │
        │  36 stored procs     │  │  HF / Groq / Gemini    │
        │  Row-Level Security  │  │  LightGBM scoring      │
        └──────────────────────┘  └────────────────────────┘
```

---

## Tech Stack

| Layer | Stack |
|---|---|
| Frontend | Angular 15, TypeScript, RxJS, SCSS |
| Backend | .NET 8, ASP.NET Core Web API, EF Core, QuestPDF |
| Auth | JWT (HS256) with refresh-token rotation |
| Database | SQL Server 2019+, stored procedures, Row-Level Security |
| AI service | Python 3.11, FastAPI, Hugging Face Inference API, Groq, Google Gemini |
| ML | sentence-transformers (MiniLM), CLIP, Mistral-7B, LightGBM |
| Storage | Cloudinary (images) with local-disk fallback |
| Payments | Razorpay (citizen contributions) |
| Deployment | Docker Compose for the AI microservice |

---

## Repository Layout

```
FixMyCity/
├── FixMyCity.API/     → .NET 8 Web API (controllers, JWT, middleware, QuestPDF)
├── FixMyCity.DAL/     → Data access layer (EF Core, repositories, DTOs)
├── FixMyCity.AI/      → Python FastAPI AI microservice + Docker
│   ├── ml_service/
│   ├── docker-compose.yml
│   └── sql/
├── FixMyCityApp/      → Angular 15 frontend
├── Database/          → SQL scripts (schema + seed data)
└── README.md
```

---

## Quick Start

### Prerequisites

| Tool | Version |
|---|---|
| SQL Server | 2019+ or LocalDB |
| .NET SDK | 8.0+ |
| Python | 3.11 |
| Node.js | 18+ |
| Hugging Face token | free — [huggingface.co/settings/tokens](https://huggingface.co/settings/tokens) |

### 1. Database

```sql
CREATE DATABASE FixMyCityDB;
GO
USE FixMyCityDB;
GO
```

Then run the scripts in `Database/` in order:

```
01_AI_Tables_Addition.sql    -- AI tables (embeddings, tags)
02_UserRefreshTokens.sql     -- JWT refresh-token storage
03_SeedData.sql              -- 19 users, 30 complaints, full lifecycle
```

### 2. .NET API

Fill in real values in a local `appsettings.Development.json` (gitignored). The committed `appsettings.json` only contains placeholders.

```bash
cd FixMyCity.API
dotnet restore
dotnet run
```

Swagger UI at **http://localhost:5065/swagger**.

### 3. AI microservice

```bash
cd FixMyCity.AI/ml_service
cp .env.example .env        # then fill in HF_API_TOKEN
pip install -r requirements.txt
uvicorn main:app --host 0.0.0.0 --port 8001 --reload
```

Health check: `curl http://localhost:8001/health` → `{"status":"ok"}`.

Or with Docker: `cd FixMyCity.AI && docker-compose up -d`.

### 4. Angular app

```bash
cd FixMyCityApp
npm install
ng serve --port 4200
```

Open **http://localhost:4200**.

### 5. Sample logins

All seeded passwords: `Password123!`

| Role | Email |
|---|---|
| SuperAdmin | `admin@fixmycity.in` |
| Solver (BBMP / roads) | `rakesh.bbmp@fixmycity.in` |
| Solver (BWSSB / water) | `priya.bwssb@fixmycity.in` |
| Solver (BESCOM / power) | `suresh.bescom@fixmycity.in` |
| PWG | `anjali@cleanbengaluru.org` |
| Citizen | `arjun.r@example.com` |

---

## Security

| Concern | Mechanism |
|---|---|
| Authentication | JWT (15-min access + 7-day rotating refresh) |
| Authorization | `[Authorize(Roles=…)]` on every controller |
| Refresh tokens | SHA-256 hashed in DB, server-side revocable |
| Multi-tenancy | SQL Server Row-Level Security from JWT claims |
| XSS / clickjacking | CSP + `X-Frame-Options: DENY` + no `innerHTML` |
| Brute force | 10 login attempts / min / IP |
| Service-to-service | Constant-time `X-AI-Service-Key` check |
| Token leaks | `Cache-Control: no-store` on `/api/*` |
| MIME sniffing | `X-Content-Type-Options: nosniff` |

---

## AI Features

| Feature | Model | Endpoint |
|---|---|---|
| Duplicate detection | `sentence-transformers/all-MiniLM-L6-v2` | `POST /api/ML/CheckDuplicates` |
| Image classification | `openai/clip-vit-base-patch32` | `POST /api/ML/AnalyzeImage` |
| Conversational assistant | `mistralai/Mistral-7B-Instruct-v0.3` | `POST /ai/chat` |
| Toxicity moderation | `unitary/toxic-bert` | `POST /ai/check-toxicity` |
| Keyword tagging | HF embeddings + KeyBERT | `POST /ai/tag-complaint` |
| Priority / ETA / probability | LightGBM regressor | `POST /api/ML/score-complaint` |

All HF calls go through the serverless Inference API — no local model downloads required, only an `HF_API_TOKEN` in `.env`.

---

## User-story coverage (65 stories)

Grouped by area. Every story has a corresponding endpoint, repository method, and (where applicable) Angular component.

- **A. Auth & onboarding (US01–US09)** — registration for citizens / departments / NGOs, JWT login, SSO, profile management, account anonymisation.
- **B. Super Admin (US10–US13)** — approve departments and NGOs, platform stats, ban / deactivate.
- **C. Complaint lifecycle (US14–US24)** — submit, history, timeline, filter, rating, re-open, locality feed, contributions, notifications, recommendations.
- **D. Gamification (US25–US27)** — scoreboard, interests, certificate PDFs.
- **E. PWG (US28–US35)** — browse open complaints, request to help, progress updates, org profile.
- **F. Solver (US36–US47)** — department dashboard, status updates, ETA, PWG request approvals, reporting.
- **G. System (US48–US50)** — auto-routing, duplicate detection, daily auto-escalation hosted service.
- **H. ML (US51–US54)** — recommendations, resolution-time prediction, success probability, priority score.
- **I. Gap stories (US55–US65)** — reassignment, funding visibility, search, share, map view, resolution photos, FAQ, weekly digest cron.

---

## Known limitations

- First Hugging Face call is slow (~10s cold start); subsequent calls are fast.
- CLIP classification at scale requires a paid HF tier.
- OCR (pytesseract) is optional and silently no-ops if Tesseract isn't installed.
- Passwords use SHA-256 (existing scheme) — bcrypt / Argon2 is recommended for production.

---

## License

Educational / portfolio project. QuestPDF is used under its Community license. Hugging Face / Groq / Gemini free tiers cover demo-level traffic; production use requires paid plans.

---

## Author

**Syamantak May** — full-stack & AI engineering. Reach me at [syamantakmay@gmail.com](mailto:syamantakmay@gmail.com).
