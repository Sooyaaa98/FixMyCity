// src/app/fmc-services/pwg.service.ts

import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { environment } from '../../environments/environment';
import { IApiResponse } from '../fmc-interfaces/api-response.interface';
import { IComplaint } from '../fmc-interfaces/complaint.interface';
import {
  IDepartment,
  IOrganisation,
  IPwgParticipationRequest,
  ISubmitParticipationRequest,
  IProgressUpdateRequest,
  IResolvePwgRequest,
  IUpdateDeptProfileRequest,
  IUpdateOrgProfileRequest
} from '../fmc-interfaces/pwg.interface';

@Injectable({ providedIn: 'root' })
export class PwgService {

  private baseUrl = `${environment.apiBaseUrl}/api/PWG`;

  constructor(private http: HttpClient) {}

  // ── PWG: Open Complaints ──────────────────────────────────────────────────
  // Returns bare array — confirmed from controller
  getOpenComplaints(categoryId?: number, localityId?: number, criticality?: string): Observable<IComplaint[]> {
    let params: string[] = [];
    if (categoryId != null) params.push(`categoryId=${categoryId}`);
    if (localityId != null) params.push(`localityId=${localityId}`);
    if (criticality)        params.push(`criticality=${encodeURIComponent(criticality)}`);
    const qs = params.length ? `?${params.join('&')}` : '';
    return this.http.get<IComplaint[]>(`${this.baseUrl}/GetOpenComplaints${qs}`)
      .pipe(catchError(this.handleError));
  }

  // Returns { success, requestId }
  submitParticipationRequest(payload: ISubmitParticipationRequest): Observable<{ success: boolean; requestId: number }> {
    return this.http.post<{ success: boolean; requestId: number }>(
      `${this.baseUrl}/SubmitParticipationRequest`, payload
    ).pipe(catchError(this.handleError));
  }

  // ── PWG: My Requests ──────────────────────────────────────────────────────
  // Returns bare array
  getRequestsByOrg(orgId: number): Observable<IPwgParticipationRequest[]> {
    return this.http.get<IPwgParticipationRequest[]>(
      `${this.baseUrl}/GetRequestsByOrg?orgId=${orgId}`
    ).pipe(catchError(this.handleError));
  }

  progressUpdate(payload: IProgressUpdateRequest): Observable<IApiResponse> {
    return this.http.post<IApiResponse>(`${this.baseUrl}/ProgressUpdate`, payload)
      .pipe(catchError(this.handleError));
  }

  // ── PWG: Org Profile ──────────────────────────────────────────────────────
  // Takes userId (NOT orgId) — controller calls GetOrgByUserId
  getOrgProfile(userId: number): Observable<IOrganisation> {
    return this.http.get<IOrganisation>(`${this.baseUrl}/GetOrgProfile?userId=${userId}`)
      .pipe(catchError(this.handleError));
  }

  updateOrgProfile(payload: IUpdateOrgProfileRequest): Observable<IApiResponse> {
    return this.http.put<IApiResponse>(`${this.baseUrl}/UpdateOrgProfile`, payload)
      .pipe(catchError(this.handleError));
  }

  // ── Solver: PWG Requests ──────────────────────────────────────────────────
  getPendingRequestsForSolver(solverUserId: number): Observable<IPwgParticipationRequest[]> {
    return this.http.get<IPwgParticipationRequest[]>(
      `${this.baseUrl}/GetPendingRequestsForSolver?solverUserId=${solverUserId}`
    ).pipe(catchError(this.handleError));
  }

  getAllRequestsForSolver(solverUserId: number): Observable<IPwgParticipationRequest[]> {
    return this.http.get<IPwgParticipationRequest[]>(
      `${this.baseUrl}/GetAllRequestsForSolver?solverUserId=${solverUserId}`
    ).pipe(catchError(this.handleError));
  }

  resolvePWGRequest(payload: IResolvePwgRequest): Observable<IApiResponse> {
    return this.http.put<IApiResponse>(`${this.baseUrl}/ResolvePWGRequest`, payload)
      .pipe(catchError(this.handleError));
  }

  // ── Solver: Dept Profile ──────────────────────────────────────────────────
  // Takes userId (NOT deptId) — controller calls GetDeptByUserId
  getDeptProfile(userId: number): Observable<IDepartment> {
    return this.http.get<IDepartment>(`${this.baseUrl}/GetDeptProfile?userId=${userId}`)
      .pipe(catchError(this.handleError));
  }

  updateDeptProfile(payload: IUpdateDeptProfileRequest): Observable<IApiResponse> {
    return this.http.put<IApiResponse>(`${this.baseUrl}/UpdateDeptProfile`, payload)
      .pipe(catchError(this.handleError));
  }

  // ── Error Handler ─────────────────────────────────────────────────────────

  private handleError(error: HttpErrorResponse): Observable<never> {
    console.error('[PwgService]', error);
    return throwError(() => error);
  }
}
