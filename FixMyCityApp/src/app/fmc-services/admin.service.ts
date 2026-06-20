// src/app/fmc-services/admin.service.ts

import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { environment } from '../../environments/environment';
import { IApiResponse } from '../fmc-interfaces/api-response.interface';
import {
  IDeptDecisionRequest,
  IOrgDecisionRequest,
  IDeactivateUserRequest,
  IBanUserRequest,
  IManualEscalationRequest,
  IUserProfile
} from '../fmc-interfaces/user.interface';
import { IDepartment, IOrganisation, IPwgReport } from '../fmc-interfaces/pwg.interface';
import { IPlatformStats } from '../fmc-interfaces/gamification.interface';
import { IComplaint } from '../fmc-interfaces/complaint.interface';
import {
  IReviewPwgReportRequest,
  IClosePwgReportRequest
} from '../fmc-interfaces/pwg.interface';

@Injectable({ providedIn: 'root' })
export class AdminService {

  private baseUrl = `${environment.apiBaseUrl}/api/Admin`;

  constructor(private http: HttpClient) {}

  // ── Stats ──────────────────────────────────────────────────────────────

  getPlatformStats(): Observable<IPlatformStats> {
    return this.http.get<IPlatformStats>(`${this.baseUrl}/GetPlatformStats`)
      .pipe(catchError(this.handleError));
  }

  snapshotPlatformStats(): Observable<IApiResponse> {
    return this.http.post<IApiResponse>(`${this.baseUrl}/SnapshotPlatformStats`, null)
      .pipe(catchError(this.handleError));
  }

  // ── Pending Approvals ─────────────────────────────────────────────────

  getPendingDepartments(): Observable<IDepartment[]> {
    return this.http.get<IDepartment[]>(`${this.baseUrl}/GetPendingDepartments`)
      .pipe(catchError(this.handleError));
  }

  getPendingOrganisations(): Observable<IOrganisation[]> {
    return this.http.get<IOrganisation[]>(`${this.baseUrl}/GetPendingOrganisations`)
      .pipe(catchError(this.handleError));
  }

  decideDeptRegistration(payload: IDeptDecisionRequest): Observable<IApiResponse> {
    return this.http.put<IApiResponse>(`${this.baseUrl}/DecideDeptRegistration`, payload)
      .pipe(catchError(this.handleError));
  }

  decideOrgRegistration(payload: IOrgDecisionRequest): Observable<IApiResponse> {
    return this.http.put<IApiResponse>(`${this.baseUrl}/DecideOrgRegistration`, payload)
      .pipe(catchError(this.handleError));
  }

  // ── User Management ───────────────────────────────────────────────────

  getUsersByRole(roleName: string): Observable<IUserProfile[]> {
    return this.http.get<IUserProfile[]>(
      `${this.baseUrl}/GetUsersByRole?roleName=${encodeURIComponent(roleName)}`
    ).pipe(catchError(this.handleError));
  }

  deactivateUser(payload: IDeactivateUserRequest): Observable<IApiResponse> {
    return this.http.put<IApiResponse>(`${this.baseUrl}/DeactivateUser`, payload)
      .pipe(catchError(this.handleError));
  }

  banUser(payload: IBanUserRequest): Observable<IApiResponse> {
    return this.http.put<IApiResponse>(`${this.baseUrl}/BanUser`, payload)
      .pipe(catchError(this.handleError));
  }

  reactivateUser(payload: { targetUserId: number; reason: string; adminUserId: number }): Observable<IApiResponse> {
    return this.http.put<IApiResponse>(`${this.baseUrl}/ReactivateUser`, payload)
      .pipe(catchError(this.handleError));
  }

  // ── Complaints ────────────────────────────────────────────────────────

  getEscalatedComplaints(): Observable<IComplaint[]> {
    return this.http.get<IComplaint[]>(`${this.baseUrl}/GetEscalatedComplaints`)
      .pipe(catchError(this.handleError));
  }

  getAdminComplaints(status: string = ''): Observable<IComplaint[]> {
    return this.http.get<IComplaint[]>(
      `${this.baseUrl}/GetAllComplaints?status=${encodeURIComponent(status)}`
    ).pipe(catchError(this.handleError));
  }

