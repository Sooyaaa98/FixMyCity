// src/app/shared/components/activity-feed/activity-feed.component.ts
//
// Phase 8 (§20) — Unified user activity feed.
// Fetches from /api/User/GetActivityFeed which UNIONs complaint, status,
// points, certificate and comment events in time-descending order.
//
// Used on the citizen home / profile to give a "what have I done lately"
// glanceable view.

import { Component, Input, OnInit } from '@angular/core';
import { UserActivityService, IActivityFeedRow } from '../../../fmc-services/user.service';

const ICONS: Record<string, string> = {
  ComplaintSubmitted: 'bi-pencil-square',
  StatusChange:       'bi-arrow-repeat',
  PointsAwarded:      'bi-star-fill',
  CertificateIssued:  'bi-award-fill',
  CommentPosted:      'bi-chat-dots',
};

@Component({
  selector: 'app-activity-feed',
  templateUrl: './activity-feed.component.html',
  styleUrls: ['./activity-feed.component.css'],
})
export class ActivityFeedComponent implements OnInit {

  /** The user whose feed we are rendering. */
  @Input() userId!: number;

  rows: IActivityFeedRow[] = [];
  loading = false;
  errorMsg = '';
  page = 1;
  pageSize = 20;
  reachedEnd = false;

  constructor(private activity: UserActivityService) {}

  ngOnInit(): void { this.load(); }

  /** Initial load — replaces the list. */
  load(): void {
    this.page = 1;
    this.reachedEnd = false;
    this.rows = [];
    this.fetch();
  }

  /** Pull the next page and append. */
  loadMore(): void {
    if (this.loading || this.reachedEnd) return;
    this.page += 1;
    this.fetch(true);
  }

  iconFor(eventType: string): string {
    return ICONS[eventType] ?? 'bi-circle-fill';
  }

  private fetch(append = false): void {
    if (!this.userId) return;
    this.loading = true;
    this.activity.getActivityFeed(this.userId, this.page, this.pageSize).subscribe({
      next: (rows) => {
        const next = rows ?? [];
        if (next.length < this.pageSize) this.reachedEnd = true;
        this.rows = append ? this.rows.concat(next) : next;
        this.loading = false;
      },
      error: () => {
        this.errorMsg = 'Could not load activity feed.';
        this.loading  = false;
      }
    });
  }
}
