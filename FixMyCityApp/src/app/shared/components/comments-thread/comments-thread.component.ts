// src/app/shared/components/comments-thread/comments-thread.component.ts
//
// Phase 8 (§7) — Comments thread on a complaint.
//
// Shown on every complaint detail page (citizen, solver, admin). The
// server-side SP stamps IsOfficialReply automatically when the author has the
// Solver / SuperAdmin role, so the UI just renders the badge — never the
// client deciding "is this official".

import { Component, Input, OnChanges, OnInit, SimpleChanges } from '@angular/core';
import { ComplaintService } from '../../../fmc-services/complaint.service';
import { SessionService }   from '../../../core/services/session.service';
import { ToastService }     from '../../../fmc-services/toast.service';

interface ICommentRow {
  commentId:       number;
  complaintId:     number;
  userId:          number;
  commentText:     string;
  isOfficialReply: boolean;
  createdAt:       string;
  authorName?:     string;
  authorRole?:     string;
}

@Component({
  selector: 'app-comments-thread',
  templateUrl: './comments-thread.component.html',
  styleUrls: ['./comments-thread.component.css'],
})
export class CommentsThreadComponent implements OnInit, OnChanges {

  @Input() complaintId!: number;

  comments: ICommentRow[] = [];
  loading = false;
  posting = false;
  draft = '';

  constructor(
    private complaintService: ComplaintService,
    private session: SessionService,
    private toast: ToastService,
  ) {}

  ngOnInit(): void { this.load(); }
  ngOnChanges(c: SimpleChanges): void {
    if (c['complaintId'] && !c['complaintId'].firstChange) this.load();
  }

  load(): void {
    if (!this.complaintId) return;
    this.loading = true;
    this.complaintService.getComments(this.complaintId).subscribe({
      next: (rows) => { this.comments = rows ?? []; this.loading = false; },
      error: () => { this.loading = false; }
    });
  }

  post(): void {
    const text = this.draft.trim();
    if (!text || text.length < 2) return;
    const uid = this.session.getUserId();
    if (!uid) {
      this.toast.info('Please log in to comment.');
      return;
    }
    this.posting = true;
    this.complaintService.addComment({
      complaintId: this.complaintId,
      userId:      uid,
      commentText: text,
    }).subscribe({
      next: (r) => {
        this.posting = false;
        if (r?.success) {
          this.draft = '';
          this.load();
        } else {
          this.toast.error('Could not post your comment.');
        }
      },
      error: () => {
        this.posting = false;
        this.toast.error('Could not post your comment.');
      }
    });
  }

  delete(commentId: number): void {
    const uid = this.session.getUserId();
    if (!uid) return;
    this.complaintService.deleteComment(commentId, uid).subscribe({
      next: (r) => {
        if (r?.success) {
          this.comments = this.comments.filter(c => c.commentId !== commentId);
        } else {
          this.toast.error('Could not delete the comment.');
        }
      },
      error: () => this.toast.error('Could not delete the comment.')
    });
  }

  canDelete(c: ICommentRow): boolean {
    const uid = this.session.getUserId();
    const role = this.session.getRole?.() ?? '';
    return c.userId === uid || role === 'SuperAdmin';
  }
}
