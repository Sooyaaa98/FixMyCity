// src/app/shared/components/skeleton-card/skeleton-card.component.ts
// Phase 6 — Shimmer placeholder used while complaint / dashboard data loads.
// Drop in as <app-skeleton-card> (optionally inside *ngFor) — no inputs needed.

import { Component } from '@angular/core';

@Component({
  selector: 'app-skeleton-card',
  template: `
    <div class="sk-card" aria-hidden="true">
      <div class="sk-row sk-row--between">
        <div class="fmc-skeleton" style="height:12px; width:60px;"></div>
        <div class="fmc-skeleton" style="height:20px; width:70px; border-radius:20px;"></div>
      </div>
      <div class="fmc-skeleton sk-title"></div>
      <div class="fmc-skeleton sk-line"></div>
      <div class="fmc-skeleton sk-line sk-line--short"></div>
      <div class="sk-chips">
        <div class="fmc-skeleton sk-chip"></div>
        <div class="fmc-skeleton sk-chip"></div>
        <div class="fmc-skeleton sk-chip"></div>
      </div>
    </div>
  `,
  styles: [`
    .sk-card {
      background: var(--fmc-surface);
      border-radius: var(--fmc-radius-lg);
      border: 1px solid var(--fmc-border);
      padding: 18px 20px;
      margin-bottom: 12px;
    }
    .sk-row { display: flex; align-items: center; margin-bottom: 12px; }
    .sk-row--between { justify-content: space-between; }
    .sk-title       { height: 16px; width: 55%; margin-bottom: 10px; }
    .sk-line        { height: 12px; width: 90%; margin-bottom: 6px; }
    .sk-line--short { width: 70%; }
    .sk-chips       { display: flex; gap: 8px; margin-top: 10px; flex-wrap: wrap; }
    .sk-chip        { height: 22px; width: 80px; border-radius: 20px; }
  `]
})
export class SkeletonCardComponent {}
