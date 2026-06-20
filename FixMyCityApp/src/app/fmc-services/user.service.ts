// src/app/fmc-services/user.service.ts
//
// Phase 8 — talks to /api/User/* endpoints.
// Currently just the activity feed (§20); auth flows still live in
// AuthService and admin-managed profile reads in AdminService.

import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { environment } from '../../environments/environment';

export interface IActivityFeedRow {
  eventType:    'ComplaintSubmitted' | 'StatusChange' | 'PointsAwarded'
              | 'CertificateIssued' | 'CommentPosted' | string;
  description:  string;
  relatedId?:   number;
  createdAt:    string;
}

@Injectable({ providedIn: 'root' })
export class UserActivityService {

  private baseUrl = `${environment.apiBaseUrl}/api/User`;

  constructor(private http: HttpClient) {}

  getActivityFeed(userId: number, pageNum = 1, pageSize = 20)
    : Observable<IActivityFeedRow[]> {
    return this.http.get<IActivityFeedRow[]>(
      `${this.baseUrl}/GetActivityFeed?userId=${userId}&pageNum=${pageNum}&pageSize=${pageSize}`
    ).pipe(catchError(this.handleError));
  }

  private handleError(error: HttpErrorResponse): Observable<never> {
    console.error('[UserActivityService]', error);
    return throwError(() => error);
  }
}
