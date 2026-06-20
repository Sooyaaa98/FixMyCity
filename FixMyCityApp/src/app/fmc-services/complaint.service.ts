// src/app/fmc-services/complaint.service.ts

import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { environment } from '../../environments/environment';
import { IApiResponse } from '../fmc-interfaces/api-response.interface';
import {
  IComplaint,
  IComplaintTimeline,
  IComplaintAttachment,
  ISubmitComplaintRequest,
  ISubmitComplaintResponse,
  IUpdateStatusRequest,
  IUpdateEstDateRequest,
  IRateComplaintRequest,
  IReopenComplaintRequest,
  IAddAttachmentRequest,
  ILinkDuplicateRequest
} from '../fmc-interfaces/complaint.interface';

@Injectable({ providedIn: 'root' })
export class ComplaintService {

  private baseUrl = `${environment.apiBaseUrl}/api/Complaint`;

  constructor(private http: HttpClient) {}

  // ── Submit ────────────────────────────────────────────────────────────────

  submitComplaint(payload: ISubmitComplaintRequest): Observable<ISubmitComplaintResponse> {
    return this.http.post<ISubmitComplaintResponse>(`${this.baseUrl}/SubmitComplaint`, payload)
      .pipe(catchError(this.handleError));
  }

  /**
   * Uploads a single image (jpg/png/webp) via multipart form-data to the new
   * /api/Complaint/UploadComplaintImage endpoint. Returns the stored filename
   * so the caller can pass it to /api/ML/AnalyzeImage for AI suggestions.
   * X-Silent skips the global toast — the component handles its own UX.
   */
  uploadComplaintImage(file: File): Observable<{
    success: boolean;
    filePath?: string;
    fileName?: string;
    fileSizeKB?: number;
    message?: string;
  }> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<any>(`${this.baseUrl}/UploadComplaintImage`, form, {
      headers: { 'X-Silent': 'true' }
    }).pipe(catchError(this.handleError));
  }

  // ── Citizen reads ─────────────────────────────────────────────────────────
  // Returns bare array — no { success } wrapper

  getComplaintsByCitizen(citizenUserId: number): Observable<IComplaint[]> {
    return this.http.get<IComplaint[]>(
      `${this.baseUrl}/GetComplaintsByCitizen?citizenUserId=${citizenUserId}`
    ).pipe(catchError(this.handleError));
  }

  getComplaintById(complaintId: number): Observable<IComplaint> {
    return this.http.get<IComplaint>(
      `${this.baseUrl}/GetComplaintById?complaintId=${complaintId}`
    ).pipe(catchError(this.handleError));
  }

  // Returns bare array
  getTimeline(complaintId: number): Observable<IComplaintTimeline[]> {
    return this.http.get<IComplaintTimeline[]>(
      `${this.baseUrl}/GetTimeline?complaintId=${complaintId}`
    ).pipe(catchError(this.handleError));
  }

  // ── Filter & Search ───────────────────────────────────────────────────────

  filterComplaints(citizenUserId: number, status: string, localityId?: number): Observable<IComplaint[]> {
    let url = `${this.baseUrl}/FilterComplaints?citizenUserId=${citizenUserId}&status=${encodeURIComponent(status)}`;
    if (localityId != null) url += `&localityId=${localityId}`;
    return this.http.get<IComplaint[]>(url).pipe(catchError(this.handleError));
  }

  // Returns bare array
  getLocalityFeed(localityId: number): Observable<IComplaint[]> {
    return this.http.get<IComplaint[]>(
      `${this.baseUrl}/GetLocalityFeed?localityId=${localityId}`
    ).pipe(catchError(this.handleError));
  }

  search(keyword: string, categoryId?: number, localityId?: number): Observable<IComplaint[]> {
    let url = `${this.baseUrl}/Search?keyword=${encodeURIComponent(keyword)}`;
    if (categoryId) url += `&categoryId=${categoryId}`;
    if (localityId) url += `&localityId=${localityId}`;
    return this.http.get<IComplaint[]>(url).pipe(catchError(this.handleError));
  }

  // ── Map complaints — returns array directly ───────────────────────────────

  getMapComplaints(localityId?: number): Observable<IComplaint[]> {
    let url = `${this.baseUrl}/GetMapComplaints`;
    if (localityId != null) url += `?localityId=${localityId}`;
    return this.http.get<IComplaint[]>(url).pipe(catchError(this.handleError));
  }

  // ── Solver reads ──────────────────────────────────────────────────────────

  getComplaintsByDept(
    deptId: number, status = '', localityId?: number, criticality = ''
  ): Observable<IComplaint[]> {
    let url = `${this.baseUrl}/GetComplaintsByDept?deptId=${deptId}&status=${encodeURIComponent(status)}&criticality=${encodeURIComponent(criticality)}`;
    if (localityId != null) url += `&localityId=${localityId}`;
    return this.http.get<IComplaint[]>(url).pipe(catchError(this.handleError));
  }

  // ── Solver mutations ──────────────────────────────────────────────────────

  updateStatus(payload: IUpdateStatusRequest): Observable<IApiResponse> {
    return this.http.put<IApiResponse>(`${this.baseUrl}/UpdateStatus`, payload)
      .pipe(catchError(this.handleError));
  }

  setEstimatedDate(payload: IUpdateEstDateRequest): Observable<IApiResponse> {
    return this.http.put<IApiResponse>(`${this.baseUrl}/SetEstimatedDate`, payload)
      .pipe(catchError(this.handleError));
  }

  // ── Citizen mutations ─────────────────────────────────────────────────────

  rateComplaint(payload: IRateComplaintRequest): Observable<IApiResponse> {
    return this.http.post<IApiResponse>(`${this.baseUrl}/RateComplaint`, payload)
      .pipe(catchError(this.handleError));
  }

  reopenComplaint(payload: IReopenComplaintRequest): Observable<IApiResponse> {
    return this.http.put<IApiResponse>(`${this.baseUrl}/ReopenComplaint`, payload)
      .pipe(catchError(this.handleError));
  }

  // ── Attachments ───────────────────────────────────────────────────────────

  getAttachments(complaintId: number, attachmentType: string): Observable<IComplaintAttachment[]> {
    return this.http.get<IComplaintAttachment[]>(
      `${this.baseUrl}/GetAttachments?complaintId=${complaintId}&attachmentType=${encodeURIComponent(attachmentType)}`
    ).pipe(catchError(this.handleError));
  }

  addAttachment(payload: IAddAttachmentRequest): Observable<IApiResponse & { attachmentId: number }> {
    return this.http.post<IApiResponse & { attachmentId: number }>(
      `${this.baseUrl}/AddAttachment`, payload
    ).pipe(catchError(this.handleError));
  }

  // ── Duplicate management ──────────────────────────────────────────────────

  getCandidateDuplicates(localityId: number, categoryId: number, excludeComplaintId: number): Observable<IComplaint[]> {
    return this.http.get<IComplaint[]>(
      `${this.baseUrl}/GetCandidateDuplicates?localityId=${localityId}&categoryId=${categoryId}&excludeComplaintId=${excludeComplaintId}`
    ).pipe(catchError(this.handleError));
  }

  linkDuplicate(payload: ILinkDuplicateRequest): Observable<IApiResponse> {
    return this.http.put<IApiResponse>(`${this.baseUrl}/LinkDuplicate`, payload)
      .pipe(catchError(this.handleError));
  }

  // ── Categories shortcut ───────────────────────────────────────────────────

  getAllCategories(): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/GetAllCategories`)
      .pipe(catchError(this.handleError));
  }

  // ══════════════════════════════════════════════════════════════════════════
  //  Phase 8 — feature-suggestion endpoints
  // ══════════════════════════════════════════════════════════════════════════

  // ── §1 Upvotes ────────────────────────────────────────────────────────────

  toggleUpvote(complaintId: number, citizenUserId: number)
    : Observable<{ success: boolean; newCount: number; hasUpvoted: boolean }> {
    return this.http.post<any>(`${this.baseUrl}/ToggleUpvote`,
      { complaintId, citizenUserId })
      .pipe(catchError(this.handleError));
  }

  getUpvoteState(complaintId: number, citizenUserId: number)
    : Observable<{ success: boolean; count: number; hasUpvoted: boolean }> {
    return this.http.get<any>(
      `${this.baseUrl}/GetUpvoteState?complaintId=${complaintId}&citizenUserId=${citizenUserId}`
    ).pipe(catchError(this.handleError));
  }

  // ── §7 Comments ───────────────────────────────────────────────────────────

  addComment(payload: { complaintId: number; userId: number; commentText: string })
    : Observable<{ success: boolean; commentId: number }> {
    return this.http.post<any>(`${this.baseUrl}/AddComment`, payload)
      .pipe(catchError(this.handleError));
  }

  getComments(complaintId: number): Observable<Array<{
    commentId: number; complaintId: number; userId: number;
    commentText: string; isOfficialReply: boolean; createdAt: string;
    authorName?: string; authorRole?: string;
  }>> {
    return this.http.get<any[]>(`${this.baseUrl}/GetComments?complaintId=${complaintId}`)
      .pipe(catchError(this.handleError));
  }

  deleteComment(commentId: number, actingUserId: number): Observable<{ success: boolean }> {
    return this.http.request<{ success: boolean }>('delete',
      `${this.baseUrl}/DeleteComment`,
      { body: { commentId, actingUserId } })
      .pipe(catchError(this.handleError));
  }

  // ── §6 Appeals (citizen) ──────────────────────────────────────────────────

  submitAppeal(payload: { complaintId: number; citizenUserId: number; reason: string })
    : Observable<{ success: boolean; appealId: number }> {
    return this.http.post<any>(`${this.baseUrl}/SubmitAppeal`, payload)
      .pipe(catchError(this.handleError));
  }

  getMyAppeals(citizenUserId: number): Observable<any[]> {
    return this.http.get<any[]>(
      `${this.baseUrl}/GetMyAppeals?citizenUserId=${citizenUserId}`
    ).pipe(catchError(this.handleError));
  }

  // ── §15 Internal notes (Solver / SuperAdmin only) ─────────────────────────

  addInternalNote(payload: { complaintId: number; createdByUserId: number; noteText: string })
    : Observable<{ success: boolean; noteId: number }> {
    return this.http.post<any>(`${this.baseUrl}/AddInternalNote`, payload)
      .pipe(catchError(this.handleError));
  }

  getInternalNotes(complaintId: number): Observable<Array<{
    noteId: number; complaintId: number; noteText: string; createdAt: string;
    authorName?: string; authorRole?: string;
  }>> {
    return this.http.get<any[]>(
      `${this.baseUrl}/GetInternalNotes?complaintId=${complaintId}`
    ).pipe(catchError(this.handleError));
  }

  // ── §5 Near-me ────────────────────────────────────────────────────────────

  getNearby(lat: number, lng: number, radiusKm = 2, pageSize = 50)
    : Observable<Array<{
      complaintId: number; title: string; status: string; criticality: string;
      latitude: number; longitude: number; submittedAt: string;
      categoryId?: number; categoryName?: string;
      localityId?: number; localityName?: string;
      distanceKm: number;
    }>> {
    return this.http.get<any[]>(
      `${this.baseUrl}/GetNearby?lat=${lat}&lng=${lng}&radiusKm=${radiusKm}&pageSize=${pageSize}`
    ).pipe(catchError(this.handleError));
  }

  // ── Error Handler ─────────────────────────────────────────────────────────

  private handleError(error: HttpErrorResponse): Observable<never> {
    console.error('[ComplaintService]', error);
    return throwError(() => error);
  }
}
