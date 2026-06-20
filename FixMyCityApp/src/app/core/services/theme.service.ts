// src/app/core/services/theme.service.ts
//
// Phase 5 — Theme system for FixMyCity. Drives the `[data-theme]` attribute on
// the root <html> element, persists the user's choice in localStorage, and
// falls back to `prefers-color-scheme` on first visit.
//
// The styles.css token system reads `[data-theme="dark"]` and overrides the
// neutral palette; every component that uses the existing `--fmc-*` variables
// automatically picks up the new colours.

import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';

export type ThemeMode = 'light' | 'dark';

const STORAGE_KEY  = 'fmc_theme';
const ATTRIBUTE    = 'data-theme';

@Injectable({ providedIn: 'root' })
export class ThemeService {

  private readonly _theme$ = new BehaviorSubject<ThemeMode>(this._resolveInitial());

  /** Observable stream of theme changes — components subscribe to update icons / aria. */
  readonly theme$: Observable<ThemeMode> = this._theme$.asObservable();

  constructor() {
    // Apply on construction so the initial paint already reflects the resolved theme.
    this._apply(this._theme$.value);

    // If the user hasn't manually picked one yet, follow OS theme changes live.
    if (window.matchMedia && !this._hasUserOverride()) {
      const mq = window.matchMedia('(prefers-color-scheme: dark)');
      const handler = (e: MediaQueryListEvent) => {
        // Only react if the user still hasn't overridden their preference.
        if (!this._hasUserOverride()) {
          this._setInternal(e.matches ? 'dark' : 'light', /*persist=*/false);
        }
      };
      try { mq.addEventListener('change', handler); }
      catch { mq.addListener?.(handler); /* Safari < 14 */ }
    }
  }

  get current(): ThemeMode { return this._theme$.value; }

  /** Explicit user toggle — persists. */
  toggle(): void {
    this.setTheme(this.current === 'dark' ? 'light' : 'dark');
  }

  /** Explicit set — persists. */
  setTheme(mode: ThemeMode): void {
    this._setInternal(mode, /*persist=*/true);
  }

  /** Reset to OS-driven mode. */
  clearOverride(): void {
    localStorage.removeItem(STORAGE_KEY);
    const next: ThemeMode = window.matchMedia?.('(prefers-color-scheme: dark)')?.matches
      ? 'dark'
      : 'light';
    this._setInternal(next, /*persist=*/false);
  }

  // ── Internals ──────────────────────────────────────────────────────────

  private _setInternal(mode: ThemeMode, persist: boolean): void {
    this._apply(mode);
    this._theme$.next(mode);
    if (persist) {
      try { localStorage.setItem(STORAGE_KEY, mode); }
      catch { /* private browsing → ignore */ }
    }
  }

  private _apply(mode: ThemeMode): void {
    const root = document.documentElement;
    // Trigger the theme-switch flash animation (CSS removes it via animation).
    root.classList.add('theme-switching');
    if (mode === 'dark') root.setAttribute(ATTRIBUTE, 'dark');
    else                 root.removeAttribute(ATTRIBUTE);
    // Remove the class after the animation completes (450ms).
    setTimeout(() => root.classList.remove('theme-switching'), 500);
  }

  private _resolveInitial(): ThemeMode {
    // 1. User override (set via toggle) wins.
    const saved = (() => { try { return localStorage.getItem(STORAGE_KEY); } catch { return null; }})();
    if (saved === 'dark' || saved === 'light') return saved;

    // 2. OS preference.
    if (window.matchMedia?.('(prefers-color-scheme: dark)')?.matches) return 'dark';

    // 3. Default.
    return 'light';
  }

  private _hasUserOverride(): boolean {
    try {
      const v = localStorage.getItem(STORAGE_KEY);
      return v === 'light' || v === 'dark';
    } catch { return false; }
  }
}