  manualEscalation(payload: IManualEscalationRequest): Observable<IApiResponse> {
    return this.http.post<IApiResponse>(`${this.baseUrl}/ManualEscalation`, payload)
      .pipe(catchError(this.handleError));
  }

  getEscalationLog(complaintId: number): Observable<any[]> {
    return this.http.get<any[]>(
      `${this.baseUrl}/GetEscalationLog?complaintId=${complaintId}`
    ).pipe(catchError(this.handleError));
  }

  // ── Lookups ───────────────────────────────────────────────────────────

  getAllDepartments(): Observable<IDepartment[]> {
    return this.http.get<IDepartment[]>(`${this.baseUrl}/GetAllDepartments`)
      .pipe(catchError(this.handleError));
  }

  getAllCategories(): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/GetAllCategories`)
      .pipe(catchError(this.handleError));
  }

  getAuditLog(actionType: string = '', top: number = 100): Observable<any[]> {
    return this.http.get<any[]>(
      `${this.baseUrl}/GetAuditLog?actionType=${encodeURIComponent(actionType)}&top=${top}`
    ).pipe(catchError(this.handleError));
  }

  // ── PWG Report Management (US63) ──────────────────────────────────────

  getAllPwgReports(status: string = ''): Observable<IPwgReport[]> {
    return this.http.get<IPwgReport[]>(
      `${this.baseUrl}/GetAllPWGReports?status=${encodeURIComponent(status)}`
    ).pipe(catchError(this.handleError));
  }

  reviewPwgReport(payload: IReviewPwgReportRequest): Observable<IApiResponse> {
    return this.http.put<IApiResponse>(`${this.baseUrl}/ReviewPWGReport`, payload)
      .pipe(catchError(this.handleError));
  }

  closePwgReport(payload: IClosePwgReportRequest): Observable<IApiResponse> {
    return this.http.put<IApiResponse>(`${this.baseUrl}/ClosePWGReport`, payload)
      .pipe(catchError(this.handleError));
  }

  // ══════════════════════════════════════════════════════════════════════════
  //  Phase 8 — feature-suggestion admin endpoints
  // ══════════════════════════════════════════════════════════════════════════

  // ── §11/§16 Bulk update ───────────────────────────────────────────────────

  bulkUpdateStatus(payload: {
    complaintIds: number[]; newStatus: string;
    actorUserId: number; remark?: string;
  }): Observable<{ success: boolean; updatedCount: number }> {
    return this.http.post<any>(`${this.baseUrl}/BulkUpdateStatus`, payload)
      .pipe(catchError(this.handleError));
  }

  // ── §12 Manual reassignment ───────────────────────────────────────────────

  reassignDept(payload: {
    complaintId: number; newDeptId: number;
    adminUserId: number; reason: string;
  }): Observable<IApiResponse> {
    return this.http.put<IApiResponse>(`${this.baseUrl}/ReassignDept`, payload)
      .pipe(catchError(this.handleError));
  }

  // ── §6 Appeals (admin side) ───────────────────────────────────────────────

  getAppeals(status: string = ''): Observable<any[]> {
    return this.http.get<any[]>(
      `${this.baseUrl}/GetAppeals?status=${encodeURIComponent(status)}`
    ).pipe(catchError(this.handleError));
  }

  resolveAppeal(payload: {
    appealId: number; adminUserId: number;
    decision: 'Approved' | 'Rejected'; adminNote?: string;
  }): Observable<IApiResponse> {
    return this.http.post<IApiResponse>(`${this.baseUrl}/ResolveAppeal`, payload)
      .pipe(catchError(this.handleError));
  }

  // ── §9 Complaint trend (analytics) ────────────────────────────────────────

  getComplaintTrend(days: number = 30): Observable<Array<{
    date: string; count: number; resolved: number;
  }>> {
    return this.http.get<any[]>(
      `${this.baseUrl}/GetComplaintTrend?days=${days}`
    ).pipe(catchError(this.handleError));
  }

  // ── Error Handler ─────────────────────────────────────────────────────

  private handleError(error: HttpErrorResponse): Observable<never> {
    console.error('[AdminService]', error);
    return throwError(() => error);
  }
}
