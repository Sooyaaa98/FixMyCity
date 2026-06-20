// src/app/fmc-services/toast.service.ts

import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

export interface IToastMessage {
  id: string;
  type: 'success' | 'error' | 'info' | 'warning';
  message: string;
  timestamp: number;
}

@Injectable({ providedIn: 'root' })
export class ToastService {

  private readonly MAX_TOASTS = 3;
  private readonly AUTO_DISMISS_MS = 4000;

  private _toasts$ = new BehaviorSubject<IToastMessage[]>([]);
  toasts$ = this._toasts$.asObservable();

  // ── Public API ────────────────────────────────────────────────────────────

  success(message: string): void {
    this._add('success', message);
  }

  error(message: string): void {
    this._add('error', message);
  }

  info(message: string): void {
    this._add('info', message);
  }

  warning(message: string): void {
    this._add('warning', message);
  }

  dismiss(id: string): void {
    const current = this._toasts$.value.filter(t => t.id !== id);
    this._toasts$.next(current);
  }

  // ── Internal ──────────────────────────────────────────────────────────────

  private _add(type: IToastMessage['type'], message: string): void {
    const id = `toast-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`;
    const toast: IToastMessage = { id, type, message, timestamp: Date.now() };

    let current = [...this._toasts$.value, toast];

    // Cap at MAX_TOASTS — drop oldest
    if (current.length > this.MAX_TOASTS) {
      current = current.slice(current.length - this.MAX_TOASTS);
    }

    this._toasts$.next(current);

    // Auto-dismiss after timeout
    setTimeout(() => this.dismiss(id), this.AUTO_DISMISS_MS);
  }
}
