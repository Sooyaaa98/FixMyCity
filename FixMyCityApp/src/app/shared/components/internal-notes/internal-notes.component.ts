// src/app/shared/components/internal-notes/internal-notes.component.ts
//
// Phase 8 (§15) — Internal notes panel.
//
// Mirrors comments-thread but talks to AddInternalNote / GetInternalNotes
// (which are [Authorize(Roles="Solver,SuperAdmin")] on the API side).
// The component shows nothing if the current user lacks the right role so
// it can be embedded on any complaint detail page without conditional
// wrapping at the call site.

import { Component, Input, OnChanges, OnInit, SimpleChanges } from '@angular/core';
import { ComplaintService } from '../../../fmc-services/complaint.service';
import { SessionService }   from '../../../core/services/session.service';
import { ToastService }     from '../../../fmc-services/toast.service';

interface INoteRow {
  noteId:      number;
  complaintId: number;
  noteText:    string;
  createdAt:   string;
  authorName?: string;
  authorRole?: string;
}

@Component({
  selector: 'app-internal-notes',
  templateUrl: './internal-notes.component.html',
  styleUrls: ['./internal-notes.component.css'],
})
export class InternalNotesComponent implements OnInit, OnChanges {

  @Input() complaintId!: number;

  notes: INoteRow[] = [];
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

  get canAccess(): boolean {
    const r = this.session.getRole?.() ?? '';
    return r === 'Solver' || r === 'SuperAdmin';
  }

  load(): void {
    if (!this.canAccess || !this.complaintId) return;
    this.loading = true;
    this.complaintService.getInternalNotes(this.complaintId).subscribe({
      next: (rows) => { this.notes = rows ?? []; this.loading = false; },
      error: () => { this.loading = false; }
    });
  }

  add(): void {
    const text = this.draft.trim();
    if (!text || text.length < 2 || !this.canAccess) return;
    const uid = this.session.getUserId();
    if (!uid) return;
    this.posting = true;
    this.complaintService.addInternalNote({
      complaintId:     this.complaintId,
      createdByUserId: uid,
      noteText:        text,
    }).subscribe({
      next: (r) => {
        this.posting = false;
        if (r?.success) {
          this.draft = '';
          this.load();
        } else {
          this.toast.error('Could not save note.');
        }
      },
      error: () => {
        this.posting = false;
        this.toast.error('Could not save note.');
      }
    });
  }
}
