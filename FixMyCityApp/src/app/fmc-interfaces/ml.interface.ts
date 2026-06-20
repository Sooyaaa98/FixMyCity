// src/app/fmc-interfaces/ml.interface.ts
// FIXES:
//   1. ICategorySuggestion now includes categoryId (was missing — AI category accept was broken)
//   2. IDuplicateResult now includes the real candidates[] shape from the API,
//      plus computed count and similarComplaintIds for backward compatibility.

// ── ML Scores (GET /api/ML/GetMLScores) ─────────────────────────────────────
export interface IMLScores {
  complaintId?: number;
  scoreId?: number;
  predictedResolutionDate?: string;
  resolutionProbability?: number;
  priorityScore?: number;
  modelVersion?: string;
  predictionModelVersion?: string;
}

// ── Complaint Tags (GET /api/ML/GetTags) ────────────────────────────────────
export interface IComplaintTag {
  tag: string;
  score?: number;
}

// ── Category Suggestion (POST /api/ML/CategorizeText) ───────────────────────
// FIX 1: Added categoryId — previously missing. Python now returns category_id
//         and .NET CategorySuggestion now includes CategoryId.
export interface ICategorySuggestion {
  categoryId:   number;   // ← FIX: was missing, causing accept button to do nothing
  categoryName: string;
  confidence:   number;
}

export interface ICategorizeTextResponse {
  success:               boolean;
  suggestions:           ICategorySuggestion[];
  suggestedDescription?: string;
}

// ── Duplicate Check (POST /api/ML/CheckDuplicates) ──────────────────────────
// FIX 2: The real API returns { success, result: { complaintId, candidates[], embeddingStored } }
//         The old interface expected { success, result: { count, similarComplaintIds[] } }
//         which never matched — hasDuplicates was always false.
//         New interface matches the real shape, with computed helpers for backward compat.

export interface IDuplicateCandidate {
  complaintId: number;
  similarity:  number;
  isDuplicate: boolean;
}

export interface IDuplicateResult {
  // Computed in ml.service.ts from candidates (backward-compatible)
  count:              number;
  similarComplaintIds: number[];
  // Raw from API
  candidates?:        IDuplicateCandidate[];
  embeddingStored?:   boolean;
}

export interface ICheckDuplicatesResponse {
  success: boolean;
  result:  IDuplicateResult;
}

// ── Image Analysis (POST /api/ML/AnalyzeImage) ───────────────────────────────
// Real response shape (matches FixMyCity.AI/ml_service/routers/categorization.py).
// The legacy fields below are kept commented out for reference.
export interface IImageAnalyzeResult {
  complaintId?:           number;
  suggestions?:           ICategorySuggestion[];
  ocrText?:               string;
  gpsLat?:                number;
  gpsLon?:                number;
  suggestedDescription?:  string;
}

export interface IImageAnalyzeResponse {
  success: boolean;
  result:  IImageAnalyzeResult;
}

// ── Photo upload (POST /api/Complaint/UploadComplaintImage) ─────────────────
export interface IPhotoUploadResponse {
  success:    boolean;
  filePath?:  string;       // basename used by Python's analyze-image
  fileName?:  string;
  fileSizeKB?: number;
  message?:   string;
}

// ── User Interests (GET /api/ML/GetUserInterests) ────────────────────────────
export interface IUserInterest {
  interestId:          number;
  userId:              number;
  categoryId?:         number;
  preferredLocalityId?: number;
}

// ── Recommendations (GET /api/ML/GetRecommendedComplaints) ───────────────────
export interface IRecommendedComplaint {
  complaintId: number;
}

// ── Chatbot (POST /api/ML/Chat) ──────────────────────────────────────────────
export interface IChatMessage {
  role:    'user' | 'assistant';
  content: string;
}

export interface IChatRequest {
  messages:          IChatMessage[];
  complaintLookupId?: number;
}

export interface IChatResponse {
  success: boolean;
  reply:   string;
}

// ── AI Health (GET /api/ML/CheckAIHealth) ────────────────────────────────────
export interface IAIHealthResponse {
  success:         boolean;
  aiServiceOnline: boolean;
}

// ── Request Payloads ─────────────────────────────────────────────────────────

export interface ICategorizeTextRequest {
  complaintId: number;
  title:       string;
  description: string;
}

export interface IAnalyzeImageRequest {
  complaintId: number;
  filePath:    string;
}

export interface ICheckDuplicatesRequest {
  complaintId: number;
  title:       string;
  description: string;
  categoryId:  number;
  localityId:  number;
  excludeId:   number;
}

export interface IAddUserInterestRequest {
  userId:               number;
  categoryId?:          number;
  preferredLocalityId?: number;
}

export interface IRemoveUserInterestRequest {
  userId:               number;
  categoryId?:          number;
  preferredLocalityId?: number;
}

export interface IForecastRequest {
  categoryId?: number;
  periods?:    number;
}
