// src/app/shared/components/toast/toast.component.ts

import { Component, OnInit, OnDestroy } from '@angular/core';
import { Subscription } from 'rxjs';
import { IToastMessage, ToastService } from '../../../fmc-services/toast.service';

@Component({
  selector: 'app-toast',
  templateUrl: './toast.component.html',
  styleUrls: ['./toast.component.css']
})
export class ToastComponent implements OnInit, OnDestroy {

  toasts: IToastMessage[] = [];
  private sub!: Subscription;

  constructor(private toastService: ToastService) {}

  ngOnInit(): void {
    this.sub = this.toastService.toasts$.subscribe(toasts => {
      this.toasts = toasts;
    });
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  /**
   * Phase 6 — play the leave animation first, then remove from state.
   * If the DOM node can't be found (race / SSR), dismiss immediately so
   * the toast never gets stuck on screen.
   */
  dismiss(id: string): void {
    const el = document.querySelector(`[data-toast-id="${id}"]`) as HTMLElement | null;
    if (!el) { this.toastService.dismiss(id); return; }
    el.classList.add('leaving');
    el.addEventListener(
      'animationend',
      () => this.toastService.dismiss(id),
      { once: true }
    );
    // Failsafe: if the animation event never fires, dismiss after 400ms.
    setTimeout(() => this.toastService.dismiss(id), 400);
  }

  trackById(_: number, t: IToastMessage): string {
    return t.id;
  }

  iconFor(type: string): string {
    const icons: Record<string, string> = {
      success: 'bi bi-check-circle-fill',
      error:   'bi bi-x-circle-fill',
      warning: 'bi bi-exclamation-triangle-fill',
      info:    'bi bi-info-circle-fill'
    };
    return icons[type] ?? 'bi bi-bell-fill';
  }
}
