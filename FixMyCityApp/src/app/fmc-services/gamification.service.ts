// src/app/fmc-services/gamification.service.ts

import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { environment } from '../../environments/environment';
import { IApiResponse } from '../fmc-interfaces/api-response.interface';
import {
  INotification,
  IUserPoint,
  IScoreboardEntry,
  ICertificate
} from '../fmc-interfaces/gamification.interface';

@Injectable({ providedIn: 'root' })
export class GamificationService {

  private baseUrl = `${environment.apiBaseUrl}/api/Gamification`;

  constructor(private http: HttpClient) {}

  // ── Notifications ─────────────────────────────────────────────────────────

  // Returns bare array
  getUnreadNotifications(userId: number): Observable<INotification[]> {
    return this.http.get<INotification[]>(
      `${this.baseUrl}/GetUnreadNotifications?userId=${userId}`
    ).pipe(catchError(this.handleError));
  }

  // Returns bare array
  getAllNotifications(userId: number): Observable<INotification[]> {
    return this.http.get<INotification[]>(
      `${this.baseUrl}/GetAllNotifications?userId=${userId}`
    ).pipe(catchError(this.handleError));
  }

  markAllRead(userId: number): Observable<IApiResponse> {
    return this.http.put<IApiResponse>(
      `${this.baseUrl}/MarkAllRead?userId=${userId}`, null
    ).pipe(catchError(this.handleError));
  }

  markOneRead(notificationId: number): Observable<IApiResponse> {
    return this.http.put<IApiResponse>(
      `${this.baseUrl}/MarkOneRead?notificationId=${notificationId}`, null
    ).pipe(catchError(this.handleError));
  }

  archiveNotification(userId: number, notificationId: number): Observable<IApiResponse> {
    return this.http.put<IApiResponse>(
      `${this.baseUrl}/ArchiveNotification`, { userId, notificationId }
    ).pipe(catchError(this.handleError));
  }

  // ── Points ────────────────────────────────────────────────────────────────

  // Returns object directly (no wrapper)
  getUserPoints(userId: number): Observable<IUserPoint> {
    return this.http.get<IUserPoint>(
      `${this.baseUrl}/GetUserPoints?userId=${userId}`
    ).pipe(catchError(this.handleError));
  }

  // ── Scoreboard ────────────────────────────────────────────────────────────

  // Returns bare array — localityId is a number (FK)
  getScoreboard(localityId: number): Observable<IScoreboardEntry[]> {
    return this.http.get<IScoreboardEntry[]>(
      `${this.baseUrl}/GetScoreboard?localityId=${localityId}`
    ).pipe(catchError(this.handleError));
  }

  // ── Certificates ──────────────────────────────────────────────────────────

  // Returns bare array
  getCertificates(userId: number): Observable<ICertificate[]> {
    return this.http.get<ICertificate[]>(
      `${this.baseUrl}/GetCertificates?userId=${userId}`
    ).pipe(catchError(this.handleError));
  }

  /**
   * Downloads a certificate PDF from /api/Report/CertificatePdf.
   * The endpoint requires JWT auth, so we cannot use a plain anchor target=_blank;
   * we fetch as a blob (the AuthInterceptor attaches the Bearer token) and
   * synthesise an <a download> click. Seeded certificates have FilePath = NULL
   * because the PDF is generated on demand by QuestPdfService.
   */
  downloadCertificatePdf(certificateId: number, milestone: string): void {
    const url = `${environment.apiBaseUrl}/api/Report/CertificatePdf?certificateId=${certificateId}`;
    this.http.get(url, { responseType: 'blob' }).subscribe({
      next: (blob) => {
        const objectUrl = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = objectUrl;
        a.download = `FixMyCity_Certificate_${milestone.replace(/\s+/g, '_')}_${certificateId}.pdf`;
        document.body.appendChild(a);
        a.click();
        a.remove();
        // Defer revoke so the browser can complete the download
        setTimeout(() => URL.revokeObjectURL(objectUrl), 1000);
      },
      error: (err) => {
        console.error('[GamificationService] Certificate download failed', err);
      }
    });
  }

  // ── Error Handler ─────────────────────────────────────────────────────────

  private handleError(error: HttpErrorResponse): Observable<never> {
    console.error('[GamificationService]', error);
    return throwError(() => error);
  }
}
