// src/app/fmc-services/ml.service.ts
// FIXES:
//   1. categorizeText: response mapping handles the category_id field correctly
//   2. checkDuplicates: FIX — maps candidates[] to {count, similarComplaintIds[]}
//      so hasDuplicates and the warning panel actually work

import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, of, throwError } from 'rxjs';
import { catchError, map } from 'rxjs/operators';

import { environment } from '../../environments/environment';
import { IApiResponse } from '../fmc-interfaces/api-response.interface';
import {
  IMLScores,
  IComplaintTag,
  ICategorySuggestion,
  ICategorizeTextRequest,
  ICategorizeTextResponse,
  ICheckDuplicatesRequest,
  ICheckDuplicatesResponse,
  IDuplicateResult,
  IAnalyzeImageRequest,
  IImageAnalyzeResult,
  IForecastRequest,
  IChatRequest,
  IChatResponse,
  IAIHealthResponse,
  IUserInterest,
  IRecommendedComplaint,
  IAddUserInterestRequest,
  IRemoveUserInterestRequest,
  IDuplicateCandidate,
} from '../fmc-interfaces/ml.interface';

@Injectable({ providedIn: 'root' })
export class MlService {

  private baseUrl = `${environment.apiBaseUrl}/api/ML`;

  constructor(private http: HttpClient) {}

  // ── AI Health ─────────────────────────────────────────────────────────────

  checkAIHealth(): Observable<IAIHealthResponse> {
    return this.http.get<IAIHealthResponse>(`${this.baseUrl}/CheckAIHealth`)
      .pipe(catchError(() => of({ success: false, aiServiceOnline: false })));
  }

  // ── Read Endpoints ────────────────────────────────────────────────────────

  getMLScores(complaintId: number): Observable<IMLScores | null> {
    return this.http.get<any>(`${this.baseUrl}/GetMLScores?complaintId=${complaintId}`)
      .pipe(
        map(res => {
          if (res && res.success === false) return null;
          if (!res) return null;
          return {
            ...res,
            modelVersion: res.predictionModelVersion ?? res.modelVersion ?? 'N/A'
          } as IMLScores;
        }),
        catchError(() => of(null))
      );
  }

  getTags(complaintId: number): Observable<IComplaintTag[]> {
    return this.http.get<IComplaintTag[]>(`${this.baseUrl}/GetTags?complaintId=${complaintId}`)
      .pipe(catchError(() => of([])));
  }

  getRecommendedComplaints(userId: number, topN = 10): Observable<IRecommendedComplaint[]> {
    return this.http.get<IRecommendedComplaint[]>(
      `${this.baseUrl}/GetRecommendedComplaints?userId=${userId}&topN=${topN}`
    ).pipe(catchError(() => of([])));
  }

  getUserInterests(userId: number): Observable<IUserInterest[]> {
    return this.http.get<IUserInterest[]>(
      `${this.baseUrl}/GetUserInterests?userId=${userId}`
    ).pipe(catchError(() => of([])));
  }

  // ── AI Feature Endpoints ──────────────────────────────────────────────────

  // FIX 1: categorizeText — the response now includes categoryId from the API.
  // No special mapping needed; the interface matches the wire format.
  categorizeText(payload: ICategorizeTextRequest): Observable<ICategorySuggestion[]> {
    return this.http.post<ICategorizeTextResponse>(`${this.baseUrl}/CategorizeText`, payload)
      .pipe(
        map(res => res.suggestions ?? []),
        catchError(() => of([]))
      );
  }

  /**
   * Full categorize-text response (suggestions + AI-drafted description).
   * Used by the "Suggest description" button on the submit-complaint form.
   * Falls back to an empty response on AI failure — the citizen can still type
   * their own description.
   */
  categorizeTextFull(payload: ICategorizeTextRequest): Observable<ICategorizeTextResponse> {
    return this.http.post<ICategorizeTextResponse>(`${this.baseUrl}/CategorizeText`, payload)
      .pipe(catchError(() => of({ success: false, suggestions: [] } as ICategorizeTextResponse)));
  }

  // FIX 2: checkDuplicates — API returns candidates[], but the AiHintPanel expects
  //         {count, similarComplaintIds[]}.  We map here so both old and new code work.
  checkDuplicates(payload: ICheckDuplicatesRequest): Observable<ICheckDuplicatesResponse> {
    return this.http.post<{ success: boolean; result: any }>(`${this.baseUrl}/CheckDuplicates`, payload)
      .pipe(
        map(res => {
          if (!res.success || !res.result) {
            return { success: false, result: { count: 0, similarComplaintIds: [], candidates: [] } };
          }

          const rawResult = res.result;

          // API shape: { complaintId, candidates: [{complaintId, similarity, isDuplicate}], embeddingStored }
          const candidates: IDuplicateCandidate[] = rawResult.candidates ?? [];
          const duplicates  = candidates.filter((c: IDuplicateCandidate) => c.isDuplicate);

          const mapped: IDuplicateResult = {
            // Computed for backward compatibility with AiHintPanel
            count:              duplicates.length,
            similarComplaintIds: duplicates.map((c: IDuplicateCandidate) => c.complaintId),
            // Raw shape (available if components want richer data)
            candidates,
            embeddingStored:    rawResult.embeddingStored ?? false,
          };

          return { success: true, result: mapped };
        }),
        catchError(() => of({
          success: false,
          result:  { count: 0, similarComplaintIds: [], candidates: [] }
        }))
      );
  }

  analyzeImage(payload: IAnalyzeImageRequest): Observable<IImageAnalyzeResult | null> {
    return this.http.post<{ success: boolean; result: IImageAnalyzeResult }>(
      `${this.baseUrl}/AnalyzeImage`, payload
    ).pipe(
      map(res => res.result ?? null),
      catchError(() => of(null))
    );
  }

  getForecast(payload: IForecastRequest): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/GetForecast`, payload)
      .pipe(catchError(() => of(null)));
  }

  chat(payload: IChatRequest): Observable<IChatResponse> {
    return this.http.post<IChatResponse>(`${this.baseUrl}/Chat`, payload)
      .pipe(
        catchError(() => of({
          success: false,
          reply: 'The chatbot is temporarily unavailable. Please try again later.'
        }))
      );
  }

  // ── User Interest Management ──────────────────────────────────────────────

  addUserInterest(payload: IAddUserInterestRequest): Observable<IApiResponse> {
    return this.http.post<IApiResponse>(`${this.baseUrl}/AddUserInterest`, payload)
      .pipe(catchError(this.handleError));
  }

  removeUserInterest(payload: IRemoveUserInterestRequest): Observable<IApiResponse> {
    return this.http.delete<IApiResponse>(`${this.baseUrl}/RemoveUserInterest`, { body: payload })
      .pipe(catchError(this.handleError));
  }

  // ── Error Handler ─────────────────────────────────────────────────────────

  private handleError(error: HttpErrorResponse): Observable<never> {
    console.error('[MlService]', error);
    return throwError(() => error);
  }
}
