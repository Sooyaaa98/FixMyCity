// src/app/shared/components/status-badge/status-badge.component.ts

import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-status-badge',
  templateUrl: './status-badge.component.html',
  styleUrls: ['./status-badge.component.css']
})
export class StatusBadgeComponent {
  @Input() status: string = '';

  // Returns the CSS class matching §8 — badge-Submitted, badge-Resolved, etc.
  get badgeClass(): string {
    return `badge-status badge-${this.status}`;
  }
}
