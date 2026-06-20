// src/app/core/services/session.service.ts
// Stores JWT tokens and user profile.
// Access token → sessionStorage (cleared on tab close — intentional for security).
// Refresh token → localStorage (persists across tabs so "remember me" works).
// NEVER store the access token in localStorage — it's a known XSS vector.

import { Injectable } from '@angular/core';
import { ILoginResponse } from '../../fmc-interfaces/user.interface';

@Injectable({ providedIn: 'root' })
export class SessionService {

  private readonly USER_KEY          = 'fmc_user';
  private readonly ACCESS_TOKEN_KEY  = 'fmc_access';   // sessionStorage
  private readonly REFRESH_TOKEN_KEY = 'fmc_refresh';  // localStorage

  // ── Save ──────────────────────────────────────────────────────────────────

  saveSession(res: ILoginResponse): void {
    // Flatten user object into session for backward compatibility
    const flat = {
      ...res,
      userId:     res.user?.userId     ?? res.userId,
      fullName:   res.user?.fullName   ?? res.fullName,
      email:      res.user?.email      ?? res.email,
      roleId:     res.user?.roleId     ?? res.roleId,
      roleName:   res.user?.roleName   ?? res.roleName,
      localityId: res.user?.localityId ?? res.localityId,
      deptId:     res.user?.deptId     ?? res.deptId,
      orgId:      res.user?.orgId      ?? res.orgId,
    };
    sessionStorage.setItem(this.USER_KEY, JSON.stringify(flat));

    if (res.accessToken)
      sessionStorage.setItem(this.ACCESS_TOKEN_KEY, res.accessToken);

    if (res.refreshToken)
      localStorage.setItem(this.REFRESH_TOKEN_KEY, res.refreshToken);
  }

  updateAccessToken(accessToken: string): void {
    sessionStorage.setItem(this.ACCESS_TOKEN_KEY, accessToken);
  }

  updateRefreshToken(refreshToken: string): void {
    localStorage.setItem(this.REFRESH_TOKEN_KEY, refreshToken);
  }

  // ── Read ──────────────────────────────────────────────────────────────────

  getUser(): ILoginResponse | null {
    const raw = sessionStorage.getItem(this.USER_KEY);
    if (!raw) return null;
    try { return JSON.parse(raw) as ILoginResponse; }
    catch { return null; }
  }

  getAccessToken(): string | null {
    return sessionStorage.getItem(this.ACCESS_TOKEN_KEY);
  }

  getRefreshToken(): string | null {
    return localStorage.getItem(this.REFRESH_TOKEN_KEY);
  }

  // ── Convenience accessors (unchanged API) ─────────────────────────────────

  getUserId(): number      { return this.getUser()?.userId    ?? 0; }
  getFullName(): string    { return this.getUser()?.fullName  ?? ''; }
  getEmail(): string       { return this.getUser()?.email     ?? ''; }
  getRole(): string        { return this.getUser()?.roleName  ?? ''; }
  getRoleId(): number      { return this.getUser()?.roleId    ?? 0; }
  getLocalityId(): number  { return this.getUser()?.localityId ?? 0; }
  getOrgId(): number       { return this.getUser()?.orgId     ?? 0; }
  getDeptId(): number      { return this.getUser()?.deptId    ?? 0; }

  // ── Auth state ────────────────────────────────────────────────────────────

  isLoggedIn(): boolean {
    return this.getUser() !== null && this.getAccessToken() !== null;
  }

  // ── Destroy ───────────────────────────────────────────────────────────────

  clearSession(): void {
    sessionStorage.removeItem(this.USER_KEY);
    sessionStorage.removeItem(this.ACCESS_TOKEN_KEY);
    localStorage.removeItem(this.REFRESH_TOKEN_KEY);
  }
}
