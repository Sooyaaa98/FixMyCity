// src/app/shared/components/upvote-button/upvote-button.component.ts
//
// Phase 8 (§1) — Upvote toggle button.
//
// Re-usable across complaint cards, complaint detail pages, public feed, etc.
// Internally calls /api/Complaint/ToggleUpvote and refreshes its own count.
//
// Optimistic UI: the count + voted-state flip the instant the button is
// clicked. If the server call fails the state is reverted and a toast is
// surfaced. Citizens never see a disabled button waiting for a network
// round-trip on the most-tapped affordance in the app.

import { Component, Input, OnChanges, OnInit, SimpleChanges } from '@angular/core';
import { ComplaintService } from '../../../fmc-services/complaint.service';
import { SessionService }   from '../../../core/services/session.service';
import { ToastService }     from '../../../fmc-services/toast.service';

@Component({
  selector: 'app-upvote-button',
  templateUrl: './upvote-button.component.html',
  styleUrls: ['./upvote-button.component.css'],
})
export class UpvoteButtonComponent implements OnInit, OnChanges {

  @Input() complaintId!: number;
  /** Visual variant — `inline` for inside a card, `block` for the detail page. */
  @Input() variant: 'inline' | 'block' = 'inline';

  count: number = 0;
  hasUpvoted = false;
  loading = false;
  // Disable the button while a server round-trip is in flight to prevent
  // double-clicks racing the toggle.
  busy = false;

  constructor(
    private complaintService: ComplaintService,
    private session: SessionService,
    private toast: ToastService,
  ) {}

  ngOnInit(): void { this.refresh(); }
  ngOnChanges(c: SimpleChanges): void {
    if (c['complaintId'] && !c['complaintId'].firstChange) this.refresh();
  }

  private get citizenUserId(): number | null {
    return this.session.getUserId() ?? null;
  }

  /** Pull the latest count + voted-state. Silent on errors. */
  private refresh(): void {
    if (!this.complaintId) return;
    const uid = this.citizenUserId ?? 0;
    this.loading = true;
    this.complaintService.getUpvoteState(this.complaintId, uid).subscribe({
      next: (r) => {
        if (r?.success) {
          this.count      = r.count ?? 0;
          this.hasUpvoted = !!r.hasUpvoted;
        }
        this.loading = false;
      },
      error: () => { this.loading = false; }
    });
  }

  toggle(): void {
    if (this.busy) return;
    const uid = this.citizenUserId;
    if (!uid) {
      this.toast.info('Please log in to vote.');
      return;
    }

    // Optimistic flip
    const wasVoted    = this.hasUpvoted;
    this.hasUpvoted   = !wasVoted;
    this.count       += wasVoted ? -1 : 1;
    this.busy = true;

    this.complaintService.toggleUpvote(this.complaintId, uid).subscribe({
      next: (r) => {
        this.busy = false;
        if (r?.success) {
          // Reconcile with the server-truth count (handles concurrent voters).
          this.count      = r.newCount ?? this.count;
          this.hasUpvoted = !!r.hasUpvoted;
        } else {
          // Rollback the optimistic flip
          this.hasUpvoted = wasVoted;
          this.count     += wasVoted ? 1 : -1;
          this.toast.error('Could not register your vote.');
        }
      },
      error: () => {
        this.busy = false;
        this.hasUpvoted = wasVoted;
        this.count     += wasVoted ? 1 : -1;
        this.toast.error('Could not register your vote.');
      }
    });
  }
}
