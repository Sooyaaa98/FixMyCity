// src/app/shared/pipes/sla.pipe.ts
//
// Phase 8 (§14) — renders an SLA countdown / overdue badge from a complaint's
// EstimatedResDate (or SubmittedAt + default-SLA-days fallback).
//
// Output examples:
//   future ETA, 3 days away   → "3 days left"
//   future ETA, < 24 h         → "8 hours left"
//   past ETA                   → "2 days overdue"
//   missing ETA                → "—"
//
// Pure pipe — fast, change-detection-friendly. Components can also feed in
// the literal status string and the pipe will short-circuit to '' for already-
// resolved / rejected / linked complaints (no SLA on closed work).

import { Pipe, PipeTransform } from '@angular/core';

@Pipe({ name: 'sla' })
export class SlaPipe implements PipeTransform {

  transform(
    estDate: string | Date | null | undefined,
    status?: string | null,
  ): string {
    // SLA does not apply to closed complaints.
    if (status && ['Resolved', 'Rejected', 'Linked'].includes(status)) return '';

    if (!estDate) return '—';
    const due = typeof estDate === 'string' ? new Date(estDate) : estDate;
    if (isNaN(due.getTime())) return '—';

    const now = Date.now();
    const diffMs = due.getTime() - now;
    const absMs  = Math.abs(diffMs);

    const days  = Math.floor(absMs / 86_400_000);
    const hours = Math.floor((absMs % 86_400_000) / 3_600_000);

    if (diffMs >= 0) {
      // Still on time
      if (days >= 1) return `${days} day${days === 1 ? '' : 's'} left`;
      if (hours >= 1) return `${hours} hour${hours === 1 ? '' : 's'} left`;
      return 'Due today';
    }

    // Overdue
    if (days >= 1) return `${days} day${days === 1 ? '' : 's'} overdue`;
    if (hours >= 1) return `${hours} hour${hours === 1 ? '' : 's'} overdue`;
    return 'Overdue';
  }
}

/**
 * Maps an SLA string from `SlaPipe` to a Bootstrap badge variant so templates
 * can stay declarative:  `<span [class]="'badge ' + (estDate | slaBadgeClass)">`
 */
@Pipe({ name: 'slaBadgeClass' })
export class SlaBadgeClassPipe implements PipeTransform {

  transform(
    estDate: string | Date | null | undefined,
    status?: string | null,
  ): string {
    if (status && ['Resolved', 'Rejected', 'Linked'].includes(status)) return 'bg-secondary';
    if (!estDate) return 'bg-secondary';
    const due = typeof estDate === 'string' ? new Date(estDate) : estDate;
    if (isNaN(due.getTime())) return 'bg-secondary';

    const diffMs = due.getTime() - Date.now();
    if (diffMs < 0)               return 'bg-danger';   // overdue
    if (diffMs < 86_400_000)      return 'bg-warning text-dark'; // < 24 h
    if (diffMs < 3 * 86_400_000)  return 'bg-info text-dark';    // < 3 days
    return 'bg-success';
  }
}
