// src/app/fmc-services/auth.service.ts
// Updated: login now returns JWT tokens and populates SessionService correctly.
// The refresh token function is exposed for AuthInterceptor (breaks circular dep).

import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { BehaviorSubject, Observable, of, throwError } from 'rxjs';
import { catchError, map, tap } from 'rxjs/operators';

import { environment } from '../../environments/environment';
import { IApiResponse } from '../fmc-interfaces/api-response.interface';
import {
  ILoginResponse,
  IUserProfile,
  IRegisterCitizenRequest,
  IRegisterOrgRequest,
  IRegisterDeptRequest,
  IChangePasswordRequest,
  IUpdateProfileRequest
} from '../fmc-interfaces/user.interface';
import { IIssueCategory } from '../fmc-interfaces/complaint.interface';
import { SessionService } from '../core/services/session.service';
import { AuthInterceptor } from '../core/interceptors/auth.interceptor';

@Injectable({ providedIn: 'root' })
export class AuthService {

  private baseUrl = `${environment.apiBaseUrl}/api/Auth`;

  private categoriesCache$ = new BehaviorSubject<IIssueCategory[]>([]);
  private localitiesCache$ = new BehaviorSubject<any[]>([]);

  constructor(
    private http:        HttpClient,
    private session:     SessionService,
    private interceptor: AuthInterceptor,   // inject to set refresh function
  ) {
    // Break circular dependency: give the interceptor a reference to refresh()
    this.interceptor.setRefreshFn((rt: string) => this.doRefresh(rt));
  }

  // ── Login ─────────────────────────────────────────────────────────────────

  login(payload: { email: string; password: string }): Observable<ILoginResponse> {
    return this.http.post<ILoginResponse>(`${this.baseUrl}/Login`, payload).pipe(
      tap(res => {
        if (res.success) this.session.saveSession(res);
      }),
      catchError(this.handleError)
    );
  }

  // ── Logout ────────────────────────────────────────────────────────────────

  logout(): Observable<any> {
    const refreshToken = this.session.getRefreshToken();
    this.session.clearSession();
    if (refreshToken) {
      return this.http.post(`${this.baseUrl}/Logout`, { refreshToken })
        .pipe(catchError(() => of(null)));
    }
    return of(null);
  }

  // ── Token refresh (called by AuthInterceptor) ─────────────────────────────

  doRefresh(refreshToken: string): Observable<{ accessToken: string; refreshToken: string }> {
    return this.http.post<any>(`${this.baseUrl}/RefreshToken`, { refreshToken }).pipe(
      map(res => ({
        accessToken:  res.accessToken,
        refreshToken: res.refreshToken,
      }))
    );
  }

  // ── SSO Login ─────────────────────────────────────────────────────────────
  // Posts to /api/Auth/SSOLogin. The backend's usp_SSOLoginOrCreate either
  // signs in an existing SSO user, links SSO to an existing email account, or
  // creates a fresh citizen. Caller is responsible for sourcing
  // SSOExternalId / Email / FullName from a real OAuth provider in production;
  // the demo button on the register/login pages passes synthetic values.

  ssoLogin(payload: {
    ssoProvider: string;
    ssoExternalId: string;
    email: string;
    fullName: string;
  }): Observable<ILoginResponse> {
    return this.http.post<ILoginResponse>(`${this.baseUrl}/SSOLogin`, payload).pipe(
      tap(res => {
        if (res.success) this.session.saveSession(res);
      }),
      catchError(this.handleError)
    );
  }

  // ── Registration ──────────────────────────────────────────────────────────

  registerCitizen(payload: IRegisterCitizenRequest): Observable<IApiResponse> {
    return this.http.post<IApiResponse>(`${this.baseUrl}/RegisterCitizen`, payload)
      .pipe(catchError(this.handleError));
  }

  registerOrganisation(payload: IRegisterOrgRequest): Observable<IApiResponse> {
    return this.http.post<IApiResponse>(`${this.baseUrl}/RegisterOrganisation`, payload)
      .pipe(catchError(this.handleError));
  }

  registerDepartment(payload: IRegisterDeptRequest): Observable<IApiResponse> {
    return this.http.post<IApiResponse>(`${this.baseUrl}/RegisterDepartment`, payload)
      .pipe(catchError(this.handleError));
  }

  // ── Profile ───────────────────────────────────────────────────────────────

  getUserById(userId: number): Observable<IUserProfile> {
    return this.http.get<IUserProfile>(`${this.baseUrl}/GetUserById?userId=${userId}`)
      .pipe(catchError(this.handleError));
  }

  changePassword(payload: IChangePasswordRequest): Observable<IApiResponse> {
    return this.http.post<IApiResponse>(`${this.baseUrl}/ChangePassword`, payload)
      .pipe(catchError(this.handleError));
  }

  updateProfile(payload: IUpdateProfileRequest): Observable<IApiResponse> {
    return this.http.post<IApiResponse>(`${this.baseUrl}/UpdateProfile`, payload)
      .pipe(catchError(this.handleError));
  }

  // ── Reference data (cached) ───────────────────────────────────────────────

  getAllCategories(): Observable<IIssueCategory[]> {
    const cached = this.categoriesCache$.value;
    if (cached.length) return of(cached);
    return this.http.get<IIssueCategory[]>(`${this.baseUrl}/GetAllCategories`).pipe(
      tap(cats => this.categoriesCache$.next(cats)),
      catchError(() => of([]))
    );
  }

  getAllLocalities(): Observable<any[]> {
    const cached = this.localitiesCache$.value;
    if (cached.length) return of(cached);
    return this.http.get<any[]>(`${this.baseUrl}/GetAllLocalities`).pipe(
      tap(locs => this.localitiesCache$.next(locs)),
      catchError(() => of([]))
    );
  }

  // ── Error Handler ─────────────────────────────────────────────────────────

  private handleError(error: HttpErrorResponse): Observable<never> {
    console.error('[AuthService]', error);
    return throwError(() => error);
  }
}
