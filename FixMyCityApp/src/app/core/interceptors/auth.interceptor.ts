// src/app/core/interceptors/auth.interceptor.ts
// Attaches the JWT access token to every outgoing API request.
// On 401 with Token-Expired header → automatically refreshes the token and retries.
// On 401 without the header (bad creds) → clears session and redirects to login.
// Skips auth header for public endpoints (login, register, refresh).

import { Injectable } from '@angular/core';
import {
  HttpRequest,
  HttpHandler,
  HttpEvent,
  HttpInterceptor,
  HttpErrorResponse
} from '@angular/common/http';
import { Observable, BehaviorSubject, throwError, EMPTY } from 'rxjs';
import {
  catchError, filter, switchMap, take, finalize
} from 'rxjs/operators';
import { Router } from '@angular/router';
import { SessionService } from '../services/session.service';

// Lazy import to avoid circular dependency (AuthService also uses HttpClient)
type RefreshFn = (token: string) => Observable<{ accessToken: string; refreshToken: string }>;

@Injectable()
export class AuthInterceptor implements HttpInterceptor {

  // Prevents multiple simultaneous refresh calls
  private isRefreshing       = false;
  private refreshToken$      = new BehaviorSubject<string | null>(null);
  private refreshFn?: RefreshFn;

  constructor(
    private session: SessionService,
    private router:  Router,
  ) {}

  // Called by AuthService to break the circular dependency
  setRefreshFn(fn: RefreshFn): void {
    this.refreshFn = fn;
  }

  // ── Public endpoints that must NOT have an Authorization header ──────────
  // (Adding the header on login causes a 401 loop when the token is expired)
  // Phase 8 — `/api/Public/` is the transparency-portal namespace; anything
  // under it is anonymous. Individual callers can additionally set the
  // `X-Public: true` header for one-off anonymous reads.
  private readonly PUBLIC_PATHS = [
    '/api/Auth/Login',
    '/api/Auth/RefreshToken',
    '/api/Auth/RegisterCitizen',
    '/api/Auth/RegisterOrganisation',
    '/api/Auth/RegisterDepartment',
    '/api/Auth/GetAllCategories',
    '/api/Auth/GetAllLocalities',
    '/api/Auth/ForgotPassword',
    '/api/Auth/ResetPassword',
    '/api/Auth/VerifyResetToken',
    '/api/Public/',
  ];

  intercept(req: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    // Explicit opt-out — any request with X-Public skips the token attach
    // and the 401-refresh dance entirely.
    if (req.headers.has('X-Public')) {
      const stripped = req.clone({ headers: req.headers.delete('X-Public') });
      return next.handle(stripped);
    }

    const isPublic = this.PUBLIC_PATHS.some(p =>
      req.url.includes(p));

    if (isPublic) return next.handle(req);

    const token = this.session.getAccessToken();
    const authReq = token ? this.addToken(req, token) : req;

    return next.handle(authReq).pipe(
      catchError((err: HttpErrorResponse) => {
        if (err.status === 401) {
          const isExpired = err.headers.get('Token-Expired') === 'true';
          if (isExpired) {
            return this.handleTokenRefresh(req, next);
          }
          // Genuine 401 (bad creds / revoked) — log out
          this.logout();
          return EMPTY;
        }
        return throwError(() => err);
      })
    );
  }

  // ── Token rotation ────────────────────────────────────────────────────────

  private handleTokenRefresh(
    originalReq: HttpRequest<unknown>,
    next: HttpHandler
  ): Observable<HttpEvent<unknown>> {
    if (!this.isRefreshing) {
      this.isRefreshing = true;
      this.refreshToken$.next(null);

      const refreshToken = this.session.getRefreshToken();
      if (!refreshToken || !this.refreshFn) {
        this.logout();
        return EMPTY;
      }

      return this.refreshFn(refreshToken).pipe(
        switchMap(({ accessToken, refreshToken: newRefresh }) => {
          this.isRefreshing = false;
          this.session.updateAccessToken(accessToken);
          this.session.updateRefreshToken(newRefresh);
          this.refreshToken$.next(accessToken);
          return next.handle(this.addToken(originalReq, accessToken));
        }),
        catchError(err => {
          this.isRefreshing = false;
          this.logout();
          return throwError(() => err);
        }),
        finalize(() => { this.isRefreshing = false; })
      );
    }

    // Another request is already refreshing — wait for the new token
    return this.refreshToken$.pipe(
      filter(token => token !== null),
      take(1),
      switchMap(token => next.handle(this.addToken(originalReq, token!)))
    );
  }

  private addToken(req: HttpRequest<unknown>, token: string): HttpRequest<unknown> {
    return req.clone({
      setHeaders: { Authorization: `Bearer ${token}` }
    });
  }

  private logout(): void {
    this.session.clearSession();
    this.router.navigate(['/login'], { queryParams: { reason: 'session_expired' } });
  }
}
