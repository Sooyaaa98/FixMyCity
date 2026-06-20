// src/app/shared/components/theme-toggle/theme-toggle.component.ts
//
// Phase 5 — sun / moon button that lives in the topbars (navbar) of every
// role layout. Persisted via ThemeService → localStorage; respects OS
// preference on first visit.
// Phase 7 — added icon-spin animation on toggle.

import { Component } from '@angular/core';
import { ThemeService, ThemeMode } from '../../../core/services/theme.service';

@Component({
  selector: 'app-theme-toggle',
  template: `
    <button class="fmc-theme-toggle"
            [class.toggling]="spinning"
            type="button"
            [attr.aria-label]="(current === 'dark' ? 'Switch to light mode' : 'Switch to dark mode')"
            [title]="(current === 'dark' ? 'Switch to light mode' : 'Switch to dark mode')"
            (click)="toggle()">
      <!-- Sun (shown in light mode) -->
      <svg class="fmc-theme-toggle__icon icon-sun" viewBox="0 0 24 24" fill="none"
           stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"
           aria-hidden="true">
        <circle cx="12" cy="12" r="4"></circle>
        <path d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M4.93 19.07l1.41-1.41M17.66 6.34l1.41-1.41"></path>
      </svg>
      <!-- Moon (shown in dark mode) -->
      <svg class="fmc-theme-toggle__icon icon-moon" viewBox="0 0 24 24" fill="none"
           stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"
           aria-hidden="true">
        <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"></path>
      </svg>
    </button>
  `,
  styles: [] // visual styles live in styles.css under .fmc-theme-toggle for theme reactivity
})
export class ThemeToggleComponent {
  current: ThemeMode = this.themeService.current;
  spinning = false;

  constructor(private themeService: ThemeService) {
    this.themeService.theme$.subscribe(m => this.current = m);
  }

  toggle(): void {
    // Briefly set spinning=true so the CSS animation fires, then remove it.
    this.spinning = true;
    this.themeService.toggle();
    setTimeout(() => { this.spinning = false; }, 460);
  }
}
