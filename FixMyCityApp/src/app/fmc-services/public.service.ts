// src/app/fmc-services/public.service.ts
//
// Phase 8 (§17) — talks to the anonymous transparency-portal endpoints.
// No JWT is required; the AuthInterceptor knows to skip the Authorization
// header when X-Public is set on a request, so anonymous visitors can browse
// the portal even when the citizen app's cached token is expired / missing.

import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { environment } from '../../environments/environment';

export interface IPublicFeedRow {
  complaintId:   number;
  title:         string;
  description:   string;
  status:        string;
  criticality:   string;
  submittedAt:   string;
  resolvedAt?:   string;
  latitude?:     number;
  longitude?:    number;
  categoryId?:   number;
  categoryName?: string;
  localityId?:   number;
  localityName?: string;
  deptId?:       number;
  deptName?:     string;
  upvoteCount:   number;
}

export interface IPublicLocality {
  localityId:   number;
  localityName: string;
  city:         string;
}

@Injectable({ providedIn: 'root' })
export class PublicService {

  private baseUrl  = `${environment.apiBaseUrl}/api/Public`;
  // Marker header so the AuthInterceptor can skip the Bearer-token attach step
  // (the request is intentionally anonymous).
  private headers  = new HttpHeaders({ 'X-Public': 'true' });

  constructor(private http: HttpClient) {}

  getFeed(filters: {
    localityId?: number;
    categoryId?: number;
    status?: string;
    keyword?: string;
    pageNum?:  number;
    pageSize?: number;
  } = {}): Observable<IPublicFeedRow[]> {
    const params: string[] = [];
    if (filters.localityId != null) params.push(`localityId=${filters.localityId}`);
    if (filters.categoryId != null) params.push(`categoryId=${filters.categoryId}`);
    if (filters.status)             params.push(`status=${encodeURIComponent(filters.status)}`);
    if (filters.keyword)            params.push(`keyword=${encodeURIComponent(filters.keyword)}`);
    params.push(`pageNum=${filters.pageNum ?? 1}`);
    params.push(`pageSize=${filters.pageSize ?? 20}`);
    return this.http.get<IPublicFeedRow[]>(
      `${this.baseUrl}/GetFeed?${params.join('&')}`,
      { headers: this.headers }
    ).pipe(catchError(this.handleError));
  }

  getCategories(): Observable<Array<{ categoryId: number; categoryName: string }>> {
    return this.http.get<any[]>(`${this.baseUrl}/GetCategories`, { headers: this.headers })
      .pipe(catchError(this.handleError));
  }

  getLocalities(): Observable<IPublicLocality[]> {
    return this.http.get<IPublicLocality[]>(`${this.baseUrl}/GetLocalities`, { headers: this.headers })
      .pipe(catchError(this.handleError));
  }

  private handleError(error: HttpErrorResponse): Observable<never> {
    console.error('[PublicService]', error);
    return throwError(() => error);
  }
}
