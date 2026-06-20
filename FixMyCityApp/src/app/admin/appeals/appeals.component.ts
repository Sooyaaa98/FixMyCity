// src/app/admin/appeals/appeals.component.ts
//
// Phase 8 (§6) — Admin queue for citizen appeals against rejected complaints.

import { Component, OnInit } from '@angular/core';
import { AdminService } from '../../fmc-services/admin.service';
import { SessionService } from '../../core/services/session.service';
import { ToastService } from '../../fmc-services/toast.service';

interface IAppealRow {
  appealId:          number;
  complaintId:       number;
  complaintTitle?:   string;
  complaintCategory?:string;
  complaintLocality?:string;
  citizenName?:      string;
  reason:            string;
  status:            string;
  decision?:         string;
  adminNote?:        string;
  createdAt:         string;
  resolvedAt?:       string;
  adminName?:        string;
}

@Component({
  selector: 'app-appeals',
  templateUrl: './appeals.component.html',
  styleUrls: ['./appeals.component.css'],
})
export class AppealsComponent implements OnInit {

  appeals: IAppealRow[] = [];
  filter:  string = 'Pending';   // 'Pending' | 'Resolved' | ''
  loading = false;

  // Modal state
  selected: IAppealRow | null = null;
  noteDraft = '';

  constructor(
    private admin: AdminService,
    private session: SessionService,
    private toast: ToastService,
  ) {}

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading = true;
    this.admin.getAppeals(this.filter).subscribe({
      next: (rows) => { this.appeals = (rows ?? []) as IAppealRow[]; this.loading = false; },
      error: () => { this.loading = false; }
    });
  }

  setFilter(f: string): void { this.filter = f; this.load(); }

  open(row: IAppealRow): void {
    this.selected = row;
    this.noteDraft = '';
  }
  close(): void { this.selected = null; this.noteDraft = ''; }

  decide(decision: 'Approved' | 'Rejected'): void {
    if (!this.selected) return;
    const uid = this.session.getUserId();
    if (!uid) return;
    this.admin.resolveAppeal({
      appealId:    this.selected.appealId,
      adminUserId: uid,
      decision,
      adminNote:   this.noteDraft.trim() || undefined,
    }).subscribe({
      next: (r) => {
        if (r?.success) {
          this.toast.success(`Appeal ${decision.toLowerCase()}.`);
          this.close();
          this.load();
        } else {
          this.toast.error((r as any)?.message ?? 'Could not save decision.');
        }
      },
      error: () => this.toast.error('Could not save decision.')
    });
  }
}
