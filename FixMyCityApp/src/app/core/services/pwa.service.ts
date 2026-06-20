// src/app/core/services/pwa.service.ts
//
// Phase 8 (§19) — PWA install + service worker registration.
//
// Listens for the `beforeinstallprompt` event so the citizen-layout can
// surface an "Install app" button at the right moment. Also registers the
// minimal service worker shipped at /fmc-sw.js.
//
// All ops are best-effort — the service must NEVER throw on browsers that
// lack PWA support (older Firefox, in-app browsers, etc).

import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

interface IBeforeInstallPromptEvent extends Event {
  prompt: () => Promise<void>;
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>;
}

@Injectable({ providedIn: 'root' })
export class PwaService {

  /** True once a deferred installPrompt has been captured. */
  readonly canInstall$ = new BehaviorSubject<boolean>(false);

  /** True after the app reports itself as running standalone. */
  readonly isInstalled$ = new BehaviorSubject<boolean>(false);

  private deferred: IBeforeInstallPromptEvent | null = null;

  /** Called once from AppComponent. Idempotent. */
  init(): void {
    if (typeof window === 'undefined') return;

    // 1. Register the service worker (best effort).
    if ('serviceWorker' in navigator) {
      window.addEventListener('load', () => {
        navigator.serviceWorker.register('/fmc-sw.js')
          .catch(err => console.warn('[PWA] SW registration failed', err));
      });
    }

    // 2. Capture the install prompt so we can fire it on demand.
    window.addEventListener('beforeinstallprompt', (e: any) => {
      e.preventDefault();
      this.deferred = e as IBeforeInstallPromptEvent;
      this.canInstall$.next(true);
    });

    // 3. Watch installed state — UA-specific but reliable enough.
    const mql = window.matchMedia?.('(display-mode: standalone)');
    const setInstalled = () => this.isInstalled$.next(
      mql?.matches || (window.navigator as any).standalone === true);
    setInstalled();
    mql?.addEventListener?.('change', setInstalled);

    window.addEventListener('appinstalled', () => {
      this.deferred = null;
      this.canInstall$.next(false);
      this.isInstalled$.next(true);
    });
  }

  /** Trigger the deferred install prompt. Returns the user choice. */
  async promptInstall(): Promise<'accepted' | 'dismissed' | 'unavailable'> {
    if (!this.deferred) return 'unavailable';
    await this.deferred.prompt();
    const choice = await this.deferred.userChoice;
    this.deferred = null;
    this.canInstall$.next(false);
    return choice.outcome;
  }
}
